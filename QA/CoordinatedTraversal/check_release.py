from pathlib import Path
import json,hashlib,collections
q=Path(__file__).resolve().parent;r=q.parents[1];s=r/'BotTraversal-0.9.0-BETA-SOURCE'
j=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
c=j(q/'reviewed-contract.json');a=j(q/'baseline-il.json');b=j(q/'current-il.json');checks=[]
def check(v,n):
 assert v,n
 checks.append(n)
check(a['dllSha256']==c['baselineDllSha256']==sha(q/'baseline-RC120.dll'),'exact actual RC120 baseline')
check(b['dllSha256']==sha(s/'bin/Release/net472/BotTraversal.dll'),'actual candidate fingerprint')
check(sha(q/'reviewed-production.diff')==c['patchSha256'],'reviewed production diff immutable')
files={p.relative_to(s).as_posix():sha(p) for p in (s/'src').rglob('*.cs')}
check(files==c['sourcePins'] and len(files)==109,'all 109 reviewed production files, no unreviewed source')
check(sorted(k for k in c['baselineSourcePins'].keys()|files.keys() if c['baselineSourcePins'].get(k)!=files.get(k))==c['reviewedChangedFiles'],'exact 18 reviewed source differences')
for k,h in c['projectPins'].items():check(sha(s/k)==h,'project reference '+k)
for k,h in c['evidencePins'].items():check(sha(q/'Evidence'/k)==h,'frozen supplied evidence '+k)
changed={k:b['methods'][k] for k in a['methods'].keys()&b['methods'].keys() if a['methods'][k]['hash']!=b['methods'][k]['hash']}
check(changed==c['changedMethods'] and len(changed)==33,'only 33 reviewed existing compiled bodies changed')
check({k:b['methods'][k] for k in b['methods'].keys()-a['methods'].keys()}==c['addedMethods'],'only reviewed new compiled bodies')
check(sorted(a['methods'].keys()-b['methods'].keys())==c['removedMethods'],'only reviewed removed push methods/generated closures')
for k in a['methods'].keys()&b['methods'].keys()-changed.keys():check(a['methods'][k]==b['methods'][k],'unchanged compiled method '+k)
check({k:b['fields'][k] for k in b['fields'].keys()-a['fields'].keys()}==c['addedFields'],'only reviewed fields')
check(sorted(a['fields'].keys()-b['fields'].keys())==c['removedFields'],'only reviewed removed config/push/closure fields')
check({k:b['fields'][k] for k in a['fields'].keys()&b['fields'].keys() if a['fields'][k]!=b['fields'][k]}==c['changedFields']=={},'retained field attributes/constants unchanged')
check(sorted(a['catalog'].keys()-b['catalog'].keys())==sorted(c['retiredCatalog']),'only three obsolete friendly push/reset sites retired')
check({k:b['catalog'][k] for k in b['catalog'].keys()-a['catalog'].keys()}==c['newCatalog'],'only three issued-scope queue hold sites added')
check(all(a['catalog'][k]==b['catalog'][k] for k in a['catalog'].keys()&b['catalog'].keys()),'all retained catalog operations/modes unchanged')
check(collections.Counter(x['mode'] for x in b['catalog'].values())=={0:489,1:404,2:29},'scope footprint frozen, no compatibility expansion')
scan=j(q/'semantic-scan.json');scopes=j(q/'scope-classification.json');norm=lambda x:{k:v for k,v in x.items() if k!='line'};key=lambda x:json.dumps(norm(x),sort_keys=True)
check(not scan['policyErrors'] and not scan['dynamicSites'],'engine-free policies and no dynamic movement writer')
check(collections.Counter(map(key,scan['direct']))==collections.Counter(map(key,c['oldNativeCalls'])),'all existing native backend signatures/argument shapes unchanged')
check(collections.Counter(map(key,scan['writes']))==collections.Counter(map(key,c['oldResidualAssignments']))-collections.Counter(map(key,c['removedLocalAssignments']))+collections.Counter(map(key,c['addedLocalAssignments'])),'only three removed push local vectors and one new local template vector assignment')
check(len(scan['routed'])==922 and len(b['catalog'])==922,'each live catalog site has source effect')
check(scopes['counts']=={'strict-existing':191,'strict-inherited':298,'retained-compatibility':404,'retained-infrastructure':29},'all inherited ownership proofs preserved')
for x in scopes['sites']:
 if x['site'].startswith('R121-'):check(x['method'] in scopes['provenMethods'],'actual closed issued-scope proof for queue '+x['site'])
names=['regression-results.json','continuity-result.json','route-retry-result.json','recovery-result.json','failure-only-result.json','route-recovery-result.json','ingress-contract-result.json','passage-admission-result.json','motion-evidence-result.json','jump-boundary-result.json','rescue-disposition-result.json','coordination-result.json','closeout-il.json','recovery-il.json','consumer-il.json','route-il.json','continuity-il.json','route-retry-il.json','recovery-il-new.json','failure-only-il.json','route-recovery-il.json','ingress-contract-il.json','passage-admission-il.json','motion-evidence-il.json','jump-boundary-il.json','rescue-disposition-il.json','coordination-il.json','native-avoidance-transform.json','normal-result.json','compatibility-result.json','oracle-result.json']
for n in names:
 d=j(q/n)
 if isinstance(d,list):check(len(d)==21 and all(x['passed'] for x in d),'all 21 original behavior suites passed')
 else:
  check(d['passed'],'passing candidate '+n)
  if 'dllSha256' in d:check(d['dllSha256']==b['dllSha256'],'candidate hash bound '+n)
d=j(q/'native-avoidance-transform.json');check(d['nativeGameSha256']=='FAEF6F0B9F142F9D047495EC3DCCFD5D6974AC048368DC7045955CF54B117982' and not d['nativeCodeExecuted'] and not d['gameLaunched'],'actual reviewed native IL transformed without invoking game methods')
negative=j(q/'negative-RC120.json');check(not negative['passed'] and any('ladder precedes return transaction' in e for e in negative['errors']) and any('legacy compatibility forced true' in e for e in negative['errors']),'actual old RC120 rejected by new sequence/config integration')
normal=j(q/'normal-baseline-contract.json');check(len(normal['methods'])==5 and all(b['methods'][k]['hash']==v['hash'] for k,v in normal['methods'].items()),'five unaffected original normal entry/control bodies remain equal RC111')
(q/'review-result.json').write_text(json.dumps(dict(passed=True,checks=len(checks),changedMethods=len(changed),sourceFiles=len(files),dllSha256=b['dllSha256'],physicsSimulated=False),indent=2),encoding='utf-8')
print('PASS RC121 reviewed source/binary/ownership/native effects',len(checks),'checks')
