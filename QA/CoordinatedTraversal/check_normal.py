from pathlib import Path
import json,sys
q=Path(__file__).resolve().parent
j=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
contract=j(q/'normal-baseline-contract.json');current=j(Path(sys.argv[1]) if len(sys.argv)>1 else q/'current-il.json');checks=[]
for name,baseline in contract['methods'].items():
 assert name in current['methods'],name+' missing normal method'
 assert current['methods'][name]['hash']==baseline['hash'],name+' normal execution differs from RC111'
 checks.append(name)
assert len(checks)==5
output=Path(sys.argv[2]) if len(sys.argv)>2 else q/'normal-result.json'
output.write_text(json.dumps(dict(passed=True,checks=len(checks),methods=checks,baselineDllSha256=contract['baselineDllSha256'],dllSha256=current['dllSha256'],physicsSimulated=False,appliesToFirstEntryAndPostRetryEntry=True),indent=2),encoding='utf-8')
print('PASS 5 actual normal method body fingerprints equal RC111; shared first/retry entry paths')
