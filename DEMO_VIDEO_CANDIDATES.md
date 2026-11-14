# Demonstration video candidates since 2.32.0

Reviewed 8 November 2026 against main at `1db9e747c3696abdd66fd4e9b1b10bb5d4a19795`. This is a local planning list; do not commit it without a direct request.

## Scope

1. Baseline: [2.32.0](https://github.com/icosa-foundation/open-brush/releases/tag/2.32.0), the latest stable release, published 1 September 2026.
2. The literal latest tag is [2.32.59](https://github.com/icosa-foundation/open-brush/releases/tag/2.32.59), a prerelease at the current main tip. There are no main-branch changes after that tag.
3. [Comparison from the stable release](https://github.com/icosa-foundation/open-brush/compare/2.32.0...1db9e747c3696abdd66fd4e9b1b10bb5d4a19795): 59 main-line commits, each associated with a PR. All 59 are triaged below.
4. Integration merges #1069, #1191 and #1212 contain feature work whose own PRs sometimes merged into another branch or were closed without a direct GitHub merge. Inclusion follows code reaching main after the stable tag, not just PR merge dates. Reshape/Tint #1140 merged into integration in August and first reached main through #1069 in September.

## Selection rule

1. Prioritise a visible creative action with a clear before/after and a lesson that fits in roughly 15–45 seconds.
2. Give each clip one job. A feature family can have several shorts; related PRs can share one clip.
3. Fold maintenance fixes into the relevant workflow instead of producing a video per fix.
4. Use default panel layouts and the shared Open Labs section. Current prototypes are starting points, not a claim that all listed interactions are already automated.

## Candidate backlog

1. The current library has 12 canonical tutorial videos. New symmetry brush plugins compares Snowflake, Plait,
   HolidayTree and CandyCane in one shot; Tint covers amount and clearing
   in one video. The snapping videos are `endpoint-snapping`, `angle-snapping` and
   `grid-snapping`. Tool Script live preview and Darken now have recordings.
2. Draw and Open Labs are reusable components, excluded from tutorial recordings.
3. Shared sections include native preset navigation, slider dragging, grid selection,
   undo and preview holds. Membrane and endpoint demonstrations continue directly
   between strokes instead of returning the brush controller to neutral.

| # | Priority | Feature / PRs | Proposed demonstration | Production notes |
| --- | --- | --- | --- | --- |
| 1 | P1 | **Reshape series** — [#1140](https://github.com/icosa-foundation/open-brush/pull/1140), [#1069](https://github.com/icosa-foundation/open-brush/pull/1069) | Grab/bend, Push a straight stroke into an arch, Flatten broad ripples, Smooth a jittery arch; add Grab twisting if useful. | Grab, Push, Flatten and Smooth now have scripts/recordings. Pinch is unused and excluded. |
| 2 | P1 | **Tint series** — [#1140](https://github.com/icosa-foundation/open-brush/pull/1140), [#1069](https://github.com/icosa-foundation/open-brush/pull/1069) | Apply a contrasting tint locally; separate follow-ups for effect amount and clearing tint. | One combined recording covers soft application, full strength and clearing through the native controls. |
| 3 | P1 | **3D membrane brushes and outlined membrane** — [#1174](https://github.com/icosa-foundation/open-brush/pull/1174), [#1184](https://github.com/icosa-foundation/open-brush/pull/1184) | Draw a curved boundary and show the surface forming; compare matte/shiny/diamond and outlined variants. | Strong next drawing demo. Reuse brush selection, drawing paths and result reveal. |
| 4 | P1 | **Straight-edge endpoint/grid/angle snapping** — [#1151](https://github.com/icosa-foundation/open-brush/pull/1151) | Finish a line precisely at an existing endpoint, then show grid/angle constraints. | Endpoint, angle and grid snapping have separately named scripts and recordings. |
| 5 | P1 | **Symmetry-script presets** — [#1183](https://github.com/icosa-foundation/open-brush/pull/1183) | Draw one gesture using Snowflake, Plait, HolidayTree or CandyCane; show how the chosen preset changes the result. | One consolidated new symmetry brush plugins video, including an upward HolidayTree gesture. Individual presets remain reusable through --param preset=NAME. |
| 6 | P1 | **Tool Script live stroke preview** — [#1022](https://github.com/icosa-foundation/open-brush/pull/1022), [#1165](https://github.com/icosa-foundation/open-brush/pull/1165) | Choose a suitable single-path script, hold/adjust to preview, release to commit. | Circle recording demonstrates resizing while held and committing on release; its stroke-count checks distinguish preview from committed geometry. |
| 7 | P1 | **Darken brush** — [#1021](https://github.com/icosa-foundation/open-brush/pull/1021) | Draw a darkening stroke over bright artwork and reveal the resulting effect. | Darken recording uses the Dark brush over bright artwork with three light-grey, partially overlapping passes at distinct depths. |
| 8 | P1 | **Quill import and playback** — [#1069](https://github.com/icosa-foundation/open-brush/pull/1069) | Open the Quill library, import an example and demonstrate animation/chapter navigation where present. | Feature reached main through the integration merge. Needs a suitable Quill asset and reusable library/import sections. |
| 9 | P2 | **Android folder access and media import** — [#1191](https://github.com/icosa-foundation/open-brush/pull/1191), [#1229](https://github.com/icosa-foundation/open-brush/pull/1229) | Choose a folder through the system picker, import supported media, then revisit that folder. | Requires Android/system-picker footage. Source branches such as #1190/#1120 were incorporated through #1191. |
| 10 | P2 | **Hand tracking and controller handoff** — [#1191](https://github.com/icosa-foundation/open-brush/pull/1191), [#1198](https://github.com/icosa-foundation/open-brush/pull/1198), [#1212](https://github.com/icosa-foundation/open-brush/pull/1212), [#1228](https://github.com/icosa-foundation/open-brush/pull/1228), [#1231](https://github.com/icosa-foundation/open-brush/pull/1231) | Pinch to draw, grab/move the scene, then pick up a controller and continue. | Use device footage. Split drawing/UI/navigation from controller handoff rather than cramming every gesture into one clip. |
| 11 | P2 | **Handheld AR viewer** — [#1212](https://github.com/icosa-foundation/open-brush/pull/1212) | Enable AR, walk around a sketch and return to touch navigation; show camera-feed choice where supported. | Android/iOS support is in the integration contents. Needs a supported device; platform availability must be verified for the chosen build. |
| 12 | P2 | **Touchscreen viewer navigation and new viewer builds** — [#1171](https://github.com/icosa-foundation/open-brush/pull/1171), [#1169](https://github.com/icosa-foundation/open-brush/pull/1169), [#1220](https://github.com/icosa-foundation/open-brush/pull/1220) | Open a bundled sketch and pinch to dolly nearer/further; explain which viewer build to use. | Viewer existed before the baseline. Focus on the new navigation/build availability, not claiming the whole viewer is new. |
| 13 | P2 | **Headset passthrough with object selection** — [#1180](https://github.com/icosa-foundation/open-brush/pull/1180), [#1212](https://github.com/icosa-foundation/open-brush/pull/1212), [#1216](https://github.com/icosa-foundation/open-brush/pull/1216), [#1217](https://github.com/icosa-foundation/open-brush/pull/1217) | Switch to passthrough, place art and select an imported object with visible feedback. | Headset footage. Fold passthrough/selection fixes into the feature demonstration. |
| 14 | P2 | **EXR/HDR image import** — [#1130](https://github.com/icosa-foundation/open-brush/pull/1130) | Import an EXR reference/background and show the HDR result in a simple sketch. | Needs an appropriate image fixture. Avoid promising an exposure UI unless demonstrated in the actual build. |
| 15 | P3 | **Camera-path video export quality** — [#1185](https://github.com/icosa-foundation/open-brush/pull/1185), [#1177](https://github.com/icosa-foundation/open-brush/pull/1177), [#1189](https://github.com/icosa-foundation/open-brush/pull/1189) | Render one short camera path with chosen output settings and compare final exports. | Advanced workflow/configuration tutorial. Bloom controls are user-config fields, not a newly claimed slider. |
| 16 | P3 | **Gaussian capture to reusable camera path** — [#1166](https://github.com/icosa-foundation/open-brush/pull/1166) | Capture a short route and create/preview its camera path. | Separate this user workflow from downstream dataset training. |
| 17 | P3 | **Gaussian capture dataset and depth export** — [#1212](https://github.com/icosa-foundation/open-brush/pull/1212) | Show the capture output as a COLMAP-style dataset with optional depth TIFFs, and explain its intended consumer. | Technical/export tutorial. #1197's changes arrived through #1212; it was not separately merged. |
| 18 | P3 | **Gaussian splat performance controls** — [#1069](https://github.com/icosa-foundation/open-brush/pull/1069) | Use one dense splat and explain pruning/sorting/quality settings with measured tradeoffs. | Technical configuration guide rather than a first-wave artist demo. #1158 is context; changes arrived through integration. |
| 19 | P3 | **Stroke cropping through API/Lua** — [#984](https://github.com/icosa-foundation/open-brush/pull/984) | Crop prepared strokes to a plane or volume, then undo. | API/Lua-oriented example, not a tutorial for a new visible Crop tool. No such UI tool is introduced by this PR. |
| 20 | Supporting | **Layer-aware editing** — [#1163](https://github.com/icosa-foundation/open-brush/pull/1163) | Edit a stroke on the active layer while another layer's artwork remains untouched. | Fold into a Tint/Reshape or layers clip; this PR fixes intended layer scoping. |
| 21 | Supporting | **Bundled media and nested saved-stroke folders** — [#1155](https://github.com/icosa-foundation/open-brush/pull/1155), [#1168](https://github.com/icosa-foundation/open-brush/pull/1168), [#1225](https://github.com/icosa-foundation/open-brush/pull/1225) | Choose a bundled starter or navigate a saved-stroke subfolder. | Useful setup/library sections. Do not make a release-headline clip for metadata/catalog bug fixes alone. |

## Reusable sections to build or extend

1. Open Labs: already implemented and shared by Tint/Reshape.
2. Prepare a visible starter stroke, approach a contact, apply a path and withdraw: already shared. Keep controller bodies beside/below the demonstrated target.
3. Select a brush/preset and navigate its library: extend once for membrane, Darken, symmetry scripts and Tool Scripts.
4. Change colour, effect amount and tool radius: distinguish scene preparation from visible UI interactions.
5. Toggle a guide/snapping option and point out the resulting constraint.
6. Select the active layer and demonstrate that other layers are unaffected.
7. Open a media library, choose a fixture, wait for import and reveal it: shared by Quill, references, splats and saved strokes.
8. Undo/Redo and hold the result for comparison.
9. Keep headset hands/passthrough, mobile touch/AR and OS storage pickers as separate capture tracks; the current virtual-controller executor does not simulate those interfaces.

## Main-line PR audit

| PR | Title | Video disposition |
| --- | --- | --- |
| [#1231](https://github.com/icosa-foundation/open-brush/pull/1231) | Fix pinch/grab precedence | Fold into hand-tracking/controller-handoff demo. |
| [#1230](https://github.com/icosa-foundation/open-brush/pull/1230) | Bump the all-actions-updates group across 1 directory with 2 updates | No standalone demo: CI dependency update. |
| [#1229](https://github.com/icosa-foundation/open-brush/pull/1229) | Keep SAF durability checks from blocking picker dismissal | Fold into Android folder-picker/import demo. |
| [#1228](https://github.com/icosa-foundation/open-brush/pull/1228) | Fix hand grab hysteresis | Fold into hand-tracking grab demo. |
| [#1225](https://github.com/icosa-foundation/open-brush/pull/1225) | Exclude widget metadata from saved-stroke snapshots | Fold into saved-stroke/library setup if relevant; metadata fix alone needs no clip. |
| [#1226](https://github.com/icosa-foundation/open-brush/pull/1226) | Fix git churn | No standalone demo: generated-file/Git churn. |
| [#1222](https://github.com/icosa-foundation/open-brush/pull/1222) | Fix brush descriptor startup errors | No standalone demo: Editor startup diagnostic fix. |
| [#1224](https://github.com/icosa-foundation/open-brush/pull/1224) | Fix publish errors | No standalone demo: publishing. |
| [#1223](https://github.com/icosa-foundation/open-brush/pull/1223) | Play Store CI publish update | No standalone demo: store publishing. |
| [#1221](https://github.com/icosa-foundation/open-brush/pull/1221) | Fix Steam Frame dynamic quality corruption | No standalone demo: runtime quality corruption fix. |
| [#1220](https://github.com/icosa-foundation/open-brush/pull/1220) | Separate 2d builds for Android Viewer | Fold into viewer build/navigation introduction. |
| [#1219](https://github.com/icosa-foundation/open-brush/pull/1219) | Fix Android page size | No standalone demo: Android compatibility. |
| [#1217](https://github.com/icosa-foundation/open-brush/pull/1217) | Fix mobile widget selection highlights | Fold into passthrough/model-selection demo. |
| [#1218](https://github.com/icosa-foundation/open-brush/pull/1218) | Android launcher icon | No standalone demo: launcher icon. |
| [#1216](https://github.com/icosa-foundation/open-brush/pull/1216) | Fix passthrough selection bug on Quest | Fold into passthrough/model-selection demo. |
| [#1214](https://github.com/icosa-foundation/open-brush/pull/1214) | Fix compositor quality switching bugs | No standalone demo: quality-switching correctness. |
| [#1212](https://github.com/icosa-foundation/open-brush/pull/1212) | Integration branch for the PRs that will be merged soon | Multiple candidates: handheld AR, hand/controller switching, passthrough, Gaussian depth export and viewer startup. |
| [#1203](https://github.com/icosa-foundation/open-brush/pull/1203) | Update Unity Tools package lock for brush fog fixes | No standalone demo: brush fog/parity dependency fix. |
| [#1198](https://github.com/icosa-foundation/open-brush/pull/1198) | Handtracking bugfix round 1 | Fold into hand tracking. |
| [#1195](https://github.com/icosa-foundation/open-brush/pull/1195) | Remove unnecessary Play device requirements for AndroidXR builds | No standalone demo: store device-requirement metadata. |
| [#1191](https://github.com/icosa-foundation/open-brush/pull/1191) | Integrate handtracking and Android Direct Storage SAF | Multiple candidates: hand tracking and Android storage/system-picker workflow. |
| [#1194](https://github.com/icosa-foundation/open-brush/pull/1194) | Various beta fixes | No standalone demo: beta/UI fixes; use corrected behaviour in other demos. |
| [#1189](https://github.com/icosa-foundation/open-brush/pull/1189) | Restore missing post-processing | Fold into rendering/quality examples; restores previous behaviour. |
| [#1186](https://github.com/icosa-foundation/open-brush/pull/1186) | Fix offline render unicode bugs | No standalone demo: Unicode path handling. |
| [#1187](https://github.com/icosa-foundation/open-brush/pull/1187) | Fix gltf shader stripping again | No standalone demo: import shader correctness. |
| [#1188](https://github.com/icosa-foundation/open-brush/pull/1188) | Restore OpenGL ES 3 on Pico, whose Vulkan driver crashes | No standalone demo: Pico driver workaround. |
| [#1185](https://github.com/icosa-foundation/open-brush/pull/1185) | Better video rendering quality and configurability | Candidate: high-quality camera-path video export. |
| [#1183](https://github.com/icosa-foundation/open-brush/pull/1183) | Symmetry plugin versions of old parent brushes | Candidate: symmetry-script brush presets. |
| [#1184](https://github.com/icosa-foundation/open-brush/pull/1184) | Membrane brush variant with outlines | Candidate: outlined membrane; combine with 3D filling introduction. |
| [#1181](https://github.com/icosa-foundation/open-brush/pull/1181) | Fix some MSAA and A2C issues | No standalone demo: antialiasing correctness. |
| [#1182](https://github.com/icosa-foundation/open-brush/pull/1182) | Revert explicit sample coverage in UnlitA2CVertexColor shader | No standalone demo: shader coverage fix. |
| [#1180](https://github.com/icosa-foundation/open-brush/pull/1180) | Shader passthrough fixes | Fold into passthrough examples; shader correctness. |
| [#1021](https://github.com/icosa-foundation/open-brush/pull/1021) | Darken brush | Candidate: Darken brush. |
| [#1174](https://github.com/icosa-foundation/open-brush/pull/1174) | 3D stroke filling | Candidate: 3D membrane/fill brushes. |
| [#1178](https://github.com/icosa-foundation/open-brush/pull/1178) | Fix atlas issues | No standalone demo: icon/atlas correctness. |
| [#1177](https://github.com/icosa-foundation/open-brush/pull/1177) | Expose bloom amount and threshold in user config | Advanced configuration segment: bloom amount/threshold. |
| [#1176](https://github.com/icosa-foundation/open-brush/pull/1176) | Fix macOS plugin bundle identifiers before signing | No standalone demo: signing/package identifiers. |
| [#1169](https://github.com/icosa-foundation/open-brush/pull/1169) | Build and publish non-XR Apple viewer apps | Fold into viewer introduction; Apple build availability. |
| [#1171](https://github.com/icosa-foundation/open-brush/pull/1171) | Add pinch dolly navigation for touchscreen viewer | Candidate: touchscreen pinch-dolly navigation. |
| [#1168](https://github.com/icosa-foundation/open-brush/pull/1168) | Upstream catalog fixes from SAF branch | Reusable media-library section; no standalone catalog-fix video. |
| [#1170](https://github.com/icosa-foundation/open-brush/pull/1170) | Fix non-vr black screen | No standalone demo: non-VR rendering correctness. |
| [#1167](https://github.com/icosa-foundation/open-brush/pull/1167) | Update imm plugin | Fold into Quill import if behaviour is relevant; package update alone needs no video. |
| [#1166](https://github.com/icosa-foundation/open-brush/pull/1166) | Splat capture camera path | Candidate: Gaussian capture to camera path. |
| [#1165](https://github.com/icosa-foundation/open-brush/pull/1165) | Reject invalid ToolScript preview scales | Fold into Tool Script preview. |
| [#1130](https://github.com/icosa-foundation/open-brush/pull/1130) | EXR import and better hdr image handling | Candidate: EXR/HDR references/backgrounds. |
| [#1148](https://github.com/icosa-foundation/open-brush/pull/1148) | Http api tests | No standalone demo: API test coverage and correctness fixes. |
| [#1151](https://github.com/icosa-foundation/open-brush/pull/1151) | Simple straight edge snapping | Candidate: straight-edge endpoint/grid/angle snapping. |
| [#1022](https://github.com/icosa-foundation/open-brush/pull/1022) | Tool plugin stroke preview | Candidate: live single-path Tool Script stroke preview. |
| [#1163](https://github.com/icosa-foundation/open-brush/pull/1163) | Fix tools affecting other layers | Optional layers/tool-scope example; otherwise fold into Reshape/Tint. |
| [#984](https://github.com/icosa-foundation/open-brush/pull/984) | Crop strokes | Candidate for API/Lua audience: stroke cropping, not a new controller tool. |
| [#1155](https://github.com/icosa-foundation/open-brush/pull/1155) | Default media files improvements | Reusable bundled-media/starter selection section. |
| [#1162](https://github.com/icosa-foundation/open-brush/pull/1162) | Fix export exceptions | No standalone demo: export correctness. |
| [#1161](https://github.com/icosa-foundation/open-brush/pull/1161) | Fix guide settings panel | Fold into snapping/guide tutorial; UI fix. |
| [#1160](https://github.com/icosa-foundation/open-brush/pull/1160) | Put UNITY_LIBRARY_CACHE_PATHS in the proper step | No standalone demo: CI cache configuration. |
| [#1157](https://github.com/icosa-foundation/open-brush/pull/1157) | Unity 6 post-merge fixes 2 | No standalone demo: post-migration fixes. |
| [#1156](https://github.com/icosa-foundation/open-brush/pull/1156) | Fix publish jobs and some general build cleanup | No standalone demo: builds, publishing and compatibility. |
| [#1154](https://github.com/icosa-foundation/open-brush/pull/1154) | Unity6 post merge fixes | No standalone demo: post-migration fixes. |
| [#1153](https://github.com/icosa-foundation/open-brush/pull/1153) | Fix builds after caches are available in Unity 6+ | No standalone demo: CI builds. |
| [#1069](https://github.com/icosa-foundation/open-brush/pull/1069) | Unity 6 Kitchen Sink | Multiple candidates: Reshape, Tint, Quill import, splat controls; rendering migration itself needs no tool tutorial. |

## Excluded pending work

1. [#1175 Symmetry stroke linking](https://github.com/icosa-foundation/open-brush/pull/1175), [#1192 Native IMM import](https://github.com/icosa-foundation/open-brush/pull/1192) and [#1147 Animation timeline](https://github.com/icosa-foundation/open-brush/pull/1147) are not merged into main as standalone PRs at this snapshot. Do not promote their new workflows as shipped in this release interval.
2. [#1164 multipath Tool Script preview](https://github.com/icosa-foundation/open-brush/pull/1164) is separate from the merged single-path preview. Do not promise multipath/symmetry live preview in that tutorial.
3. The general viewer was already present before 2.32.0; #1068 merged in July. The candidates here concern newer navigation, handheld AR, build availability and bundled startup content.

## Suggested next production order

1. Membrane fill and the outlined variant: strong visual result and substantial reuse of the drawing pipeline.
2. Straight-edge snapping: a practical precision-drawing lesson.
3. Symmetry presets: visually distinctive, with preset selection reusable for Tool Script preview.
4. Expand the existing Reshape/Tint series while adding reusable slider/press interactions.
5. Quill import after choosing a suitable asset, then schedule the device-specific capture track.

