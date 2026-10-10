const box=document.querySelector('#status');
const token=location.hash.slice(1);
async function load(){
 try{
  const res=await fetch('/api/guest/status',{headers:{Authorization:`Bearer ${token}`},credentials:'omit',cache:'no-store'});
  if(!res.ok)throw new Error();const s=await res.json();box.replaceChildren();
  const heading=document.createElement('h2');heading.textContent=s.state==='Pending'?'Request received — awaiting approval':s.state==='Accepted'?'Reservation confirmed':s.state;box.append(heading);
  for(const text of [`${s.property} · ${s.unit}`,`${s.arrival} → ${s.departure} · ${s.guests} guest(s)`,s.state==='Pending'?'Your dates are not held. Decision deadline: '+new Date(s.expires).toLocaleString():'',s.amount!==null?`Agreed total: ${s.amount} ${s.currency}`:'',s.reason,`Property contact: ${s.contact}`]){if(text){const p=document.createElement('p');p.textContent=text;box.append(p);}}
 }catch{box.textContent='This private link is unavailable or expired. Contact the property, or try again shortly.';}
}
document.querySelector('#copy').addEventListener('click',async()=>{try{await navigator.clipboard.writeText(location.href);document.querySelector('#copy-result').textContent='Private link copied. Save it somewhere safe.';}catch{document.querySelector('#copy-result').textContent='Copy the address from your browser and save it somewhere safe.';}});
load();
