# Quest bloom benchmarks

One APK contains encoded mobile bloom, optional alternate-eye reuse, rotation
reprojection, native URP comparison profiles, and runtime diagnostics.

## Run

1. Save any current sketch before starting: the runner restarts the app.
   Install the CI APK, connect a Quest with USB debugging enabled, enable the
   Open Brush HTTP API, and keep the headset awake and rendering. Complete any
   welcome dialogs so the drawing scene is ready.
2. Use Python 3 and Android SDK adb. Pass --adb with its full path if necessary.
   Specify the APK's actual package ID; development flavors may differ.
3. Preview:
   ~~~powershell
   python Support/Python/run-bloom-benchmarks.py --dry-run
   ~~~
4. Short initial run:
   ~~~powershell
   python Support/Python/run-bloom-benchmarks.py --serial DEVICE_SERIAL --package org.icosa.openbrush --profiles encoded-off encoded-both encoded-alternate encoded-reproject --fixtures sparse --repeats 1 --warmup 5 --duration 10
   ~~~
5. Full randomized series:
   ~~~powershell
   python Support/Python/run-bloom-benchmarks.py --serial DEVICE_SERIAL --package org.icosa.openbrush --apk PATH_TO_CI_APK --output bloom-quest-results
   ~~~
6. Pyramid sweep:
   ~~~powershell
   python Support/Python/run-bloom-benchmarks.py --serial DEVICE_SERIAL --profiles encoded-both encoded-alternate encoded-reproject --fixtures sparse dense --levels 2 3 4 --downsample 2 4 --repeats 3
   ~~~

Defaults: 20s warmup, 30s sampling, 15s cooldown, three repeats, sparse/dense/white
fixtures, recorded randomized ordering. The output directory must be new.
The same installed app restarts for startup changes; no rebuild is needed.

## Profiles

| Profile | Buffers | Bloom |
| --- | --- | --- |
| off-ldr | LDR | Disabled; ordinary brush rendering |
| encoded-off | LDR | Disabled; encoded brush rendering |
| encoded-both | LDR | Encoded extraction/blur for both eyes |
| encoded-alternate | LDR | One-eye updates, other-eye reuse |
| encoded-reproject | LDR | Alternate updates with rotation reprojection |
| native-ldr | LDR | Native URP |
| off-hdr32 / off-hdr64 | Requested HDR precision | Disabled |
| native-hdr32 / native-hdr64 | Requested HDR precision | Native URP |
| default | Normal settings | Removes benchmark startup override |

Compare encoded modes with encoded-off; native modes with their matching off
baseline. HDR precision is a request: Unity's alpha requirements may change the
format. Reports record actual targets, dimensions, slices, MSAA, viewport/pipeline
scale, shader LOD and refresh rate. Quality is frozen during sampling.

Native defaults: two iterations, quarter resolution, low-quality filtering,
scatter 0.35; LDR threshold/intensity 0.5/1, HDR 1.05/0.1. These are starting points,
not matched visual quality. Tune and compare screenshots.

Alternate reuse requires two-slice single-pass XR. Other layouts fall back to both
eyes and are flagged. Stereo draws remain; unchanged-eye fragment work is discarded.
Both histories refresh after gaps, pyramid/threshold changes and large rotations.
Reprojection handles rotation only; translation and moving objects can show stale
glow. Normal app rendering uses both-eye updates. No URP package fork is involved.
The old desktop bloom is not ported and cannot be measured in this series.

Fixtures use shipping materials independently of m_TestingMaterial and deterministic
camera-relative quads on unused layer 31. White uses Flat, other fixtures use Light.
Scene uses the currently loaded sketch; configure a startup sketch for repeatability.
Stop restores camera masks, clear settings and quality. No sketches are saved.

## Runtime HTTP API

Commands enqueue asynchronously. Poll status until token matches; check error.
Queries read published snapshots without Unity access from the HTTP worker thread.

1. bloom.benchmark.configure=encoded-both,4,1,config1 persists profile, MSAA,
   eye-buffer scale and token. Restart to activate. Preferences use a dedicated
   PlayerPrefs key; the Open Brush config is untouched.
2. bloom.benchmark.tune=3,2,1,0,2,true,false,0.35,1,tune1 sets encoded levels,
   downsample divisor, amount, threshold, native iterations, quarter flag,
   high-quality flag, scatter, native intensity and token. Live while idle.
3. bloom.benchmark.start=run1,20,30,sparse,0 starts token, warmup seconds,
   sampling seconds, fixture (scene/sparse/dense/white), quality level.
4. query.bloom.benchmark.status returns readiness, active/pending settings, token,
   error, state, actual backend/bloom settings, targets and diagnostics.
5. query.bloom.benchmark.result returns the completed report with raw samples.
6. bloom.benchmark.stop=stop1 stops and restores scene/quality. Completed fixtures
   remain visible until stopped so screenshots can be captured.
7. bloom.benchmark.configure=default,4,1,restore then restart clears overrides;
   MSAA/scale arguments are ignored for default.
8. Existing bloom.amount and bloom.threshold remain usable manually; use
   benchmark.tune for captures to record the requested tuning.

Normal Editor Play mode retains the temporary LDR tuning override; selecting an
HDR benchmark profile overrides it.

## Reports and traces

Each run saves raw JSON, thermal snapshots, logcat, screenshot and validation
issues. With optional Pillow installed, synthetic-fixture screenshots also get\na stereo halo-balance check to flag missing-eye glow. Summary CSV contains GPU mean/median/p95, wall-frame p95, GPU coverage,
refresh rate and dropped-frame delta. XR GPU timing may be unavailable: null,
never fake zero. Unity Frame Timing statistics are enabled, with fallback CPU/GPU
samples retained separately. Timing samples may repeat; wall time includes pacing.

Reports also save under the app's persistent BloomBenchmarks directory. The runner
restores the original startup profile and removes only its own ADB forward.
Restoration failures write RESTORE-FAILED.txt with recovery steps. It never clears
app data or rewrites user config.

Optional Meta VR CLI Perfetto capture accepts an argument array with app, serial,
duration (milliseconds), and output placeholders:

~~~powershell
python Support/Python/run-bloom-benchmarks.py --serial DEVICE_SERIAL --profiles encoded-both --fixtures dense --repeats 1 --trace-command '["npx","-y","metavr","perf","capture","--device","{serial}","--mode","gpu","--app","{app}","--duration","{duration}","--output","{output}"]'
~~~

Trace output is saved per run. Compare traced cases with traced cases, and retain
an untraced primary series. Keep headset wear, movement, charging/battery state
and thermal conditions consistent.
