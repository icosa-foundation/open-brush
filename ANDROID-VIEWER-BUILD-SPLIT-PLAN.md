# Android headset and handheld Viewer build split

## 1. Objective and agreed scope

1. Separate Android headset applications from the Android phone/tablet Viewer.
2. Include ARCore only in the handheld Android Viewer, for optional handheld AR navigation and its camera feed.
3. Keep OpenXR and the appropriate AR Foundation/OpenXR providers in headset builds. AR Foundation is a shared API; retaining it does not require shipping the ARCore provider.
4. Do not create a non-Viewer handheld Android editor product.
5. Preserve the existing headset Viewer variants. The new handheld Viewer does not replace those products.
6. Use supported Unity build/provider configuration. Do not introduce undocumented runtime switches, binary patches, native interception, custom delayed loading, or reverse-engineered workarounds.
7. This document is a plan only. Writing it does not authorize implementation, Unity runs, builds, publishing, commits, pushes or PR creation.
8. Confirmed scope: this is a build compatibility change, not a functionality or UX redesign. Preserve existing handheld behavior; new file-opening workflows, screen layouts, orientation behavior and unrelated runtime improvements are out of scope.
9. Confirmed product policy: remove handheld AR from the existing headset APKs, including the full Open Brush APK. Provide phone AR only through the new handheld Viewer; no handheld full-editor replacement is required.
10. Responsibility: the agent handles code and build configuration; the user handles device/runtime validation. User device validation is not a prerequisite for completing the code work, and must not be reported as performed or successful without the user's results.

## 2. Evidence and limits

1. Source reviewed on 2026-10-28 at local HEAD `09917c13e671f7141031aeadced311ac210a647f`. Recheck current files and unrelated working-tree changes before implementation.
2. The failing headset APK reports `2.32.45-gd60634329`. Its logs show ARCore native initialization and Vulkan fence handling before OpenXR Vulkan device creation fails.
3. ARCore exclusion fixing the failure is the user's hypothetical assumption. No controlled device comparison has established it. Do not report this product split as a verified launch fix until it is tested.
4. `Assets/Editor/BuildTiltBrush.cs`, in `TempSetXrPlugin`, currently appends `UnityEngine.XR.ARCore.ARCoreLoader` to generic Android OpenXR builds unless `USE_QUEST_PACKAGE_NAME` is defined. This couples handheld AR to headset APKs.
5. `Assets/Scripts/HandheldArNavigation.cs` removes the AR loader from the headset search at `BeforeSceneLoad`. This does not establish that the native library stayed unloaded.
6. Published ARCore 6.6.2 package source was inspected. `Editor/ARCoreBuildProcessor.cs` uses `SetIncludeInBuildDelegate` for `UnityARCore.aar`, `ARPresto.aar`, and `arcore_client.aar`. Inclusion checks whether `ARCoreLoader` is selected in the Android build's XR settings. Start with ordinary build-time provider selection and inspect the packaged outputs.
7. `PassthroughManager.SetBlendModePassthrough` uses an AR Foundation `ARCameraManager`. Android XR provides its own OpenXR camera subsystem. Removing ARCore must not remove these shared features.
8. Native-library inspection is stopped. This plan requires no decompilation, disassembly or undocumented workaround.

## 3. Intended CI products

| Matrix entry | Product | Selected Android providers | Output/destination |
| --- | --- | --- | --- |
| Android OpenXR | Full headset app | OpenXR, no ARCore | Existing APK; Steam, Pico and generic headset download |
| Android AndroidXR | Full Android XR headset app | OpenXR with Android XR features, no ARCore | Existing AAB and Google Play destination |
| Android Viewer OpenXR | Headset Viewer | OpenXR, no ARCore | Existing Viewer APK |
| Android Viewer AndroidXR | Android XR headset Viewer | OpenXR with Android XR features, no ARCore | Existing Viewer AAB and Google Play destination |
| Android Meta Quest | Full Meta headset app | Existing OpenXR/Meta configuration, no ARCore | Existing Meta APK and destinations |
| Android Viewer Handheld (new) | Phone/tablet Viewer | ARCore, no OpenXR loader | New APK artifact and release download |

1. Add exactly one CI configuration: `Android Viewer Handheld`. Do not add a Steam Frame-specific build or a handheld full-editor build.
2. "Non-XR" means no headset/OpenXR requirement here. Handheld AR still uses Unity XR Management and AR Foundation internally.
3. Keep Windows, Linux, macOS, iOS Viewer and iOS Zapbox behavior unchanged.
4. Initially ship the handheld Viewer as an APK artifact/release download. A phone Google Play AAB/store listing is a separate release decision.

## 4. Build representation

1. Use existing `XrSdkMode.Monoscopic` for the Android handheld build selection, with `OPEN_BRUSH_VIEWER`. This avoids inventing another headset SDK.
2. Add Android/Monoscopic to `kValidSdkTargets`, with an explicit validation that rejects it unless the Viewer product is selected. Do not accidentally enable a full-editor phone build.
3. Centralize the handheld product predicate: Android target, Monoscopic build selection and Viewer product. Reuse it for loader selection, manifest configuration and build reporting.
4. Keep build selection distinct from application runtime mode. `BuildTiltBrush` currently sets `Config.m_SdkMode = SdkMode.UnityXR`, and `HandheldArNavigation.IsSupported` requires that runtime mode. Do not blindly set runtime Monoscopic or force `EnableMonoscopicMode`; that would disable the existing handheld AR path.
5. Preserve the existing UnityXR application infrastructure with no headset loader and use the existing no-headset Viewer UI. Audit `VrSdk.Awake`, loader exclusion and startup ordering to ensure an ARCore session cannot be mistaken for a headset.
6. Make this configuration reproducible through both the local build UI and command-line builder. CI should select the product, not implement a separate policy.

## 5. Provider and package inclusion

1. In `TempSetXrPlugin`, remove ARCore loader registration from generic Android OpenXR builds. They select their existing headset loader only.
2. For Android/Monoscopic Viewer, select only `ARCoreLoader` and call `ConfigureOptionalArCore`. Keep ARCore and depth optional so unsupported phones can still use touch navigation.
3. Preserve current AndroidXR features and Meta settings. Preserve iOS ARKit selection and Zapbox behavior.
4. Let the ARCore package's own build processor omit its runtime AARs when its loader is unselected. Keep the package dependency in the project for the handheld build.
5. Do not initially remove ARCore from the manifest/lock file per job, edit package caches, or add custom plugin-import filters. Establish whether the existing provider inclusion hook produces the required binaries first.
6. Inspect actual APK/AAB contents. If built-in exclusion fails, identify the exact cause before choosing another supported mechanism; do not silently introduce a workaround.
7. Retain AR Foundation where shared source needs it. Headset camera/session/spatial features must continue to compile and run.
8. Restore prior loader lists in their original order and restore changed provider settings after success or failure. Cover exceptions during partial setup, before a disposable constructor completes; relying on its later `Dispose` is insufficient. Preserve prior values and whether settings assets existed. Switching variants must not leave project assets changed. Keep this work scoped to settings touched by the split.

## 6. Runtime behavior

1. Handheld Viewer starts in touch navigation, without an OpenXR runtime requirement. AR tracking and its camera feed remain explicit user actions.
2. Reuse `HandheldArNavigation` for permission requests, installation/availability checks, tracking, camera feed, pause/resume and teardown.
3. Preserve useful touch-mode fallback when ARCore is unavailable, camera permission is denied, installation is declined or tracking fails.
4. Headset variants must not attempt ARCore installation, request phone-AR camera permissions or show nonfunctional handheld-AR controls. Preserve platform camera permissions actually needed by headset features.
5. Preserve existing Viewer restrictions. Do not expose editor/drawing functions in the handheld product.
6. Existing flat/view-only fallbacks on headset apps may remain, but no longer offer handheld AR.
7. Keep player controls about touch navigation, AR and camera feed. Do not expose loader names, CI labels, package details or diagnostics in player UI.
8. Keep iOS Viewer and Zapbox behavior intact where they share runtime code.
9. These are preservation requirements, not authorization to add new runtime functionality or UX. Change shared runtime code only where the provider split requires it.

## 7. Manifest, identity and storage

1. Audit `Assets/Editor/AndroidStoreManifest.cs`, Android manifest templates, XR package hooks and `TempSetPlayerSettings` for assumptions that all Android builds target headsets.
2. Handheld Viewer must require no headset/OpenXR system feature, headset-only launcher category, hand-tracking support or controller capability. Its normal installation and touch mode must not require ARCore availability.
   Audit existing headset metadata and permission contributions too, including `pvr.app.type=vr` in the base manifest and the Quest/Pico declarations currently added to non-AndroidXR builds by `AndroidStoreManifest.Configure`. Apply handheld-specific handling without changing headset policy.
3. Retain existing AndroidXR merged-manifest policy and validation for the two AndroidXR AABs. Do not apply phone-specific transformations to them.
4. Preserve Steam/Pico/Meta application identities, entry points, signing and SDK requirements.
5. Confirmed: retain the existing Android Viewer application identifier for both Viewer APK variants. These APKs replace each other on one device and retain app data under normal compatible updates. Preserve signing/version conventions and existing preferences; do not introduce a new identity, preference reset or migration as part of this split. A different identifier is needed only if simultaneous installation or separate store listings become an explicit requirement.
6. Use distinct release filenames. Proposed phone asset: `OpenBrushViewer_Android_Handheld_<version>.apk`. Identify the headset OpenXR Viewer clearly wherever both are offered.
7. Keep existing APK storage behavior, file-opening functionality, media import, bundled content and preferences. Do not add a file picker, external-file workflow or storage redesign. Device regression validation belongs to the user. A later phone AAB release needs separate review of scoped storage and Play requirements.
8. Inspect final merged manifests. Source template checks alone cannot establish installability or store/device eligibility.

## 8. CI implementation

1. Add `Android Viewer Handheld` to both `matrix.name` and `matrix.include` in `.github/workflows/build.yml`. Its flavors cross-product means adding an include row alone is insufficient in the current workflow.
2. Use `targetPlatform: Android`, `vrsdk: Monoscopic`, `extra_defines: OPEN_BRUSH_VIEWER`, IL2CPP, API 35 initially and the existing appropriate Android Photon defines. Confirm package requirements during implementation.
3. Start with the existing Vulkan graphics selection. Do not combine the product split with an API migration or performance tuning. ARCore 6.6.2 supports Vulkan; retain the renderer support it needs.
4. Reuse the existing `Android_Vulkan` Library cache for handheld builds, as confirmed after the first PR run missed its separate cache and compiled shaders from scratch. Provider selection is applied per build and packaged-provider checks enforce the split. Preserve the existing main-only cache-save policy.
5. Account for output under `build/Monoscopic/Android`. Audit any step assuming all Android output paths use `OpenXR` or `AndroidXR`.
6. Verify generated filenames as well as artifact names. `OPEN_BRUSH_VIEWER` alone does not establish that every workflow-generated output name is unique.
7. Add a small packaged-artifact verification step with bounded output: archive library entries, relevant merged-manifest features and an explicit result.
8. Headset outputs must omit ARCore native runtime libraries, including `libUnityARCore.so`. Derive the complete relevant library list from the three provider AARs during implementation instead of assuming that checking one name proves full exclusion.
9. Handheld output must include ARCore and select no OpenXR loader. Inspect OpenXR plugin/runtime packaging and required manifest features as well. A package remaining in the project is not proof that its runtime is shipped.
10. Retain existing AndroidXR AAB manifest checks and applicable CI checks. Avoid excessive new tests or unrelated workflow refactoring.

## 9. Release routing

1. Steam continues downloading `Android OpenXR`. No additional Frame job, depot, staging directory or package ID is needed.
2. Pico and Pico ArborXR continue consuming `Android OpenXR`. Headset capability remains; its former phone AR capability is removed.
3. Meta publishing continues consuming `Android Meta Quest`.
4. Google Play full and Viewer AndroidXR jobs continue consuming their existing AAB artifacts. Do not substitute the handheld Viewer APK.
5. Add the handheld Viewer APK to downloadable release assembly with a distinct filename and player-facing label.
6. Audit all artifact consumers and hard-coded Android paths. Preserve established headset filenames where downstream systems rely on them; avoid unnecessary matrix renames.
7. Do not enable a new store upload or publish anything without explicit authorization. Do not create a PR or push a branch unless directly requested.

## 10. Implementation sequence

1. Build policy: support the handheld Viewer combination, validate unsupported combinations, select ARCore only for that product and preserve settings restoration.
2. Runtime/manifests: ensure touch-first handheld startup and explicit AR, remove handheld-AR controls from headset variants, and verify per-product manifests.
3. CI/release routing: add the single matrix row, output/cache handling, archive checks and downloadable release asset; preserve store/depot consumers.
4. Player documentation: explain which Viewer download is for a headset and which is for a phone/tablet. Keep technical implementation details out of download instructions.
5. If commits are requested, keep independent changes in focused commits. Preserve unrelated staged, modified and untracked work. Keep this temporary plan untracked unless explicitly asked to version it.

## 11. Verification and acceptance

The agent owns implementation and narrow static/code checks, including the packaged checks wired into CI. The user owns device/runtime validation below. Do not launch Unity, make local builds or run device tests under this plan alone; later implementation instructions determine authorization for those actions. The device checks describe useful regression coverage, not additional functionality or a gate on code completion.

1. Static review: inspect build combinations, loader lists, inclusion hooks, settings restoration, output naming, manifests and publishing consumers. Run applicable narrow checks and `git diff --check`.
2. Packaged checks: inspect a full headset APK, a headset Viewer APK, the handheld Viewer APK and relevant AndroidXR AABs. Establish actual absence/presence of provider libraries and required features.
3. User validation, Frame: launch full app and headset Viewer, reach a usable VR session, check controllers and passthrough, and capture logs comparable with the failing APK. Claim the reported launch issue is fixed only if the user's results establish that.
4. User validation, phone/tablet: launch without an OpenXR runtime; check existing touch navigation, file opening, explicit AR activation, feed toggle, return from AR, pause/resume and unavailable-AR fallback. Include switching Viewer APK variants with retained data/preferences if relevant to the user's testing. Do not pre-grant permissions when testing normal user permission flow.
5. User validation, Quest/Pico/Android XR: check startup, tracking and passthrough on representative devices. Packaging/CI success is not device validation.
6. iOS: the agent reviews shared code changes and uses applicable existing CI checks when authorized. Any device testing belongs to the user. Do not claim unperformed device testing.
7. Use a unique diagnostic prefix, for example `[OB_ANDROID_BUILD_SPLIT]`, for any temporary validation logging. Match timestamps to the actual test and report only relevant bounded excerpts.
8. Respect Unity coordination: inspect Editor ownership/state before live actions, do not discard unsaved work or interrupt another agent, and do not resume explicitly paused Unity work. This plan is not authorization to launch an Editor.

## 12. Completion and rollback

1. Code work is complete when the implementation and CI configuration provide the handheld Viewer, select providers per product, enforce the intended packaged contents through CI checks, preserve existing functionality and route artifacts correctly, with narrow code checks reported. Report actual package/CI results separately when available; configured checks are not evidence they passed. User device/runtime validation may remain pending without blocking code completion.
2. Preserve current headset identities and routing so the change can be reverted without a store/depot migration.
3. If ARCore exclusion does not fix Frame launch, report that result and investigate the Vulkan failure independently. Do not present the product split as causal proof.
4. If provider selection does not exclude the native library as expected, document the blocking package/build behavior before proposing another mechanism or another CI variant.
