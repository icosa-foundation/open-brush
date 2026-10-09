# Isolated Android direct-storage feasibility probe

This source template builds a separate ARM64 IL2CPP application, targeting SDK 35,
with package ID `foundation.icosa.obdsunityprobe20261009`. It does not build or
replace Open Brush. It is a prerequisite experiment, not production storage support.

1. From this worktree, run `Support/AndroidDirectStorageProbe/build.ps1` through
   host PowerShell. An agent must use `require_escalated` because this launches
   Unity. The script copies sources to the untracked
   `LocalPlanning/AndroidDirectStorage/UnityProbe` project and records its build log
   there. No existing Editor is controlled. Generated assets/settings stay local.
   If compilation succeeds but Gradle fails because that Editor's SDK lacks API
   35, use `package.ps1 -SdkRoot <configured-sdk>` to package the generated player.
   The SDK needs API 35 and build tools 36.0.0. This sets only the generated
   project's `local.properties`; it does not change shared Unity preferences.
   Alternatively, pass `build.ps1 -PackagingSdk <configured-sdk>` for that retry
   automatically. An existing local `LocalPlanning/AndroidDirectStorage/AndroidSdk`
   is detected. The retry is limited to failures that reached Gradle, so a C#
   compilation failure cannot silently package an older generated project.
2. Inspect the actual APK manifest with Android SDK `apkanalyzer manifest print`.
   Confirm target 35, the unique package, `requestLegacyExternalStorage=false`,
   and absence of all-files and broad storage/media permissions. Record APK SHA256.
3. Install only if this unique package is absent. Do not replace an installed app
   without authorization. Launch `foundation.icosa.obds.ProbeActivity`. Initial
   System.IO checks create only unique fixtures under
   `Documents/Open Brush/OBDS_UnityProbe20261009`. The append-only report is
   `obds-results.txt` under the application's app-specific external files directory.
   Retrieve it with `collect.ps1 -Serial <adb-serial>`.
4. Restart the process and collect its report again. Previous marker access is
   reported separately. Reinstall/ownership-cleanup testing is a different test.
5. Place one valid externally supplied `.tilt` in a dedicated shared folder, using
   a separate package or real external transfer. Select that folder in the probe.
   It requests a persisted read-only tree grant. The probe compares ordinary path
   reads, a bounded Java channel adapter, a live descriptor alias, and MediaStore
   translation followed by ordinary path reads. It never modifies selected files.
   Repeat after process restart to test persisted grants. A path candidate is
   experimental and only derived for primary local document IDs.
6. Run the explicit >2 GB button only with sufficient space. It may allocate over
   2 GB, requires more than 3 GB free, and deletes only its own unique fixture.

Before any headset control, atomically create the shared reservation at
`C:/Users/andyb/Documents/open-brush-fast/.headset-reservation.json`, recording
`sessionId`, `task`, and UTC `startTime`. If it exists, wait. Only its owner removes
it; never overwrite or expire another agent's reservation. Release it during builds.

Use `collect.ps1 -Serial <adb-serial> -InstallAndStart -HeadsetSessionId <owner>` after manifest inspection
for the first install. It refuses to replace an existing package. Later omit
`-InstallAndStart` to capture results, package state, fingerprint, and the local
APK hash and installed base APK hash in a unique untracked evidence directory.
These can differ if the local build has changed since installation.
An explicitly authorized probe update uses `-UpdateProbe -ExpectedInstalledHash
<recorded-installed-sha256> -HeadsetSessionId <owner>`. It checks package ownership against that exact APK
revision before using `adb install -r`, preserving app data and grants.

All results use `OBDS_UNITY` plus UTC time, route, file length where applicable,
and operation. Collect OS/fingerprint/UID/grants, APK identity, actual manifest,
device restart results, and input provenance alongside the report.

The synthetic archive has the real Tilt header and ZIP layout, metadata, a PNG
thumbnail, and synthetic bytes instead of stroke data. Its success proves archive
access, not sketch validity or rendering. Channel reads use signed JNI arrays,
64-bit offsets, independent stream owners and fixed 64 KiB buffers. Descriptors
remain Java-owned; no detached descriptor enters a managed handle. Member reads
are interleaved, with a separate attached background-thread check. This does not
prove full asynchronous consumer/cancellation lifetimes. The large-offset check
also exercises Java-channel offsets through a file URI; provider-backed access
beyond 2 GB remains a separate gate.
No whole-file mirror or provider write backend is implemented.

The build template also copies the worktree's production `AndroidStoreManifest.cs`
into the generated project. A probe-only lower-priority androidlib deliberately
conflicts on storage permissions and legacy flags. The callback applies the actual
production scoped policy, checks non-scoped legacy preservation and rejects a Meta
combination. A fresh completion marker is required before packaging. Always inspect
the final merged APK; successful XML transformation alone is insufficient.

The write experiment is restricted to `Documents/OBDS_ExternalFixture20261009_1347`
and an exact six-byte synthetic payload. Its separate button requests a write grant
to that folder. It tests translation, write/flush and direct backup rename, using
SAF rename only if direct rename fails, then installs a verified new temp. The old
bytes remain under a unique backup name. It does not prove crash safety and refuses
to mutate the already-replaced payload on repeat. No real sketch is written.

Remaining milestone gates include a real large sketch, async/cancellation lifetimes,
provider errors and revocation, provider-backed offsets beyond 2 GB, direct operations
on retail devices, and the mixed SAF/path replacement transaction. Do not enable
production scoped storage based only on the initial synthetic checks.
