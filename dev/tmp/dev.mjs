const r=await fetch("https://api.devnet.solana.com",{method:"POST",headers:{"content-type":"application/json"},
 body:JSON.stringify({jsonrpc:"2.0",id:1,method:"getAccountInfo",
 params:["3BwWSAUZmyngXDSZiCawEnP7iLgY5ANNopBDz94AB77N",{encoding:"jsonParsed"}]})});
const v=(await r.json())?.result?.value;
console.log("DEVNET test SKR mint 3BwWSA...B77N");
console.log("  decimals:", v?.data?.parsed?.info?.decimals, " supply:", v?.data?.parsed?.info?.supply);
console.log("  owner program:", v?.owner);
