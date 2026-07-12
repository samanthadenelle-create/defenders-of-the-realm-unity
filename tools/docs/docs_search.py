#!/usr/bin/env python3
"""
Defenders of the Realm – Semantic Documentation Search
======================================================
TF-IDF based semantic search over every markdown file under docs/.

Usage:
  python docs_search.py "how do free placement and pillagers work"
  python docs_search.py "motion caster vfx delay" --top 8
  python docs_search.py "starting budget" --path docs

No internet, no external models. Pure numpy + scipy TF-IDF.
"""

from __future__ import annotations

import argparse
import math
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
from scipy.sparse import csr_matrix, vstack
from scipy.spatial.distance import cosine


# ---------------------------------------------------------------------------
# Tokenization & TF-IDF
# ---------------------------------------------------------------------------

STOPWORDS = {
    "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of",
    "with", "by", "from", "is", "are", "was", "were", "be", "been", "being",
    "have", "has", "had", "do", "does", "did", "will", "would", "could",
    "should", "may", "might", "must", "shall", "can", "this", "that", "these",
    "those", "it", "its", "they", "them", "their", "we", "our", "you", "your",
    "i", "me", "my", "he", "she", "him", "her", "as", "if", "then", "else",
    "when", "where", "why", "how", "all", "any", "both", "each", "few", "more",
    "most", "other", "some", "such", "no", "nor", "not", "only", "own", "same",
    "so", "than", "too", "very", "just", "also", "now", "here", "there",
}


def tokenize(text: str) -> List[str]:
    """Simple but effective tokenizer for technical docs."""
    text = text.lower()
    # Keep code-ish tokens and compound words
    text = re.sub(r"[^\w\s\-\./]", " ", text)
    tokens = re.findall(r"[a-z0-9][a-z0-9\-\./_]{1,}", text)
    return [t for t in tokens if t not in STOPWORDS and len(t) > 1]


class TfidfIndex:
    def __init__(self):
        self.docs: List[Dict] = []          # metadata
        self.vocab: Dict[str, int] = {}
        self.idf: np.ndarray | None = None
        self.matrix: csr_matrix | None = None  # document-term matrix (tf-idf)

    def add_document(self, path: Path, title: str, content: str):
        tokens = tokenize(title + " " + content)
        self.docs.append({
            "path": path,
            "title": title,
            "content": content,
            "tokens": tokens,
            "term_counts": Counter(tokens),
        })

    def build(self):
        if not self.docs:
            return

        # Build vocabulary
        all_terms = set()
        for d in self.docs:
            all_terms.update(d["term_counts"].keys())
        self.vocab = {term: i for i, term in enumerate(sorted(all_terms))}
        V = len(self.vocab)
        N = len(self.docs)

        # Document frequency
        df = np.zeros(V, dtype=np.float64)
        for d in self.docs:
            for term in d["term_counts"]:
                df[self.vocab[term]] += 1

        # IDF with smoothing
        self.idf = np.log((N + 1) / (df + 1)) + 1.0

        # Build sparse TF-IDF matrix
        rows, cols, data = [], [], []
        for doc_idx, d in enumerate(self.docs):
            total = sum(d["term_counts"].values()) or 1
            for term, count in d["term_counts"].items():
                term_idx = self.vocab[term]
                tf = count / total
                tfidf = tf * self.idf[term_idx]
                rows.append(doc_idx)
                cols.append(term_idx)
                data.append(tfidf)

        self.matrix = csr_matrix((data, (rows, cols)), shape=(N, V))

        # L2-normalize rows for cosine
        norms = np.sqrt(self.matrix.multiply(self.matrix).sum(axis=1)).A1
        norms[norms == 0] = 1.0
        self.matrix = self.matrix.multiply(1.0 / norms[:, np.newaxis])

    def search(self, query: str, top_k: int = 8) -> List[Tuple[float, Dict]]:
        if self.matrix is None or not self.docs:
            return []

        q_tokens = tokenize(query)
        if not q_tokens:
            return []

        # Query vector
        q_counts = Counter(q_tokens)
        total = sum(q_counts.values())
        q_vec = np.zeros(len(self.vocab), dtype=np.float64)
        for term, count in q_counts.items():
            if term in self.vocab:
                tf = count / total
                q_vec[self.vocab[term]] = tf * self.idf[self.vocab[term]]

        # Normalize
        q_norm = np.linalg.norm(q_vec)
        if q_norm == 0:
            return []
        q_vec /= q_norm

        # Cosine similarity (matrix is already L2-normalized)
        scores = self.matrix.dot(q_vec)

        # Top-k
        top_indices = np.argsort(scores)[::-1][:top_k]
        results = []
        for idx in top_indices:
            score = float(scores[idx])
            if score < 0.01:  # filter noise
                continue
            results.append((score, self.docs[idx]))
        return results


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def extract_title(content: str, filename: str) -> str:
    for line in content.splitlines()[:40]:
        line = line.strip()
        if line.startswith("# "):
            return line[2:].strip()
    name = Path(filename).stem
    name = re.sub(r"^\d+[_\-\s]*", "", name)
    return name.replace("_", " ").replace("-", " ").title()


def snippet(content: str, query_tokens: List[str], max_len: int = 220) -> str:
    """Return a short relevant snippet."""
    lines = [l.strip() for l in content.splitlines() if l.strip() and not l.startswith("#")]
    if not lines:
        return content[:max_len].replace("\n", " ") + "…"

    # Prefer lines that contain query terms
    best = None
    best_score = -1
    for line in lines[:80]:
        lower = line.lower()
        score = sum(1 for t in query_tokens if t in lower)
        if score > best_score:
            best_score = score
            best = line
    if best is None:
        best = lines[0]
    if len(best) > max_len:
        best = best[:max_len] + "…"
    return best


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Semantic search over project documentation")
    parser.add_argument("query", nargs="+", help="Search query")
    parser.add_argument("--path", default="docs", help="Path to docs folder (default: docs)")
    parser.add_argument("--top", type=int, default=8, help="Number of results (default: 8)")
    parser.add_argument("--show-snippet", action="store_true", default=True)
    args = parser.parse_args()

    query = " ".join(args.query)
    docs_root = Path(args.path)

    if not docs_root.exists():
        print(f"❌ Docs path not found: {docs_root}", file=sys.stderr)
        sys.exit(1)

    print(f"🔍 Building index from {docs_root} …")
    index = TfidfIndex()

    count = 0
    for md in docs_root.rglob("*.md"):
        if md.name.startswith("00_MASTER") or "node_modules" in md.parts:
            continue
        try:
            content = md.read_text(encoding="utf-8", errors="replace")
            title = extract_title(content, md.name)
            index.add_document(md, title, content)
            count += 1
        except Exception as e:
            print(f"  ⚠️  Skipped {md}: {e}", file=sys.stderr)

    print(f"   Indexed {count} documents")
    index.build()

    print(f"\n🔎 Query: “{query}”\n")
    results = index.search(query, top_k=args.top)

    if not results:
        print("No relevant documents found.")
        return

    q_tokens = tokenize(query)
    for rank, (score, doc) in enumerate(results, 1):
        rel = doc["path"].relative_to(docs_root) if docs_root in doc["path"].parents else doc["path"]
        print(f"{rank}. [{score:.3f}]  {doc['title']}")
        print(f"   📄  {rel.as_posix()}")
        if args.show_snippet:
            snip = snippet(doc["content"], q_tokens)
            print(f"   💬  {snip}")
        print()


if __name__ == "__main__":
    main()
