'use strict';
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');
const contest = require('../api/_lib/showcase-contest');
const voteModule = require('../api/showcase/vote');
const discoverModule = require('../api/showcase/discover');
const countsModule = require('../api/showcase/vote-counts');
const finalizeModule = require('../api/admin/showcase-finalize');
const reverseModule = require('../api/admin/showcase-reverse');

function res() { return { statusCode:0, body:null, headers:{}, setHeader(k,v){this.headers[k]=v;},
    status(c){this.statusCode=c;return this;}, json(b){this.body=b;return this;}, end(){return this;} }; }
function req(method, body, query={}) { return { method, query, headers:{'x-session':'proof'},
    body:JSON.stringify(body), complete:true, readableEnded:true }; }
const enabled = { COMMUNITY_SHOWCASE_VOTING_ENABLED:'true' };
const vote = { playerId:'private-player', contestId:'first_watch_1', categoryId:'best_realm', showcaseId:'sh_7Hy3qP9mN2xK4v8Q' };

test('feature is default-off and malformed vote fields fail closed', async () => {
    assert.equal(contest.enabled({}), false);
    assert.equal(contest.enabled(enabled), true);
    assert.equal(contest.validateVote({...vote, cosmeticSku:'client-choice'}), null);
    let queried=false;
    const h=voteModule._test.makeHandler({env:{},getSql:()=>async()=>{queried=true;}});
    const out=res(); await h(req('POST',vote),out);
    assert.equal(out.statusCode,404); assert.equal(queried,false);
});

test('vote requires session auth before write', async () => {
    let queried=false;
    const h=voteModule._test.makeHandler({env:enabled,getSql:()=>async()=>{queried=true;},
        verifySession:async(_s,t,p)=>{assert.equal(t,'proof');assert.equal(p,vote.playerId);return {ok:false};}});
    const out=res(); await h(req('POST',vote),out);
    assert.equal(out.statusCode,401); assert.equal(queried,false);
});

test('vote query enforces window, eligible publication, no self vote and immutable choice', async () => {
    let sqlText='';
    const sql=async strings=>{sqlText=strings.join('?');return [{state:'cast',showcase_id:vote.showcaseId}];};
    const h=voteModule._test.makeHandler({env:enabled,getSql:()=>sql,verifySession:async()=>({ok:true})});
    const out=res(); await h(req('POST',vote),out);
    assert.equal(out.statusCode,200);
    assert.match(sqlText,/NOW\(\) >= c\.starts_at AND NOW\(\) < c\.voting_ends_at/);
    assert.match(sqlText,/cc\.eligible = TRUE AND sh\.published = TRUE/);
    assert.match(sqlText,/sh\.owner_wallet <>/);
    assert.match(sqlText,/ON CONFLICT \(contest_id, category_id, voter_wallet\) DO NOTHING/);
    assert.match(sqlText,/choice_locked/);
});

test('changed vote is refused while same vote is idempotent', async () => {
    for (const [state,code] of [['choice_locked',400],['already_cast',200]]) {
        const h=voteModule._test.makeHandler({env:enabled,getSql:()=>async()=>[{state}],verifySession:async()=>({ok:true})});
        const out=res(); await h(req('POST',vote),out); assert.equal(out.statusCode,code);
    }
});

test('rankings are category-scoped, hidden until voting closes, and expose no identity', async () => {
    const h=countsModule._test.makeHandler({env:enabled,getSql:()=>async()=>[
        {showcase_id:vote.showcaseId,votes:'12',owner_wallet:'must-not-project'}]});
    const out=res(); await h({method:'GET',query:{contestId:vote.contestId,categoryId:vote.categoryId}},out);
    assert.deepEqual(out.body.candidates,[{showcaseId:vote.showcaseId,votes:12}]);
    assert.doesNotMatch(JSON.stringify(out.body),/wallet|player|account/i);
});

test('discovery is authenticated, blinded and deterministically shuffled without vote counts', async () => {
    let text='';
    const h=discoverModule._test.makeHandler({env:enabled,verifySession:async()=>({ok:true}),
        getSql:()=>async strings=>{text=strings.join('?');return[{showcase_id:vote.showcaseId}];}});
    const out=res(); await h(req('POST',{playerId:vote.playerId,contestId:vote.contestId,categoryId:vote.categoryId}),out);
    assert.equal(out.statusCode,200);
    assert.deepEqual(out.body.candidates,[{showcaseId:vote.showcaseId}]);
    assert.match(text,/ORDER BY md5\(cc\.showcase_id/);
    assert.match(text,/sh\.owner_wallet <>/);
    assert.doesNotMatch(JSON.stringify(out.body),/votes|rank|wallet|player/i);
});

test('finalization requires both constant-time admin keys and accepts no winner or sku', async () => {
    const env={...enabled,ADMIN_DASH_KEY:'read-key',ADMIN_OPS_KEY:'write-key'};
    let queried=false;
    const h=finalizeModule._test.makeHandler({env,getSql:()=>async()=>{queried=true;return[];}});
    const bad=res(); await h({method:'POST',headers:{'x-admin-key':'wrong','x-admin-ops-key':'write-key'},body:{}},bad);
    assert.equal(bad.statusCode,400);assert.equal(queried,false);
    const arbitrary=res(); await h({method:'POST',headers:{'x-admin-key':'read-key','x-admin-ops-key':'write-key'},
        body:{contestId:vote.contestId,by:'ops',winner:vote.showcaseId,sku:'chosen'}},arbitrary);
    assert.equal(arbitrary.body.code,'BAD_BODY');assert.equal(queried,false);
});

test('operator finalization derives ranking and predefined cosmetic grants idempotently', async () => {
    let text='';
    const env={...enabled,ADMIN_DASH_KEY:'read-key',ADMIN_OPS_KEY:'write-key'};
    const h=finalizeModule._test.makeHandler({env,getSql:()=>async strings=>{text=strings.join('?');return[{grants:2,finalized:true}];}});
    const out=res(); await h({method:'POST',headers:{'x-admin-key':'read-key','x-admin-ops-key':'write-key'},
        body:{contestId:vote.contestId,by:'ops'}},out);
    assert.equal(out.statusCode,200);assert.equal(out.body.grantsCreated,2);
    assert.match(text,/PARTITION BY cc\.category_id/);
    assert.match(text,/ORDER BY COUNT\(v\.voter_wallet\) DESC, cc\.showcase_id ASC/);
    assert.match(text,/showcase_contest_result_runs/);
    assert.match(text,/showcase_contest_result_rows/);
    assert.match(text,/EXISTS \(SELECT 1 FROM showcase_contest_result_runs rr/);
    assert.match(text,/showcase_contest_category_reward_tiers/);
    assert.match(text,/ci\.item_kind = 'cosmetic' AND ci\.active = TRUE/);
    assert.match(text,/'community:' \|\|/);
    assert.match(text,/ON CONFLICT \(grant_id\) DO NOTHING/);
    assert.match(text,/expiryBehavior/);assert.match(text,/fallbackSku/);
});

test('result reversal is append-only, key-gated and idempotently revokes only category grants', async () => {
    let text='';
    const env={...enabled,ADMIN_DASH_KEY:'read-key',ADMIN_OPS_KEY:'write-key'};
    const h=reverseModule._test.makeHandler({env,getSql:()=>async strings=>{text=strings.join('?');
        return[{found:true,reversed:true,revoked:2}];}});
    const out=res(); await h({method:'POST',headers:{'x-admin-key':'read-key','x-admin-ops-key':'write-key'},
        body:{contestId:vote.contestId,categoryId:vote.categoryId,by:'ops',reason:'invalid finalist'}},out);
    assert.equal(out.statusCode,200); assert.equal(out.body.entitlementsRevoked,2);
    assert.match(text,/showcase_contest_result_reversals/);
    assert.match(text,/ON CONFLICT \(result_id\) DO NOTHING/);
    assert.match(text,/metadata->>'categoryId'/);
    assert.match(text,/state='active' AND EXISTS \(SELECT 1 FROM reversal\)/);
});

test('migration pins contest windows, immutable unique votes and contains no live rows', () => {
    const root=path.resolve(__dirname,'..');
    const migration=fs.readFileSync(path.join(root,'api/migrations/20260829_0010_showcase_contests_votes_rewards.sql'),'utf8');
    assert.match(migration,/CHECK \(voting_ends_at > starts_at\)/);
    assert.match(migration,/eligible BOOLEAN NOT NULL DEFAULT FALSE/);
    assert.match(migration,/PRIMARY KEY \(contest_id, voter_wallet\)/);
    assert.match(migration,/BEFORE UPDATE OR DELETE ON showcase_contest_votes/);
    assert.match(migration,/cosmetic_sku TEXT NOT NULL REFERENCES catalog_items/);
    assert.match(migration,/FOREIGN KEY \(showcase_id, snapshot_version\)/);
    assert.doesNotMatch(migration,/INSERT INTO\s+(showcase_contests|showcase_contest_candidates|showcase_contest_reward_tiers|sku_entitlements)/i);
});

test('category migration authors weights and preserves immutable vote/result/reversal audit without seed rows', () => {
    const root=path.resolve(__dirname,'..');
    const migration=fs.readFileSync(path.join(root,'api/migrations/20260829_0012_showcase_category_voting_audit.sql'),'utf8');
    assert.match(migration,/vote_weight NUMERIC\(8,4\)/);
    assert.match(migration,/authored_by TEXT NOT NULL/);
    assert.match(migration,/active BOOLEAN NOT NULL DEFAULT FALSE/);
    assert.match(migration,/PRIMARY KEY \(contest_id, category_id, voter_wallet\)/);
    assert.match(migration,/showcase_contest_result_reversals/);
    assert.match(migration,/showcase_result_rows_immutable/);
    assert.doesNotMatch(migration,/INSERT INTO\s+(showcase_contests|showcase_contest_categories|showcase_contest_category_candidates|showcase_contest_category_reward_tiers|sku_entitlements)/i);
});

test('foundation has no payment, promo, purchase, logging, or public wallet authority', () => {
    const root=path.resolve(__dirname,'..');
    const files=['api/showcase/vote.js','api/showcase/discover.js','api/showcase/vote-counts.js',
        'api/admin/showcase-finalize.js','api/admin/showcase-reverse.js'];
    const code=files.map(f=>fs.readFileSync(path.join(root,f),'utf8')).join('\n');
    assert.doesNotMatch(code,/console\.(log|warn|error)/);
    assert.doesNotMatch(code,/promo_codes|purchase_quotes|purchase_entitlements|payment/i);
    assert.doesNotMatch(fs.readFileSync(path.join(root,'api/showcase/vote-counts.js'),'utf8'),/wallet:/);
});
