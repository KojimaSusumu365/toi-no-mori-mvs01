from __future__ import annotations
import csv, hashlib, json, re, subprocess, urllib.request, unicodedata
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone

OUT=Path('work/out'); OUT.mkdir(parents=True,exist_ok=True)
ACQ=datetime.now(timezone.utc).isoformat().replace('+00:00','Z')
TARGET=650
DOMAINS=('PUBLIC','CORPORATE','GENERAL_TECH')

def canon(s):
    s=unicodedata.normalize('NFKC',s or '')
    s=s.replace('\r\n','\n').replace('\r','\n')
    s=re.sub(r'[ \t\u3000]+',' ',s)
    s=re.sub(r'\n{3,}','\n\n',s)
    return s.strip()
def sha(s): return hashlib.sha256(canon(s).encode()).hexdigest()
def ja_chars(s): return len(re.findall(r'[ぁ-んァ-ヶ一-龠々〆ヵヶ]',s))
def sentenceish(s): return len(re.findall(r'[。！？!?]',s))
def usable(s):
    s=canon(s); return len(s)>=350 and ja_chars(s)>=120 and sentenceish(s)>=4
def simhash(s):
    toks=re.findall(r'[ぁ-んァ-ヶ一-龠々〆ヵヶA-Za-z0-9]{2,}',canon(s))
    if not toks:return 0
    v=[0]*64
    for t in toks:
        h=int(hashlib.sha256(t.encode()).hexdigest()[:16],16)
        for i in range(64):v[i]+=1 if (h>>i)&1 else -1
    return sum((1<<i) for i,x in enumerate(v) if x>=0)
def hd(a,b):return (a^b).bit_count()

rows=[]; audit=[]; seen_sha={}; sims=[]
def count(domain):return sum(r['domain']==domain for r in rows)
def add(domain,did,text,uri):
    text=canon(text)
    if not usable(text):
        audit.append({'document_id':did,'domain':domain,'status':'REJECT_SHORT_OR_NONJP'});return False
    h=sha(text)
    if h in seen_sha:
        audit.append({'document_id':did,'domain':domain,'status':'REJECT_EXACT_DUP','duplicate_of':seen_sha[h]});return False
    sh=simhash(text)
    for odom,odid,osh in sims:
        if odom==domain and hd(sh,osh)<=2:
            audit.append({'document_id':did,'domain':domain,'status':'REJECT_NEAR_DUP_SIMHASH','duplicate_of':odid});return False
    seen_sha[h]=did;sims.append((domain,did,sh))
    rows.append({'document_id':did,'domain':domain,'text':text,'source_uri':uri,'acquired_at':ACQ,'source_sha256':h});return True

# PUBLIC — Japanese central/local government FAQ corpus; no labels are retained.
pub_url='https://huggingface.co/datasets/matsuxr/JaGovFaqs-22k/resolve/main/data.jsonl?download=true'
pub_path=Path('work/jagov.jsonl');pub_path.parent.mkdir(parents=True,exist_ok=True)
urllib.request.urlretrieve(pub_url,pub_path)
with pub_path.open(encoding='utf-8') as f:
    for idx,line in enumerate(f):
        if count('PUBLIC')>=TARGET:break
        try:o=json.loads(line)
        except Exception:continue
        lower={str(k).lower():v for k,v in o.items()}
        q=next((lower[k] for k in lower if k in ('question','query','prompt','q')), '')
        a=next((lower[k] for k in lower if k in ('answer','response','completion','a')), '')
        if not isinstance(q,str):q=str(q)
        if not isinstance(a,str):a=str(a)
        src=lower.get('url') or lower.get('source_url') or lower.get('source_uri') or ''
        uri=str(src) if src else f'{pub_url}#row={idx}'
        add('PUBLIC',f'PUB-JAGOV-{idx:06d}',(q+'。\n'+a).strip(),uri)

# CORPORATE — EDINET-derived securities reports, 2023 BusinessRisks only.
subprocess.run(['git','clone','--depth','1','https://github.com/yuukimiyo/stdata-jp.git','work/stdata'],check=True,stdout=subprocess.DEVNULL)
corp_files=sorted(Path('work/stdata').glob('*/*/2023/BusinessRisks'),key=lambda p:hashlib.sha256(str(p).encode()).hexdigest())
for p in corp_files:
    if count('CORPORATE')>=TARGET:break
    text=p.read_text(encoding='utf-8',errors='ignore');rel=p.relative_to('work/stdata').as_posix();code=rel.split('/')[1]
    add('CORPORATE',f'CORP-EDINET-{code}-2023-BR',text,f'https://github.com/yuukimiyo/stdata-jp/blob/main/{rel}')

# GENERAL_TECH — Japanese MDN Web Docs; markup/code removed mechanically, no semantic filtering.
subprocess.run(['git','clone','--depth','1','https://github.com/mdn/translated-content.git','work/mdn'],check=True,stdout=subprocess.DEVNULL)
tech_files=sorted(Path('work/mdn/files/ja/web').rglob('index.md'),key=lambda p:hashlib.sha256(str(p).encode()).hexdigest())
front=re.compile(r'^---\n.*?\n---\n',re.S)
for p in tech_files:
    if count('GENERAL_TECH')>=TARGET:break
    text=p.read_text(encoding='utf-8',errors='ignore');text=front.sub('',text)
    text=re.sub(r'```.*?```',' ',text,flags=re.S);text=re.sub(r'<[^>]+>',' ',text)
    text=re.sub(r'\{\{[^}]+\}\}',' ',text);text=re.sub(r'\[([^\]]+)\]\([^\)]+\)',r'\1',text)
    text=re.sub(r'^[#>*-]+\s*','',text,flags=re.M)
    rel=p.relative_to('work/mdn').as_posix()
    add('GENERAL_TECH','TECH-MDN-'+hashlib.sha256(rel.encode()).hexdigest()[:16],text,f'https://github.com/mdn/translated-content/blob/main/{rel}')

counts=Counter(r['domain'] for r in rows)
status='PASS_SOURCE_POOL_OVERSUPPLIED' if all(counts[d]>=TARGET for d in DOMAINS) else 'BLOCKED_INSUFFICIENT_SOURCE_DOCUMENTS'
rows.sort(key=lambda r:(r['domain'],hashlib.sha256(r['document_id'].encode()).hexdigest(),r['document_id']))
tsv=OUT/'FRESH_SOURCE_POOL.tsv'
with tsv.open('w',encoding='utf-8',newline='') as f:
    w=csv.DictWriter(f,fieldnames=['document_id','domain','text','source_uri','acquired_at','source_sha256'],delimiter='\t',lineterminator='\n');w.writeheader();w.writerows(rows)
(OUT/'SOURCE_POOL_DUPLICATE_AUDIT.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
manifest={'stage':'IF-0R4 three-domain independent Fresh source-pool construction','status':status,'gold_accessed':False,'model_scores_used':False,'candidate_marker_filtering_used':False,'counts':dict(counts),'target_oversupply_per_domain':TARGET,'total_documents':len(rows),'source_pool_sha256':hashlib.sha256(tsv.read_bytes()).hexdigest(),'sources':{'PUBLIC':{'name':'matsuxr/JaGovFaqs-22k','uri':pub_url},'CORPORATE':{'name':'yuukimiyo/stdata-jp / 2023 BusinessRisks','uri':'https://github.com/yuukimiyo/stdata-jp'},'GENERAL_TECH':{'name':'MDN translated-content Japanese Web docs','uri':'https://github.com/mdn/translated-content'}},'near_duplicate_rule':'exact canonical SHA-256 + same-domain 64-bit token SimHash Hamming <= 2','construction_time_utc':ACQ}
(OUT/'SOURCE_POOL_BUILD_STATUS.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'FRESH_SOURCE_POOL_INDEX_NO_TEXT.tsv').open('w',encoding='utf-8',newline='') as f:
    w=csv.writer(f,delimiter='\t',lineterminator='\n');w.writerow(['document_id','domain','source_uri','acquired_at','source_sha256'])
    for r in rows:w.writerow([r[k] for k in ['document_id','domain','source_uri','acquired_at','source_sha256']])
(OUT/'RAW_TEXT_NOT_FOR_REDISTRIBUTION.md').write_text('Raw FRESH_SOURCE_POOL.tsv is an ephemeral execution artifact. Portable distributions should carry the no-text provenance index and reconstruction code, not bulk third-party source text.\n',encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False,indent=2))
if status!='PASS_SOURCE_POOL_OVERSUPPLIED':raise SystemExit(3)
