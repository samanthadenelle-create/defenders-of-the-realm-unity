import { PublicKey } from "@solana/web3.js";
import { getAssociatedTokenAddressSync, TOKEN_PROGRAM_ID, TOKEN_2022_PROGRAM_ID } from "@solana/spl-token";

const owner = new PublicKey("2VePaneS3xX2EdzSbe4JdiovRffboLJV4yNVmVTkeuCg");
const mint  = new PublicKey("SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3");

console.log("treasury owner :", owner.toBase58());
console.log("  on ed25519 curve (a real wallet, not a PDA):", PublicKey.isOnCurve(owner.toBytes()));
console.log("mainnet SKR mint:", mint.toBase58());
console.log("");
console.log("ATA (classic TOKEN_PROGRAM) :", getAssociatedTokenAddressSync(mint, owner, true, TOKEN_PROGRAM_ID).toBase58());
console.log("ATA (TOKEN_2022)            :", getAssociatedTokenAddressSync(mint, owner, true, TOKEN_2022_PROGRAM_ID).toBase58());
