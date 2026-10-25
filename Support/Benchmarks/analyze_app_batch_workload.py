"""Compare matched real-app batch workload protocols; all scope CPU times are inclusive."""
import argparse,json,statistics
from pathlib import Path

def load(paths):
    return [json.loads(Path(p).read_text(encoding='utf-8-sig')) for p in paths]

def compare(before,after):
    reference=before[0]
    keys=('schemaVersion','harnessSha256','fixturePaths','fixtureSha256','unity','cpu','gpu','graphicsApi','sdkMode','batching','eviction','geometryShaders','screenWidth','screenHeight','qualityLevel','targetFrameRate','vSync','autosaveDisabled')
    shape=[(p['fixture'],p['name'],p['sample']) for p in reference['phases']]
    if len(shape)!=51: raise ValueError('Expected the complete 51-phase protocol')
    for group in (before,after):
        manifest=json.loads(group[0]['sourceManifest'])
        for run in group:
            if run['status']!='completed' or run.get('error'): raise ValueError(f"Failed run: {run['label']}")
            for key in keys:
                if run[key]!=reference[key]: raise ValueError(f'Protocol/environment mismatch: {key}')
            source=json.loads(run['sourceManifest'])
            if source['revision']!=manifest['revision'] or source['installed']!=manifest['installed']:
                raise ValueError('Source or instrumentation changed within group')
            if [(p['fixture'],p['name'],p['sample']) for p in run['phases']]!=shape: raise ValueError('Phase sequence mismatch')
            for p,r in zip(run['phases'],reference['phases']):
                if [c['name'] for c in p['counters']] != [c['name'] for c in r['counters']]: raise ValueError('Counter schema mismatch')
                for key in ('brushGuid','brushName','controlPoints','qualityLevel'):
                    if p[key]!=r[key]: raise ValueError(f'Workload input mismatch: {key}')
                for key in ('strokes','batchedStrokes','solitaryStrokes','notCreated','batches','vertices','indices'):
                    if p['census'][key]!=r['census'][key]: raise ValueError(f"Geometry mismatch: {p['fixture']}/{p['name']}/{key}")
    rows=[]
    groups=list(dict.fromkeys((p['fixture'],p['name']) for p in reference['phases']))
    for fixture,name in groups:
        samples=lambda runs: [p for run in runs for p in run['phases'] if p['fixture']==fixture and p['name']==name]
        b,a=samples(before),samples(after)
        row={'fixture':fixture,'phase':name,'samples_before':len(b),'samples_after':len(a),'census':{k:b[0]['census'][k] for k in ('strokes','batchedStrokes','solitaryStrokes','notCreated','batches','vertices','indices')},'counters':[]}
        for key in ('wallMs','directCallCpuMs','frameCount'):
            row[f'before_{key}']=statistics.median(p[key] for p in b)
            row[f'after_{key}']=statistics.median(p[key] for p in a)
            row[f'before_{key}_runs']=[statistics.median(p[key] for p in samples([run])) for run in before]
            row[f'after_{key}_runs']=[statistics.median(p[key] for p in samples([run])) for run in after]
        for key in ('residentBatches','cachedIndexBytes','cachedVertexBytes'):
            row[f'before_{key}']=statistics.median(p['census'][key] for p in b)
            row[f'after_{key}']=statistics.median(p['census'][key] for p in a)
        for i,c in enumerate(b[0]['counters']):
            item={'name':c['name']}
            for label,phases in [('before',b),('after',a)]:
                for key in ('calls','cpuMs','vertices','indices'):
                    item[f'{label}_{key}']=statistics.median(p['counters'][i][key] for p in phases)
                item[f'{label}_cpuMs_range']=[min(p['counters'][i]['cpuMs'] for p in phases),max(p['counters'][i]['cpuMs'] for p in phases)]
            row['counters'].append(item)
        rows.append(row)
    return {'before':[r['label'] for r in before],'after':[r['label'] for r in after],
            'before_revision':json.loads(before[0]['sourceManifest'])['revision'],'after_revision':json.loads(after[0]['sourceManifest'])['revision'],
            'environment':{k:reference[k] for k in keys if k not in ('harnessSha256','fixturePaths','fixtureSha256')},
            'scope':'Editor Play-mode monoscopic application workloads. Nested CPU scopes must not be added. Paced replay is not hand-input latency; wall times include frame pacing. No player/mobile/FPS claim.',
            'results':rows}

def main():
    ap=argparse.ArgumentParser(description=__doc__); ap.add_argument('--before',nargs='+',required=True); ap.add_argument('--after',nargs='+',required=True); ap.add_argument('--output',required=True); a=ap.parse_args()
    report=compare(load(a.before),load(a.after));Path(a.output).write_text(json.dumps(report,indent=2)+'\n')
    print(f"Validated {len(report['before'])} before and {len(report['after'])} after runs; {len(report['results'])} fixture/phase groups; geometry census matches.")
    for row in report['results']:
        if row['phase'] in ('quick_load','visibility_after_idle'):
            c=next(x for x in row['counters'] if x['name']==('Batch.FullUpload' if row['phase']=='quick_load' else 'Batch.TopologyUpload'))
            print(f"{Path(row['fixture']).name} {row['phase']}: scope {c['before_cpuMs']:.3f} -> {c['after_cpuMs']:.3f}ms; direct calls {row['before_directCallCpuMs']:.3f} -> {row['after_directCallCpuMs']:.3f}ms")

if __name__=='__main__':main()
