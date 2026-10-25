"""Install temporary Editor-only timing scopes in a clean source revision; restore owned files exactly."""
import argparse, hashlib, json, re, subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
FILES=['Assets/Scripts/Batching/Batch.cs','Assets/Scripts/Batching/BatchPool.cs','Assets/Scripts/Brushes/GeometryPool.cs','Assets/Scripts/Brushes/GeometryBrush.cs','Assets/Scripts/PointerScript.cs','Assets/Editor/Tests/TestBatchVisibility.cs','Assets/Editor/Tests/TestGeometryPool.cs']
STATE=ROOT/'Logs/BatchWorkloadSession'

def digest(data): return hashlib.sha256(data).hexdigest()

def wrap(source, pattern, expression, expected=1):
    matches=list(re.finditer(pattern, source))
    if len(matches)!=expected: raise ValueError(f'Expected {expected} matches, found {len(matches)}: {pattern}')
    for match in reversed(matches):
        start=source.index('{',match.start())
        depth=0; end=None
        tokens=re.finditer(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\x27(?:\\.|[^\x27\\])*\x27|[{}]',source[start:])
        for token in tokens:
            if token.group()=='{': depth+=1
            elif token.group()=='}':
                depth-=1
                if depth==0: end=start+token.start(); break
        if end is None: raise ValueError('Unclosed method')
        source=source[:start+1]+f'\n#if UNITY_EDITOR\nusing (global::TiltBrush.Benchmarks.BatchWorkloadCounters.Measure({expression}))\n#endif\n{{'+source[start+1:end]+'}\n'+source[end:]
    return source

def instrument(path,source):
    if path.endswith('/Batch.cs'):
        source=wrap(source,r'if \(m_bVertexDataDirty\)\s*\{','0, m_Geometry.NumVerts, m_Geometry.NumTriIndices')
        source=wrap(source,r'else if \(m_bTopologyDirty\)\s*\{','1, 0, m_Geometry.NumTriIndices')
        source=wrap(source,r'public BatchSubset AddSubset\(\s*(?:int nVert|GeometryPool geom,)','10',2)
        source+='\n'+(ROOT/'Support/Benchmarks/BatchWorkloadCounters.cs.txt').read_text(encoding='utf-8-sig')
    elif path.endswith('/GeometryPool.cs'):
        source=wrap(source,r'public void CopyToMesh\(Mesh mesh\)','3, NumVerts, NumTriIndices')
        source=wrap(source,r'private void ReloadFromMesh\(\)','4, NumVerts, NumTriIndices')
        source=wrap(source,r'private void GenerateTriangleIds\(Mesh mesh\)','6')
        # Subset wrapper has three arguments before phase 1, five afterwards.
        source=wrap(source,r'public void CopyToMesh\(Mesh mesh, int iVert','9')
        marker='m_BackingMesh.GetTriangles(indices, 0);'
        if marker in source:
            source=source.replace(marker,'#if UNITY_EDITOR\nusing (global::TiltBrush.Benchmarks.BatchWorkloadCounters.Measure(5, 0, NumTriIndices))\n#endif\n{ '+marker+' }')
    elif path.endswith('/GeometryBrush.cs'):
        source=wrap(source,r'override public void ApplyChangesToVisuals\(\)','2')
    elif path.endswith('/PointerScript.cs'):
        source=wrap(source,r'public void UpdateLineFromControlPoint\(','7')
        source=wrap(source,r'public void RecreateLineFromMemory\(','8')
        source=wrap(source,r'public void UpdateLineVisuals\(','11')
        source=wrap(source,r'public void UpdateLineFromStroke\(','12')
    return source

def main():
    ap=argparse.ArgumentParser(description=__doc__); ap.add_argument('--revision'); ap.add_argument('--restore',action='store_true'); args=ap.parse_args()
    state_path=STATE/'state.json'
    state=json.loads(state_path.read_text()) if state_path.exists() else None
    if state:
        for path,hash_value in state['installed'].items():
            if digest((ROOT/path).read_bytes())!=hash_value: raise RuntimeError(f'File changed outside benchmark: {path}')
    if args.restore:
        if not state: raise RuntimeError('No owned instrumentation session')
        for path,backup in state['backups'].items(): (ROOT/path).write_bytes((STATE/backup).read_bytes())
        state_path.replace(STATE/'restored-state.json')
        print('Restored all seven original source/test files byte-for-byte.'); return
    if not args.revision: ap.error('--revision is required unless restoring')
    revision=subprocess.check_output(['git','rev-parse',args.revision],cwd=ROOT,text=True).strip()
    if state is None:
        subprocess.run(['git','diff','--exit-code','HEAD','--',*FILES],cwd=ROOT,stdout=subprocess.DEVNULL,check=True)
        STATE.mkdir(parents=True,exist_ok=True)
        state={'backups':{},'installed':{}}
        for i,path in enumerate(FILES):
            backup=f'{i}.original'; (STATE/backup).write_bytes((ROOT/path).read_bytes()); state['backups'][path]=backup
    pending={}; source_hashes={}
    for path in FILES:
        result=subprocess.run(['git','show',f'{revision}:{path}'],cwd=ROOT,capture_output=True)
        if result.returncode and path!='Assets/Editor/Tests/TestBatchVisibility.cs': raise RuntimeError(f'Missing baseline file: {path}')
        data=result.stdout if not result.returncode else b'// This test class does not exist in the selected baseline.\n'
        source_hashes[path]=digest(data)
        pending[path]=instrument(path,data.decode('utf-8-sig')).encode('utf-8')
    for path,data in pending.items(): (ROOT/path).write_bytes(data)
    state.update(revision=revision,sourceHashes=source_hashes,installed={p:digest(d) for p,d in pending.items()})
    state_path.write_text(json.dumps(state,indent=2)+'\n')
    print(f'Installed owned Editor-only workload scopes on {revision[:9]}. Use --restore after measurement.')

if __name__=='__main__': main()
