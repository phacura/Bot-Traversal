from pathlib import Path
from collections import defaultdict,Counter
import json
q=Path(__file__).resolve().parent;r=q.parents[1];j=lambda p:json.loads(p.read_text(encoding='utf-8-sig'));s=j(Path(__import__('sys').argv[1]) if len(__import__('sys').argv)>1 else q/'semantic-scan.json');nodes={x['key']:x for x in s['methodNodes']};incoming=defaultdict(list)
for row in s['methodCalls']:incoming[row['target']].append(row)
groups=set(s['methodGroups']);proven=set();levels={}
for depth in range(len(nodes)):
 added={key for key,node in nodes.items() if node['eligible'] and not node['attributed'] and not node['namedRoot'] and key not in groups and incoming[key] and key not in proven and all(call['scoped'] or (call['caller'] in proven and not call['callback']) for call in incoming[key])}
 if not added:break
 proven|=added
 for key in added:levels[key]=depth
input_directory=Path(__import__('sys').argv[1]).resolve().parent if len(__import__('sys').argv)>1 else q
base=j(q/'baseline-il.json');current=j(input_directory/'current-il.json');base['catalog'].update({k:v for k,v in current['catalog'].items() if k.startswith(('R112-','R113-','R116-','R121-'))});sites={row['arguments'][0].strip('"'):row for row in s['routed']};promoted=sorted(site for site,row in sites.items() if base['catalog'][site]['mode']==1 and row['enclosing'] in proven)
def reasons(key):
 node=nodes.get(key)
 if not node:return ['accessor, constructor or nonordinary entry requires separate contract']
 result=[]
 if not node['eligible']:result.append('externally visible or overridable entry')
 if node['attributed']:result.append('attributed framework entry')
 if node['namedRoot']:result.append('named or conventional runtime callback')
 if key in groups:result.append('method group or late-bound reference')
 if not incoming[key]:result.append('no closed source caller set')
 if not result:result.append('mixed or cyclic caller contexts do not establish mandatory scope')
 return result
rows=[]
for site,row in sorted(sites.items()):
 mode=base['catalog'][site]['mode'];status='strict-existing' if mode==0 else 'strict-inherited' if site in promoted else 'retained-infrastructure' if mode==2 else 'retained-compatibility'
 rows.append(dict(site=site,status=status,file=row['file'],method=row['enclosing'],backend=row['backend'],reason=['closed synchronous source caller chain, rank '+str(levels[row['enclosing']])] if status=='strict-inherited' else reasons(row['enclosing']) if mode in [1,2] else ['existing exact-scope contract'],callerContexts=incoming[row['enclosing']]))
result=dict(promotedSites=promoted,provenMethods=sorted(proven),levels=levels,proofs={key:incoming[key] for key in proven},interfaceBindings=s['interfaceBindings'],sites=rows,counts=dict(Counter(x['status'] for x in rows)),retainedMethodGroups=len({x['method'] for x in rows if x['status'].startswith('retained')}))
(Path(__import__('sys').argv[2]) if len(__import__('sys').argv)>2 else q/'scope-classification.json').write_text(json.dumps(result,indent=2));print('Scope classification',result['counts'],'retained method groups',result['retainedMethodGroups'])

