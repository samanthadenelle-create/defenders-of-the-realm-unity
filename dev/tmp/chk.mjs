const RPC="https://api.mainnet-beta.solana.com";
async function info(pk,label){
  const r=await fetch(RPC,{method:"POST",headers:{"content-type":"application/json"},
    body:JSON.stringify({jsonrpc:"2.0",id:1,method:"getAccountInfo",params:[pk,{encoding:"jsonParsed"}]})});
  const j=await r.json(); const v=j?.result?.value;
  console.log(`\n=== ${label}\n    ${pk}`);
  if(!v){console.log("    DOES NOT EXIST on mainnet");return null;}
  console.log("    owner program :",v.owner);
  const p=v.data?.parsed;
  if(p?.info){
    if(p.type==="mint") console.log("    type=mint  decimals:",p.info.decimals,"  supply:",p.info.supply);
    if(p.type==="account"){console.log("    type=tokenAccount  mint:",p.info.mint);
      console.log("    authority     :",p.info.owner);
      console.log("    balance       :",p.info.tokenAmount?.uiAmountString,"(",p.info.tokenAmount?.amount,"base units )");}
  }
  return v;
}
await info("SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3","OFFICIAL MAINNET SKR MINT");
await info("2VePaneS3xX2EdzSbe4JdiovRffboLJV4yNVmVTkeuCg","TREASURY OWNER (supplied)");
await info("ApxAy5uqivjcfxd1E5XDtubY7b4SACfTPAKfuSdVrpAy","ATA - classic TOKEN_PROGRAM");
await info("6kztXyXc3FxruXejAP7cqTrnHjpaybPgTMxCJZ78QEhv","ATA - TOKEN_2022");
