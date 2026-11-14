# Integrated branch features

Integration branch: `integration/passthrough-ar-handtracking-improvements`.

1. **Passthrough — `ccr-29efd844-5u9tp5` ([PR #1199](https://github.com/icosa-foundation/open-brush/pull/1199))**

   Adds passthrough through OpenXR's core alpha-blend composition mode, supporting capable runtimes such as Steam Frame and Google Android XR. Detects runtime support, uses the rig's default scene layer for eye-buffer blending, and adds the `passthrough.blendtype` API command for testing blending behavior.

2. **Handheld AR navigation — `ccr-90540b68-2mnxpf` ([PR #1201](https://github.com/icosa-foundation/open-brush/pull/1201))**

   Adds optional AR navigation to the handheld, no-headset view on Android and iOS. Moving the device moves the viewpoint through the sketch, with a camera-feed toggle. Integration fixes address Android permissions and pose tracking, preserve the starting viewpoint when entering AR, and retain the resulting viewpoint when leaving it.

3. **Hands and mixed input — `ccr-e389df31-tvut3x` ([PR #1200](https://github.com/icosa-foundation/open-brush/pull/1200))**

   Extends hand tracking to Quest and other Android OpenXR targets, with mid-session switching between hands and controllers. Each side can use a different input source. World manipulation supports two fists or a fist plus a controller grip. Follow-up fixes restore controller bindings, handedness, and pose drivers when returning from hands to controllers.

4. **Android Viewer without SAF — `feature/viewer-without-saf`**

   Lets Open Brush Viewer start using app-owned storage without requesting Android's Storage Access Framework folder selection or all-files access. Seeds bundled sketches into its local sketchbook, resolves built-in sketch links locally, handles inaccessible external sketches gracefully, and names the Android app Open Brush Viewer. Bundled binary sketches are included only in Viewer builds.

This summary includes the follow-up fixes merged into integration. Platform support described here is implementation scope, not a claim that every platform has been tested on hardware.
