from pathlib import Path
import hashlib,json
q=Path(__file__).resolve().parent;r=q.parents[1]
j=json.loads((q/'input-pins.json').read_text(encoding='utf-8-sig'))
for name,h in j.items():
 p=r/name
 assert p.is_file(),name+' missing required fixed input'
 assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==h,name+' changed reviewed input'
c=json.loads((q/'reviewed-contract.json').read_text(encoding='utf-8'))
for name,h in (c['sourcePins']|c['projectPins']).items():
 assert hashlib.sha256((r/'BotTraversal-0.9.0-BETA-SOURCE'/name).read_bytes()).hexdigest().upper()==h,name+' changed reviewed production input'
print('PASS',len(j),'fixed reviewed QA inputs; no automatic contract refresh')
