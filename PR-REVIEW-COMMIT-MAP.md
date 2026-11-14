# PR review comments and fix commits

Updated: 2026-10-24 03:55:31 UTC. Repository: [icosa-foundation/open-brush](https://github.com/icosa-foundation/open-brush).

## How to use this document

1. Use the branch index, then read the commit table for that branch. Every commit links to its GitHub diff; each R-number links to the matching original review comment, whose text appears below the table.
2. Review mappings require explicit commit references in GitHub replies. Resolution-reply links are included as evidence; mappings are not inferred from commit titles.
3. Commits without a matching GitHub thread link to their local branch review where available. Source-audit fixes, requested features and PR implementation are not presented as GitHub review findings.
4. Each commit is listed once and each linked original comment is reproduced once per branch. Decorative priority badge images, reaction footers and hidden bot metadata are removed; substantive review text is retained.

## Scope and counts

The refreshed map covers **286 unique commits across 25 matching PR worktrees**: 164 commits have explicit GitHub review links, and 122 do not. It includes 181 distinct original review comments.

| # | Batch | Commits |
|---|---|---:|
| 1 | Original review cleanup | 131 |
| 2 | Intermediate review recheck | 3 |
| 3 | Earlier branch-audit implementation | 40 |
| 4 | Earlier latest-review recheck | 8 |
| 5 | Subsequent source-audit, feature and review-thread work | 49 |
| 6 | Newly matched Maquette PR implementation | 4 |
| 7 | Current review recheck and later PR implementation | 48 |
| 8 | Additional review recheck | 3 |
| | **Total unique commits** | **286** |

Latest append retains all 283 preceding commits and 178 original review comments, and adds 3 commits with 3 newly quoted original review comments.

Earlier refresh: All 235 previously recorded commits and their review text are retained. This refresh adds 4 new review-fix commits and 44 later Maquette implementation/documentation commits. The earlier 235-commit refresh retained the original 182 entries and added 49 later commits on the original 24 branches plus Maquette PR #1204's first four commits. Added implementation work is not presented as review-driven bug fixes.

Main remains excluded. Four linked branches have no matching open PR: temp/pre-unity6, fix/local-build-generated-files, fix/handtracking-fixes-round-2 and integration/workshop-port. No tracked code or PR state is changed by this document.

## Branch index

| # | Branch | PR | Original | Intermediate | Audit | Earlier recheck | Update | Total | With review links |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | [feature/3d-shapes](#branch-3d-shapes) | [#862](https://github.com/icosa-foundation/open-brush/pull/862) | 0 | 0 | 4 | 0 | 2 | 6 | 0 |
| 2 | [feature/animation](#branch-animation) | [#1147](https://github.com/icosa-foundation/open-brush/pull/1147) | 22 | 0 | 2 | 0 | 7 | 31 | 26 |
| 3 | [feature/backdrop-passthrough-settings](#branch-backdrop-passthrough-settings) | [#1118](https://github.com/icosa-foundation/open-brush/pull/1118) | 9 | 0 | 1 | 0 | 2 | 12 | 9 |
| 4 | [features/brush-editing](#branch-brush-editing) | [#860](https://github.com/icosa-foundation/open-brush/pull/860) | 7 | 0 | 4 | 0 | 1 | 12 | 7 |
| 5 | [feature/dome-camera](#branch-dome-camera) | [#1114](https://github.com/icosa-foundation/open-brush/pull/1114) | 0 | 0 | 0 | 0 | 1 | 1 | 0 |
| 6 | [feature/editable-platform-settings](#branch-editable-platform-settings) | [#643](https://github.com/icosa-foundation/open-brush/pull/643) | 0 | 0 | 2 | 0 | 1 | 3 | 0 |
| 7 | [feature/fix-media-import-paths](#branch-fix-media-import-paths) | [#1121](https://github.com/icosa-foundation/open-brush/pull/1121) | 3 | 0 | 2 | 0 | 1 | 6 | 3 |
| 8 | [feature/gltf-export-profiles](#branch-gltf-export-profiles) | [#1117](https://github.com/icosa-foundation/open-brush/pull/1117) | 14 | 0 | 0 | 0 | 1 | 15 | 14 |
| 9 | [feature/icosa-collections](#branch-icosa-collections) | [#1095](https://github.com/icosa-foundation/open-brush/pull/1095) | 0 | 0 | 2 | 0 | 1 | 3 | 0 |
| 10 | [feature/inspector](#branch-inspector) | [#1150](https://github.com/icosa-foundation/open-brush/pull/1150) | 0 | 0 | 5 | 0 | 2 | 7 | 0 |
| 11 | [feature/jitter-plugins](#branch-jitter-plugins) | [#1027](https://github.com/icosa-foundation/open-brush/pull/1027) | 0 | 0 | 1 | 0 | 3 | 4 | 0 |
| 12 | [feature/mp-stroke-streaming](#branch-mp-stroke-streaming) | [#1137](https://github.com/icosa-foundation/open-brush/pull/1137) | 8 | 0 | 0 | 0 | 1 | 9 | 8 |
| 13 | [feature/multipath-toolscript-previews](#branch-multipath-toolscript-previews) | [#1164](https://github.com/icosa-foundation/open-brush/pull/1164) | 0 | 0 | 1 | 0 | 2 | 3 | 0 |
| 14 | [feature/native-imm](#branch-native-imm) | [#1192](https://github.com/icosa-foundation/open-brush/pull/1192) | 11 | 0 | 0 | 0 | 8 | 19 | 18 |
| 15 | [feature/polling-stroke-listener](#branch-polling-stroke-listener) | [#1025](https://github.com/icosa-foundation/open-brush/pull/1025) | 1 | 0 | 0 | 0 | 1 | 2 | 1 |
| 16 | [feature/sculpting](#branch-sculpting) | [#857](https://github.com/icosa-foundation/open-brush/pull/857) | 9 | 0 | 1 | 0 | 1 | 11 | 9 |
| 17 | [feature/sdf-guides](#branch-sdf-guides) | [#1119](https://github.com/icosa-foundation/open-brush/pull/1119) | 24 | 2 | 2 | 4 | 9 | 41 | 39 |
| 18 | [feature/sky-export](#branch-sky-export) | [#1122](https://github.com/icosa-foundation/open-brush/pull/1122) | 0 | 0 | 2 | 0 | 1 | 3 | 0 |
| 19 | [feature/straight-edge-snapping](#branch-straight-edge-snapping) | [#931](https://github.com/icosa-foundation/open-brush/pull/931) | 1 | 0 | 1 | 0 | 1 | 3 | 1 |
| 20 | [feature/symmetry-stroke-linking](#branch-symmetry-stroke-linking) | [#1175](https://github.com/icosa-foundation/open-brush/pull/1175) | 0 | 0 | 0 | 3 | 3 | 6 | 5 |
| 21 | [feature/text-tool](#branch-text-tool) | [#512](https://github.com/icosa-foundation/open-brush/pull/512) | 5 | 0 | 5 | 0 | 2 | 12 | 5 |
| 22 | [feature/texture-painting](#branch-texture-painting) | [#1111](https://github.com/icosa-foundation/open-brush/pull/1111) | 0 | 0 | 3 | 0 | 2 | 5 | 0 |
| 23 | [feature/voxel-editing](#branch-voxel-editing) | [#1024](https://github.com/icosa-foundation/open-brush/pull/1024) | 14 | 1 | 0 | 1 | 1 | 17 | 16 |
| 24 | [feature/websocket-client](#branch-websocket-client) | [#1113](https://github.com/icosa-foundation/open-brush/pull/1113) | 3 | 0 | 2 | 0 | 2 | 7 | 3 |
| 25 | [open-brush-maquette-compat](#branch-maquette-compat) | [#1204](https://github.com/icosa-foundation/open-brush/pull/1204) | 0 | 0 | 0 | 0 | 48 | 48 | 0 |

<a id="branch-3d-shapes"></a>

## 1. feature/3d-shapes — PR #862

[Pull request](https://github.com/icosa-foundation/open-brush/pull/862) · [Local branch review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>)

6 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [9dc74ec56](https://github.com/icosa-foundation/open-brush/commit/9dc74ec567ed20fc578e4b182af7c300160c9752) | Keep editable shape clone renderer maps on the clone | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>) |
| 2 | [78aa8ec01](https://github.com/icosa-foundation/open-brush/commit/78aa8ec013315c028adbe49420c9516adf8bc4ac) | Fit editable shape interaction bounds to offset and rebuilt meshes | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>) |
| 3 | [285b54461](https://github.com/icosa-foundation/open-brush/commit/285b544612f5d1c70781459595d1903fa0983e30) | Release generated shape preview meshes on rebuild and teardown | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>) |
| 4 | [f85b190fa](https://github.com/icosa-foundation/open-brush/commit/f85b190fae5aee3d69c9d5faeac0240dfe2cdabc) | Remove only the final JSON extension from shape preset paths | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>) |
| 5 | [f60349fe0](https://github.com/icosa-foundation/open-brush/commit/f60349fe04f111868c69f35dac8564ee1aa1f263) | Release regenerated editable-model meshes on replacement | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>) |
| 6 | [4f8fae7dc](https://github.com/icosa-foundation/open-brush/commit/4f8fae7dc52c034bd4b16d932f03dafedf212bd0) | Preserve shape presets and preview state on failed save or load | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-3d-shapes/BRANCH-REVIEW.md>) |

<a id="branch-animation"></a>

## 2. feature/animation — PR #1147

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1147) · [Local branch review](<F:/Unity Projects/open-brush-animation/BRANCH-REVIEW.md>)

31 commits; 31 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [9f4ef1257](https://github.com/icosa-foundation/open-brush/commit/9f4ef1257bf0abaf8cbc88ae168b5679d8229877) | Keep transform scratch canvases outside the animation timeline | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129359), [R20](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531980) |
| 2 | [efb94d5ab](https://github.com/icosa-foundation/open-brush/commit/efb94d5abaaa0a55a7ed9560927ec5f0efe337f8) | Preserve model widget canvas when deferred loading completes | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129368), [R21](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531985) |
| 3 | [91c91f4fd](https://github.com/icosa-foundation/open-brush/commit/91c91f4fd768acb678aca3fdaa4aeed3bf1c255e) | Ignore parentless widgets during timeline registration | Original | [R27](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4171082976) |
| 4 | [4accf0855](https://github.com/icosa-foundation/open-brush/commit/4accf085565ee884754f1db5ab54944c502536fc) | Avoid subtracting hidden stroke cost when destroying canvases | Original | [R17](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3980000181) |
| 5 | [8da8fec90](https://github.com/icosa-foundation/open-brush/commit/8da8fec906ec08c4348805e958110c484f05ccd7) | Synchronize explicit layer visibility with animation tracks | Original | [R11](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3959825460) |
| 6 | [58b584c0e](https://github.com/icosa-foundation/open-brush/commit/58b584c0e4e7039f3ed9eed2cbd578275ede3b72) | Clamp the timeline playhead when reducing the first frame | Original | [R26](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4121872889) |
| 7 | [a8b0c3111](https://github.com/icosa-foundation/open-brush/commit/a8b0c311163b398ba190aab2d0bde654f7db98fb) | Keep camera capture active and target paths in the current tool mode | Original | [R10](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3953112640), [R25](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4121872880) |
| 8 | [115e75af9](https://github.com/icosa-foundation/open-brush/commit/115e75af9ce256b023ec0ff5efd64938c5aed072) | Capture animation metadata before timesliced stroke snapshots yield | Original | [R19](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3983404338) |
| 9 | [c4f0cce4b](https://github.com/icosa-foundation/open-brush/commit/c4f0cce4b56fb4363c764618185578b807a83e66) | Serialize selected widgets using their authored animation canvas | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935463) |
| 10 | [fbe98e6b9](https://github.com/icosa-foundation/open-brush/commit/fbe98e6b9a1bd7b426fa972f582fb79860759e37) | Persist text widget animation layer and frame locations | Original | [R15](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544029) |
| 11 | [f2532ad1a](https://github.com/icosa-foundation/open-brush/commit/f2532ad1a85a117b1a5087d109c878e7738816f9) | Save and restore guide widgets on their animation frames | Original | [R24](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4083384768) |
| 12 | [545af4389](https://github.com/icosa-foundation/open-brush/commit/545af43893b823d2c84b645bb675dacafce7984c) | Initialize deferred media canvases with current frame visibility | Original | [R14](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544016) |
| 13 | [afc326b29](https://github.com/icosa-foundation/open-brush/commit/afc326b298326acc449731891aa6928097c34915) | Rename every drawing canvas in an animation layer | Original | [R12](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3959825466) |
| 14 | [de39ba92c](https://github.com/icosa-foundation/open-brush/commit/de39ba92c3208903803ec964a04010e62ecee062) | Save only live nonempty animation paths at their current timeline location | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916977552), [R7](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916977562), [R18](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3983404326) |
| 15 | [71f66c64c](https://github.com/icosa-foundation/open-brush/commit/71f66c64c0cae577d03b3c527e4b17cd1fd93482) | Detach and restore animation paths with widget creation undo | Original | [R9](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3953112636) |
| 16 | [571d51a4b](https://github.com/icosa-foundation/open-brush/commit/571d51a4bfac70ff1a3125701186f1f6d1620437) | Unregister and account for widgets before destroying drawing canvases | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935482) |
| 17 | [9a494e3da](https://github.com/icosa-foundation/open-brush/commit/9a494e3da065cb183224933547bbb36c7177f33f) | Preserve pin group and media playback state in copied animation drawings | Original | [R23](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754956) |
| 18 | [27e94b85c](https://github.com/icosa-foundation/open-brush/commit/27e94b85cbcc9bc1a36b9c7deb102a60e82f7916) | Keep temporary animation planning documents untracked | Original | [R22](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754948) |
| 19 | [c4e53ecb2](https://github.com/icosa-foundation/open-brush/commit/c4e53ecb26b42e775cb778bc761c1ee14f32c585) | Compose animation path motion with the drawing base pose and save that pose | Original | [R16](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3980000162) |
| 20 | [0cbabef5d](https://github.com/icosa-foundation/open-brush/commit/0cbabef5df80e7d9c9d28efc6e570ab1ac8b622e) | Keep the canonical main canvas stable while projecting track zero | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3952875458) |
| 21 | [c8750f25c](https://github.com/icosa-foundation/open-brush/commit/c8750f25cb462e2090defbf7e4076589179f7843) | Squash complete animation tracks across source and destination span boundaries | Original | [R13](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544002) |
| 22 | [0cbc16fa6](https://github.com/icosa-foundation/open-brush/commit/0cbc16fa667eef033e92807973913d24deec6d02) | Copy per-point colors into each memorized stroke | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3915196421) |
| 23 | [7e7416890](https://github.com/icosa-foundation/open-brush/commit/7e741689049cbd8a15035e1ee51f99f9f80c7837) | Guard the animation editor namespace import in player builds | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-animation/BRANCH-REVIEW.md>) |
| 24 | [0d98b791a](https://github.com/icosa-foundation/open-brush/commit/0d98b791af29eb53c8a74843d7134ca213543220) | Avoid false stroke count warnings when duplicating animation frames | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-animation/BRANCH-REVIEW.md>) |
| 25 | [ef9bf78a6](https://github.com/icosa-foundation/open-brush/commit/ef9bf78a6a81f3e8d823e605f043c5eeec9d9b81) | Fix animation test initialization and widget registration | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-animation/BRANCH-REVIEW.md>) |
| 26 | [f602468ea](https://github.com/icosa-foundation/open-brush/commit/f602468ea67b8c8dc74a9dd7ea7afa597544a284) | Preserve model frame coordinates through selection and reload | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-animation/BRANCH-REVIEW.md>) |
| 27 | [280e83ef9](https://github.com/icosa-foundation/open-brush/commit/280e83ef92080c525f87a4ddb5578b55fc5e92cb) | Preserve media widget frame coordinates through save and reload | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-animation/BRANCH-REVIEW.md>) |
| 28 | [bbbec6ddc](https://github.com/icosa-foundation/open-brush/commit/bbbec6ddc9989f556e5c0e54e451e5d3bac7e49f) | Keep animation performance planning documents local | Update | [R29](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4361387645) |
| 29 | [cfc625f32](https://github.com/icosa-foundation/open-brush/commit/cfc625f32b036416b1fac7913581d972c6f178cb) | Copy frame strokes directly into their destination canvas | Update | [R30](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364338488) |
| 30 | [2db92535c](https://github.com/icosa-foundation/open-brush/commit/2db92535c182ec6e87cb7c02111831987e903369) | Detach erased animation paths and restore their attachment on undo | Update | [R31](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364338498) |
| 31 | [81938b7cc](https://github.com/icosa-foundation/open-brush/commit/81938b7cc4a35f8363267da6301858d963ec626f) | Serialize every selected widget in its authored animation frame | Update | [R28](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4361387637) |

### Review comment text

#### R1 — Assets/Scripts/SceneScript.cs:270

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129359) · Author: chatgpt-codex-connector · Created: 2026-09-02T12:37:02Z · Resolved

> **[P1]  Keep temporary transform canvases out of the timeline**
> 
> `TransformItems.Transform`, `TransformSelected`, and `TransformList` call `AddLayerNow()` solely to create a temporary canvas and then destroy it, without restoring `ActiveCanvas`. These new unconditional side effects therefore leave `ActiveCanvas` pointing at a destroyed object and register a timeline track whose canvas has been destroyed after every selection/item transform. Use a transient-canvas path for these callers, or explicitly remove the track and restore the previous active canvas.

Mapped commits: [9f4ef1257](https://github.com/icosa-foundation/open-brush/commit/9f4ef1257bf0abaf8cbc88ae168b5679d8229877).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342064286) explicitly references the mapped commit.

#### R2 — Assets/Scripts/Widgets/ModelWidget.cs:291

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129368) · Author: chatgpt-codex-connector · Created: 2026-09-02T12:37:02Z · Resolved · Outdated code location

> **[P1]  Preserve a model widget's existing canvas while loading**
> 
> When a deferred model finishes loading, `OnPacCatalogChanged()` assigns `Model` and reaches this line. If the user has switched animation frames or layers while the asset was downloading, the widget is silently moved from its saved canvas to the currently active canvas; the animation ownership index is not notified either. `LoadModel()` should instantiate the model contents without changing the widget's parent.

Mapped commits: [efb94d5ab](https://github.com/icosa-foundation/open-brush/commit/efb94d5abaaa0a55a7ed9560927ec5f0efe337f8).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342065666) explicitly references the mapped commit.

#### R3 — Assets/Scripts/PointerScript.cs:1202

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3915196421) · Author: chatgpt-codex-connector · Created: 2026-09-02T14:29:55Z · Resolved · Outdated code location

> **[P2]  Give each completed stroke its own color list**
> 
> For a batched colored stroke, this passes the pointer's mutable `m_ControlPointColors` list directly into the stored `Stroke`, while `DetachLine` clears only `m_ControlPoints` and never resets the color list. Drawing another colored stroke consequently appends into the previous stroke's saved data and starts the new stroke with all earlier colors; copy the list when memorizing and reset it when the line ends.

Mapped commits: [0cbc16fa6](https://github.com/icosa-foundation/open-brush/commit/0cbc16fa667eef033e92807973913d24deec6d02).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342073162) explicitly references the mapped commit.

#### R4 — Assets/Scripts/Save/MetadataUtils.cs:133

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935463) · Author: chatgpt-codex-connector · Created: 2026-09-02T17:38:59Z · Resolved · Outdated code location

> **[P1]  Serialize selected widgets from their previous canvas**
> 
> When a snapshot is taken while a model remains selected—notably during autosave, which does not call `ClearActiveSelection()`—`widget.Canvas` is the selection canvas, which has no timeline location, so this records `(-1, -1)`. Reloading then falls back to the main canvas and moves the model out of its original animation frame; the newly added frame serializers for images, videos, lights, portals, captures, and sound clips use the same pattern. Resolve the selection canvas through `m_PreviousCanvas`, as the stroke snapshot path already does.

Mapped commits: [c4f0cce4b](https://github.com/icosa-foundation/open-brush/commit/c4f0cce4b56fb4363c764618185578b807a83e66).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342079478) explicitly references the mapped commit.

#### R5 — Assets/Scripts/Animation/AnimationUI_Manager.cs:997

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935482) · Author: chatgpt-codex-connector · Created: 2026-09-02T17:38:59Z · Resolved

> **[P2]  Clean up widgets before destroying timeline canvases**
> 
> When undoing a split or duplicated frame containing widgets, this destroys the cloned canvas and its child widgets directly. `SceneScript.DestroyCanvas()` only removes strokes and batches, while `GrabWidget.OnDestroy()` does not unregister the widget or subtract its tilt-meter cost; model/image vertex bookkeeping also relies on `UnregisterGrabWidget()`. Repeating duplicate/split and undo therefore accumulates dead widget entries and inflated complexity/vertex counts, eventually rejecting otherwise valid content.

Mapped commits: [571d51a4b](https://github.com/icosa-foundation/open-brush/commit/571d51a4bfac70ff1a3125701186f1f6d1620437).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342080055) explicitly references the mapped commit.

#### R6 — Assets/Scripts/CameraPath.cs:1801

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916977552) · Author: chatgpt-codex-connector · Created: 2026-09-02T17:43:39Z · Resolved

> **[P1]  Resolve animation-path locations from the current timeline**
> 
> When a frame containing an animation path is moved, or an earlier frame is inserted or extended, the timeline moves the `PathToken` but never updates `CameraPath.timelineLocation`. Serializing that stale field here records the path at its old frame, so reopening the sketch attaches the camera animation to the wrong drawing. Resolve the path's current location from the timeline (or update every affected path location after timeline edits) before serializing.

Mapped commits: [de39ba92c](https://github.com/icosa-foundation/open-brush/commit/de39ba92c3208903803ec964a04010e62ecee062).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342082625) explicitly references the mapped commit.

#### R7 — Assets/Scripts/Save/MetadataUtils.cs:96

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916977562) · Author: chatgpt-codex-connector · Created: 2026-09-02T17:43:39Z · Resolved

> **[P2]  Exclude deleted animation paths from saved metadata**
> 
> After deleting a frame with an animation path, the path widget remains active while the delete command is retained for undo; it is only hidden when that undo state is eventually disposed. `AnimationPathWidgets` therefore still returns it here, even though no current timeline frame references it, so saving before the command leaves the undo stack writes the deleted path and loading the sketch resurrects it at its former location. Serialize only paths referenced by the current timeline.

Mapped commits: [de39ba92c](https://github.com/icosa-foundation/open-brush/commit/de39ba92c3208903803ec964a04010e62ecee062).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342083308) explicitly references the mapped commit.

#### R8 — Assets/Scripts/Animation/AnimationUI_Manager.cs:2293

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3952875458) · Author: chatgpt-codex-connector · Created: 2026-09-07T22:19:06Z · Resolved

> **[P1]  Keep the canonical main canvas stable across frames**
> 
> When track 0 advances beyond frame 0, this replaces the scene's canonical `MainCanvas` reference with that frame's drawing canvas. Systems such as `PointerManager` and `CameraPathCaptureRig` subscribe once to the original main canvas's `PoseChanged` event, while `SceneScript.Pose` now notifies only canvases reachable through the replacement reference, so world-grabbing on a later frame skips those handlers; a subsequent `StartTimeline()` can also destroy the original canvas. Keep the startup main canvas stable and represent the current track-0 drawing separately, or explicitly rebind every main-canvas consumer.

Mapped commits: [0cbabef5d](https://github.com/icosa-foundation/open-brush/commit/0cbabef5df80e7d9c9d28efc6e570ab1ac8b622e).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342084088) explicitly references the mapped commit.

#### R9 — Assets/Scripts/Tools/CameraPathTool.cs:237

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3953112636) · Author: chatgpt-codex-connector · Created: 2026-09-07T23:25:57Z · Resolved

> **[P1]  Make timeline attachment part of animation-path undo**
> 
> When a new animation path is created, `CreatePathWidget()` records only a `CreateWidgetCommand`, while this subsequent `AddAnimationPath()` mutation has no corresponding undo operation. Undoing the knot and widget creation merely hides the widget, leaving the timeline span's `PathToken` attached; `IsSparseFrameFilled()` consequently continues treating the frame as occupied, and disposal can leave a destroyed object referenced by the timeline. Attach and detach the path through the same undoable command.

Mapped commits: [71f66c64c](https://github.com/icosa-foundation/open-brush/commit/71f66c64c0cae577d03b3c527e4b17cd1fd93482).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342088792) explicitly references the mapped commit.

#### R10 — Assets/Scripts/Tools/CameraPathTool.cs:531

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3953112640) · Author: chatgpt-codex-connector · Created: 2026-09-07T23:25:57Z · Resolved · Outdated code location

> **[P2]  Restrict knot targeting to the active path mode**
> 
> When a normal camera path and an animation path are both visible, this unfiltered collection allows proximity testing to select either kind regardless of whether the tool is in `AddPositionKnot` or `AddAnimationPositionKnot` mode. The subsequent switch cases then add or extend knots on `m_LastValidPath`, so using the animation controls near a camera path can silently edit the camera path, and vice versa. Select `CameraPathWidgets` or `AnimationPathWidgets` according to the active mode, reserving the combined set for operations intended to affect both.

Mapped commits: [a8b0c3111](https://github.com/icosa-foundation/open-brush/commit/a8b0c311163b398ba190aab2d0bde654f7db98fb).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342089562) explicitly references the mapped commit.

#### R11 — Assets/Scripts/SceneScript.cs:448

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3959825460) · Author: chatgpt-codex-connector · Created: 2026-09-08T15:58:38Z · Resolved

> **[P2]  Synchronize direct visibility changes with the timeline**
> 
> When `layer.hide` or the Lua layer wrapper calls this method, only the canvas for the currently displayed frame is deactivated; the new authoritative `Track.Visible` value remains unchanged. The next `FocusFrame` therefore shows the layer again, and saving writes the track as visible so a hidden layer also reappears after reload. Route both `ShowLayer` and `HideLayer` through the same timeline visibility update used by `ToggleLayerVisibility`.

Mapped commits: [8da8fec90](https://github.com/icosa-foundation/open-brush/commit/8da8fec906ec08c4348805e958110c484f05ccd7).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342092347) explicitly references the mapped commit.

#### R12 — Assets/Scripts/SceneScript.cs:477

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3959825466) · Author: chatgpt-codex-connector · Created: 2026-09-08T15:58:38Z · Resolved · Outdated code location

> **[P2]  Keep layer names consistent across frame canvases**
> 
> After a layer has multiple drawings from split or duplicate operations, each frame owns a separate canvas, but `RenameLayer` changes only the currently active canvas before issuing this update. Switching to an older frame restores its previous name, and whichever frame is active during save determines the serialized layer name. Store the name on the logical track or propagate renames to every canvas belonging to that track.

Mapped commits: [afc326b29](https://github.com/icosa-foundation/open-brush/commit/afc326b298326acc449731891aa6928097c34915).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342092919) explicitly references the mapped commit.

#### R13 — Assets/Scripts/Layers/AnimationLayerUI_Manager.cs:203

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544002) · Author: chatgpt-codex-connector · Created: 2026-09-09T12:51:44Z · Resolved

> **[P1]  Squash every frame in the source animation track**
> 
> When the source track has drawings on multiple frames, this passes only the currently displayed source and destination canvases to the legacy `SquashLayerCommand`. That command moves only content parented to the current canvas, but its subsequent `MarkLayerAsDeleted` call deletes the entire logical track; content on the source track's other frames is then serialized with invalid locations or remapped to the main canvas. Make squash operate pairwise across every frame/span before deleting the source track.

Mapped commits: [c8750f25c](https://github.com/icosa-foundation/open-brush/commit/c8750f25cb462e2090defbf7e4076589179f7843).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342095602) explicitly references the mapped commit.

#### R14 — Assets/Scripts/Save/SaveLoadScript.cs:929

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544016) · Author: chatgpt-codex-connector · Created: 2026-09-09T12:51:44Z · Resolved

> **[P1]  Refocus the timeline after deferred media creation**
> 
> When a loaded animation has a model, image, video, or light on a nonzero frame, these calls run immediately after scheduling `CreateMediaWidgetsFromLoadDataCoroutine`, before the compositor coroutine creates those widgets. The later `GetOrCreateContentCanvas` call copies the active state of the track's shared empty canvas, and nothing reapplies frame visibility afterward, so media from later frames remains visible on frame 0 when loading completes. Run the final focus after the media coroutine finishes, or initialize newly materialized canvases according to the current frame.

Mapped commits: [545af4389](https://github.com/icosa-foundation/open-brush/commit/545af43893b823d2c84b645bb675dacafce7984c).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342096140) explicitly references the mapped commit.

#### R15 — Assets/Scripts/Save/SketchSnapshot.cs:184

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544029) · Author: chatgpt-codex-connector · Created: 2026-09-09T12:51:44Z · Resolved

> **[P2]  Persist animation locations for text widgets**
> 
> When a `TextWidget` is placed or cloned onto a nonzero animation frame, the new timeline metadata cannot associate it with that frame: `GetTiltText` emits no frame coordinate and does not assign even the existing `TiltText.LayerId`, while `TextWidget.FromTiltText` uses the one-argument layer lookup. Saving and reopening therefore moves such text to track 0/frame 0. Add frame-location fields and use `GetSerializableIndexOfCanvas` for text just as for the other widget types.

Mapped commits: [fbe98e6b9](https://github.com/icosa-foundation/open-brush/commit/fbe98e6b9a1bd7b426fa972f582fb79860759e37).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342096823) explicitly references the mapped commit.

#### R16 — Assets/Scripts/Animation/AnimationUI_Manager.cs:3166

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3980000162) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:14:31Z · Resolved · Outdated code location

> **[P1]  Preserve the canvas's base pose when applying path motion**
> 
> When an animated layer has a non-identity `LocalPose`, scrubbing or playing its animation replaces that pose with only the path-relative delta; at the path start, `posDifference` is identity, so the layer immediately snaps to the origin and loses its original translation, rotation, and scale. Because layer serialization later reads this mutated `LocalPose`, saving after playback can make the displacement permanent. Store the layer's base pose and compose the path delta with it instead of overwriting it.

Mapped commits: [c4e53ecb2](https://github.com/icosa-foundation/open-brush/commit/c4e53ecb26b42e775cb778bc761c1ee14f32c585).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342098723) explicitly references the mapped commit.

#### R17 — Assets/Scripts/SceneScript.cs:292

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3980000181) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:14:32Z · Resolved · Outdated code location

> **[P2]  Avoid charging hidden strokes twice during canvas destruction**
> 
> When an undone stroke's redo is discarded after the user switches to another frame, `BrushStrokeCommand.OnDispose()` removes that still-disabled stroke; the resulting drawing demotion can destroy its inactive canvas through this method. The stroke's undo already decremented the tilt meter, but this type-only check decrements it again despite `IsGeometryEnabled` being false, leaving the complexity meter below the actual sketch cost and allowing content beyond the configured budget. Only subtract strokes whose geometry is currently accounted for.

Mapped commits: [4accf0855](https://github.com/icosa-foundation/open-brush/commit/4accf085565ee884754f1db5ab54944c502536fc).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342099354) explicitly references the mapped commit.

#### R18 — Assets/Scripts/Save/MetadataUtils.cs:96

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3983404326) · Author: chatgpt-codex-connector · Created: 2026-09-10T20:54:09Z · Resolved

> **[P2]  Skip empty animation paths when saving**
> 
> When the first position-knot command is undone, the animation-path widget remains active with zero knots, so this unfiltered enumeration serializes it even though `GetCameraPaths()` deliberately applies `ShouldSerialize()`. Reloading attaches that empty path to the timeline, and scrubbing or playback reaches `UpdateLayerTransforms()`, whose `GetPosition()` call indexes an empty `PositionKnots` list. Apply the same serialization filter to animation paths.

Mapped commits: [de39ba92c](https://github.com/icosa-foundation/open-brush/commit/de39ba92c3208903803ec964a04010e62ecee062).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342099909) explicitly references the mapped commit.

#### R19 — Assets/Scripts/Save/SketchSnapshot.cs:136

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3983404338) · Author: chatgpt-codex-connector · Created: 2026-09-10T20:54:09Z · Resolved · Outdated code location

> **[P2]  Capture timeline metadata before yielding**
> 
> During autosave, this coroutine yields repeatedly while the application remains interactive, but the stroke serializer has already captured its canvas-to-frame map before `GetSketchMetadata()` reads the live timeline here. If the user moves or deletes a frame during that interval, the binary strokes retain their old frame coordinates while `AnimationTracks` records the new spans; reopening the autosave can therefore restore strokes to the wrong frame or fall back to the main canvas. Use one immutable timeline snapshot for both stroke locations and metadata.

Mapped commits: [115e75af9](https://github.com/icosa-foundation/open-brush/commit/115e75af9ce256b023ec0ff5efd64938c5aed072).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342101800) explicitly references the mapped commit.

#### R20 — Assets/Scripts/SceneScript.cs:270

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531980) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:15Z · Resolved

> **[P1]  Keep scratch layers out of the active timeline**
> 
> `AddLayerNow()` is also used by all three `TransformItems` helpers to create a temporary scratch canvas that they destroy immediately afterward. Activating and registering that canvas here means every selection/item transform leaves `App.Scene.ActiveCanvas` pointing at a destroyed object and retains an empty track in the sparse timeline, which can reappear as a ghost layer on the next projection; undo and redo repeat the leak. Preserve the old scratch-safe behavior or make those callers use a canvas creation path that neither activates nor registers a logical layer.

Mapped commits: [9f4ef1257](https://github.com/icosa-foundation/open-brush/commit/9f4ef1257bf0abaf8cbc88ae168b5679d8229877).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342102431) explicitly references the mapped commit.

#### R21 — Assets/Scripts/Widgets/ModelWidget.cs:299

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531985) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:15Z · Resolved · Outdated code location

> **[P1]  Keep asynchronously loaded models on their original canvas**
> 
> When `model.import` creates a widget, `SetupWidgetAfterLoadAsync` assigns its `Model` only after awaiting the file load. If the user changes layers or timeline frames during that wait, this reparents the existing widget to whichever canvas is active at completion rather than the canvas where the command created it; because this also bypasses `SetCanvas`, the animation ownership index is not updated consistently. The model consequently disappears from its authored frame and can be saved into the newly selected one.

Mapped commits: [efb94d5ab](https://github.com/icosa-foundation/open-brush/commit/efb94d5abaaa0a55a7ed9560927ec5f0efe337f8).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342103198) explicitly references the mapped commit.

#### R22 — ANIMATION-SCALABILITY-PLAN.md:1

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754948) · Author: chatgpt-codex-connector · Created: 2026-09-14T13:39:01Z · Resolved · Outdated code location

> **[P1]  Remove the temporary animation planning documents**
> 
> This commit versions `ANIMATION-SCALABILITY-PLAN.md` and `ANIMATION-TASKS.md`, which explicitly track incomplete phases, pending gates, and future work. These documents must remain local and untracked unless the user explicitly requests versioned project documentation. Remove them from the commit.
> 
> AGENTS.md reference: [AGENTS.md:L3-L3](https://github.com/icosa-foundation/open-brush/blob/d8372349164daad1eb2500314907d13ae2394afb/AGENTS.md#L3-L3)

Mapped commits: [27e94b85c](https://github.com/icosa-foundation/open-brush/commit/27e94b85cbcc9bc1a36b9c7deb102a60e82f7916).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342105088) explicitly references the mapped commit.

#### R23 — Assets/Scripts/Animation/AnimationUI_Manager.cs:2699

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754956) · Author: chatgpt-codex-connector · Created: 2026-09-14T13:39:01Z · Resolved

> **[P2]  Preserve widget state when copying frame drawings**
> 
> When splitting or duplicating a frame containing widgets, this delegates copying to each widget's ordinary `Clone()` implementation, but prefab-instantiating clones such as `ModelWidget.Clone`, `ImageWidget.Clone`, and `SoundClipWidget.Clone` do not restore `Pinned` or `Group` (and media clones also omit playback position/paused state). A split held frame therefore changes the later portion from the original widget state to unpinned, ungrouped, and potentially restarted media; explicitly copy persistent widget state as part of frame replication.

Mapped commits: [9a494e3da](https://github.com/icosa-foundation/open-brush/commit/9a494e3da065cb183224933547bbb36c7177f33f).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342105693) explicitly references the mapped commit.

#### R24 — Assets/Scripts/Animation/AnimationUI_Manager.cs:2699

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4083384768) · Author: chatgpt-codex-connector · Created: 2026-09-23T14:11:28Z · Resolved

> **[P2]  Persist cloned guides at their animation frame**
> 
> When a split or duplicate contains a `StencilWidget`, this generic clone path creates a second guide on the new frame, but `StencilWidget.GetSaveState()` records neither its layer nor a frame coordinate and `SaveState` loads it through the layer-only overload. Saving and reopening therefore moves both the original and cloned guides to track 0/frame 0, so guide placement in the duplicated drawing is lost; either add frame-aware guide serialization or exclude guides from frame replication until it exists.

Mapped commits: [f2532ad1a](https://github.com/icosa-foundation/open-brush/commit/f2532ad1a85a117b1a5087d109c878e7738816f9).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342106147) explicitly references the mapped commit.

#### R25 — Assets/Scripts/Tools/CameraPathTool.cs:237

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4121872880) · Author: chatgpt-codex-connector · Created: 2026-09-28T11:59:29Z · Resolved · Outdated code location

> **[P1]  Keep the camera capture rig active for later recordings**
> 
> After creating an animation path, this disables the entire `CameraPathCaptureRig` GameObject rather than just hiding its preview. If the user then selects an existing normal camera path, `RefreshVisibility()` can make the child capture object report `Enabled`, but the rig hierarchy remains inactive; `RecordCameraPath` still calls `RecordPath()`, leaving the recording camera inactive and producing a failed or blank capture. Hide the preview/capture object or restore the rig whenever a normal path becomes current instead of disabling the rig root.

Mapped commits: [a8b0c3111](https://github.com/icosa-foundation/open-brush/commit/a8b0c311163b398ba190aab2d0bde654f7db98fb).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342108719) explicitly references the mapped commit.

#### R26 — Assets/Scripts/Animation/AnimationUI_Manager.cs:2533

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4121872889) · Author: chatgpt-codex-connector · Created: 2026-09-28T11:59:29Z · Resolved · Outdated code location

> **[P2]  Clamp the stored playhead when reducing frame zero**
> 
> When reducing a held span while the playhead is on frame 0, this leaves the backing `m_FrameOn` value at `-1`; the `FrameOn` property only clamps reads and does not repair that state. The subsequent refresh therefore displays frame `0 / N`, and extending again increments from `-1` to `0` instead of advancing into the extended span. Clamp or explicitly assign the resulting valid frame before refreshing.

Mapped commits: [58b584c0e](https://github.com/icosa-foundation/open-brush/commit/58b584c0e4e7039f3ed9eed2cbd578275ede3b72).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342109439) explicitly references the mapped commit.

#### R27 — Assets/Scripts/WidgetManager.cs:1477

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4171082976) · Author: chatgpt-codex-connector · Created: 2026-10-03T01:20:02Z · Resolved

> **[P1]  Defer widget timeline notification until parenting**
> 
> For active image, text, video, and similar prefabs instantiated after `WidgetManager.Init`, `GrabWidget.Awake()` registers the widget before `CreateWidgetCommand` assigns its canvas parent. This new callback then reaches `NotifyWidgetAdded()`, which evaluates `widget.Canvas` through `transform.parent.GetComponent<CanvasScript>()` and throws because the parent is null. Besides logging an exception for ordinary widget creation, the aborted notification leaves widget-only animation frames represented as empty drawings, allowing later timeline rebuilding to destroy or omit their canvas; notify after parenting or make the callback tolerate parentless widgets.

Mapped commits: [91c91f4fd](https://github.com/icosa-foundation/open-brush/commit/91c91f4fd768acb678aca3fdaa4aeed3bf1c255e).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4342110082) explicitly references the mapped commit.


#### R28 — Assets/Scripts/Save/MetadataUtils.cs:188

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4361387637) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:45:02Z · Resolved

> **[P1] Convert selected widget poses to the saved frame**
>
> When autosave captures a selected text widget after its selection has been moved, `GetSerializableIndexOfCanvas()` correctly records `m_PreviousCanvas`, but `SaveTransform` remains local to `SelectionCanvas`; loading therefore interprets that pose relative to the original frame and moves the widget. The model serializer above explicitly applies `savedCanvas.Pose.inverse * currentCanvas.Pose`, but text, image, video, sound, light, portal, capture, and guide serializers omit that conversion, so apply it consistently for selected widgets.

Mapped commits: [81938b7cc](https://github.com/icosa-foundation/open-brush/commit/81938b7cc4a35f8363267da6301858d963ec626f).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364788921) cites [81938b7cc](https://github.com/icosa-foundation/open-brush/commit/81938b7cc4a35f8363267da6301858d963ec626f).

#### R29 — ANIMATION-PERFORMANCE-WORKLOADS.md:6

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4361387645) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:45:02Z · Resolved · Outdated code location

> **[P1] Remove the remaining temporary performance plans**
>
> Remove this newly committed workload protocol and the companion baseline document: they track an incomplete phase, required future captures, and open completion gates, and this file even references the now-untracked `ANIMATION-SCALABILITY-PLAN.md`. These are the same category of temporary planning/future-work artifacts that repository policy requires keeping local and untracked.
>
> AGENTS.md reference: [AGENTS.md:L3-L3](https://github.com/icosa-foundation/open-brush/blob/f602468ea67b8c8dc74a9dd7ea7afa597544a284/AGENTS.md#L3-L3)

Mapped commits: [bbbec6ddc](https://github.com/icosa-foundation/open-brush/commit/bbbec6ddc9989f556e5c0e54e451e5d3bac7e49f).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364771572) cites [bbbec6ddc](https://github.com/icosa-foundation/open-brush/commit/bbbec6ddc9989f556e5c0e54e451e5d3bac7e49f).

#### R30 — Assets/Scripts/Animation/AnimationUI_Manager.cs:2638

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364338488) · Author: chatgpt-codex-connector · Created: 2026-10-23T21:21:24Z · Resolved · Outdated code location

> **[P2] Copy frame strokes directly into the destination canvas**
>
> When the source drawing and `SelectionCanvas` have different poses, splitting or duplicating a frame changes its stroke placement. `DuplicateStroke(..., null)` copies canvas-local control points and geometry without a coordinate conversion, but the later `SetParentKeepWorldPosition(newCanvas)` interprets those copies relative to `SelectionCanvas.Pose`, not `oldCanvas.Pose`. The copied strokes therefore acquire the selection canvas's transform, while cloned widgets preserve their original world positions. Duplicate directly into the new canvas, preserving source-local coordinates, rather than passing through the unrelated selection canvas.

Mapped commits: [cfc625f32](https://github.com/icosa-foundation/open-brush/commit/cfc625f32b036416b1fac7913581d972c6f178cb).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364776744) cites [cfc625f32](https://github.com/icosa-foundation/open-brush/commit/cfc625f32b036416b1fac7913581d972c6f178cb).

#### R31 — Assets/Scripts/Animation/AnimationUI_Manager.cs:3255

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364338498) · Author: chatgpt-codex-connector · Created: 2026-10-23T21:21:24Z · Resolved

> **[P2] Detach animation paths when deleting their widgets**
>
> When an existing animation path is erased or tossed, `HideWidgetCommand` hides its widget but never calls `DetachAnimationPath`; only undoing widget creation currently performs that detachment. The timeline consequently retains its `PathToken`, and this check continues evaluating the hidden path during playback, so deleting the path does not stop the layer's motion. The stale token also keeps the frame occupied through `IsSparseFrameFilled`. Make ordinary path deletion detach the timeline reference and restore it on undo.

Mapped commits: [2db92535c](https://github.com/icosa-foundation/open-brush/commit/2db92535c182ec6e87cb7c02111831987e903369).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4364780678) cites [2db92535c](https://github.com/icosa-foundation/open-brush/commit/2db92535c182ec6e87cb7c02111831987e903369).

<a id="branch-backdrop-passthrough-settings"></a>

## 3. feature/backdrop-passthrough-settings — PR #1118

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1118) · [Local branch review](<F:/Unity Projects/open-brush-backdrop-passthrough-settings/BRANCH-REVIEW.md>)

12 commits; 7 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [201fe4984](https://github.com/icosa-foundation/open-brush/commit/201fe49849fe7ff26a681b2e4236553f23318f85) | Reference the packaged passthrough hull material | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391656) |
| 2 | [317e73442](https://github.com/icosa-foundation/open-brush/commit/317e7344203ca5d6134bbb569c06e877d6b18161) | Scope persistent button highlights to backdrop controls | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391721) |
| 3 | [cf98d0d5d](https://github.com/icosa-foundation/open-brush/commit/cf98d0d5d9227162a5b1328d53bf6eb437ddde46) | Hide unfinished passthrough controls and remove missing callbacks | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391686) |
| 4 | [da4cdf0ec](https://github.com/icosa-foundation/open-brush/commit/da4cdf0ecb7025a875865fed43ddef53c0919771) | Apply multiplayer passthrough guard to direct backdrop changes | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391712) |
| 5 | [21f73353b](https://github.com/icosa-foundation/open-brush/commit/21f73353b99e485bddbe37bc371c85ce0e874961) | Notify backdrop UI from authoritative mode setters | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391731) |
| 6 | [864e253a8](https://github.com/icosa-foundation/open-brush/commit/864e253a8336cd41d1506b1a97c0161df184826c) | Restore fog and skybox rendering after passthrough | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391735) |
| 7 | [4ef2f1da7](https://github.com/icosa-foundation/open-brush/commit/4ef2f1da7dd4637347fa690952c961e74c88fb74) | Apply saved passthrough after creating environment geometry | Original | [R7](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744) |
| 8 | [c4d2c2f12](https://github.com/icosa-foundation/open-brush/commit/c4d2c2f12b4e48e9c0015d6885352cca9ffa01ce) | Handle passthrough in environments without room geometry | Original | [R7](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744) |
| 9 | [3d26cf1d7](https://github.com/icosa-foundation/open-brush/commit/3d26cf1d789fb9bc497b5a5bcfaf60a63b1dd03a) | Parent empty passthrough holders with environment geometry | Original | [R7](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744) |
| 10 | [e2771d721](https://github.com/icosa-foundation/open-brush/commit/e2771d721ecbfdb1c2f7bb149dd7374edc9c71a2) | Restore passthrough render state before loading custom skyboxes | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-backdrop-passthrough-settings/BRANCH-REVIEW.md>) |
| 11 | [3944ff5bc](https://github.com/icosa-foundation/open-brush/commit/3944ff5bc8f03fcfe6feecdd0c2584c7e3be17e7) | Restore custom skybox identity on environment undo | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-backdrop-passthrough-settings/BRANCH-REVIEW.md>) |
| 12 | [0622b046d](https://github.com/icosa-foundation/open-brush/commit/0622b046d5ed7341432173bcaf9aad60b1be1b6f) | Make backdrop panel mode changes undoable | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-backdrop-passthrough-settings/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Resources/X/Brushes/PassthroughHull/PassthroughHull.asset:51

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391656) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:01Z · Resolved · Outdated code location

> This new material GUID has only a `.meta` file in the change; the corresponding `PassthroughHull.mat` asset is absent. The brush, hull prefab, and passthrough preview all reference this GUID, so Unity will import them with a missing material. Add the material asset itself to the pull request.

Mapped commits: [201fe4984](https://github.com/icosa-foundation/open-brush/commit/201fe49849fe7ff26a681b2e4236553f23318f85).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342018513) explicitly references the mapped commit.

#### R2 — Assets/Prefabs/Panels/BackdropPanel.prefab:3721

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391686) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:01Z · Resolved · Outdated code location

> The “Apply fog to passthrough” toggle invokes `TogglePassthrough`, but `BackdropPanel` defines no such method (and no fog-option handler). Unity will log a missing-method error and this control will do nothing. Wire it to a real fog-setting handler or remove the unfinished control.
> 
> This issue also appears in the following locations of the same file:
> - line 4433
> - line 4624

Mapped commits: [cf98d0d5d](https://github.com/icosa-foundation/open-brush/commit/cf98d0d5d9227162a5b1328d53bf6eb437ddde46).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342020279) explicitly references the mapped commit.

#### R3 — Assets/Scripts/GUI/BackdropPanel.cs:108

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391712) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:02Z · Resolved

> This direct mode change bypasses the existing multiplayer passthrough guard. `ShouldBlockLeavingPassthroughInMultiplayer` only runs during `SetDesiredPreset` and checks `Environment.isPassthrough`, so a user who enabled passthrough through this panel can disable it while remote players are present even though the existing code explicitly blocks leaving passthrough in that state. Route backdrop mode changes through the same guard and base the check on the actual passthrough state.
> 
> This issue also appears on line 174 of the same file.

Mapped commits: [da4cdf0ec](https://github.com/icosa-foundation/open-brush/commit/da4cdf0ecb7025a875865fed43ddef53c0919771).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342021638) explicitly references the mapped commit.

#### R4 — Assets/Scripts/GUI/TextActionButton.cs:44

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391721) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:02Z · Resolved · Outdated code location

> This changes every `TextActionButton` in the project, including the existing multi-mirror and Icosa option popups: deselected buttons now keep their highlight mesh visible. Scope the always-visible, recolored background behavior to the new backdrop mode button subclass (and wire those prefab instances to it), while retaining `SetActive(bSelected)` for the shared base class.

Mapped commits: [317e73442](https://github.com/icosa-foundation/open-brush/commit/317e7344203ca5d6134bbb569c06e877d6b18161).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342022620) explicitly references the mapped commit.

#### R5 — Assets/Scripts/SceneSettings.cs:54

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391731) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:02Z · Resolved

> `BackdropModeChanged` is not emitted by either `InGradient` or `PassthroughEnabled`. The initial button click is masked by `BackdropPanel.ChangeBackdropMode` calling its handler directly, but undo/redo executes `UnlockSkyboxCommand` without that call, leaving the selected mode and visible controls stale. Raise this event from the authoritative mode-changing paths instead of only from the panel caller.

Mapped commits: [21f73353b](https://github.com/icosa-foundation/open-brush/commit/21f73353b99e485bddbe37bc371c85ce0e874961).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342023613) explicitly references the mapped commit.

#### R6 — Assets/Scripts/SceneSettings.cs:206

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391735) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:02Z · Resolved · Outdated code location

> Disabling passthrough does not restore all rendering state that the enabled branch changes. In particular, fog remains disabled, and gradient/custom-skybox environments get `SolidColor` clear flags, so their skybox material is not rendered after switching out of passthrough. Restore the environment's fog setting and use skybox clearing for the registered cameras.

Mapped commits: [864e253a8](https://github.com/icosa-foundation/open-brush/commit/864e253a8336cd41d1506b1a97c0161df184826c).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342024769) explicitly references the mapped commit.

#### R7 — Assets/Scripts/SceneSettings.cs:478

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:55:03Z · Resolved · Outdated code location

> The saved passthrough state is applied before `SetDesiredPreset` transitions to `env`. When that preset uses a different environment prefab, `CreateEnvironment` destroys `m_RoomGeometry` (and the just-added `OVRPassthroughLayer`), while the custom-environment branch deliberately skips reapplying passthrough afterward. Loading such a sketch therefore reports passthrough enabled but has no active layer; defer/reapply this state after the new room geometry is created.

Mapped commits: [4ef2f1da7](https://github.com/icosa-foundation/open-brush/commit/4ef2f1da7dd4637347fa690952c961e74c88fb74), [c4d2c2f12](https://github.com/icosa-foundation/open-brush/commit/c4d2c2f12b4e48e9c0015d6885352cca9ffa01ce), [3d26cf1d7](https://github.com/icosa-foundation/open-brush/commit/3d26cf1d789fb9bc497b5a5bcfaf60a63b1dd03a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342025679), [resolution reply 2](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r4342257823) explicitly references the mapped commits.

<a id="branch-brush-editing"></a>

## 4. features/brush-editing — PR #860

[Pull request](https://github.com/icosa-foundation/open-brush/pull/860) · [Local branch review](<F:/Unity Projects/open-brush-brush-editing/BRANCH-REVIEW.md>)

12 commits; 6 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [a209c62e3](https://github.com/icosa-foundation/open-brush/commit/a209c62e38af8f550f5a10608e45113a19b3dd69) | Compare and hash serializable GUIDs by value | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530086), [R3](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530203) |
| 2 | [8a9a9a75b](https://github.com/icosa-foundation/open-brush/commit/8a9a9a75b519afbb2bf5453085aee8d4102a31be) | Clamp brush page selection in the correct argument order | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530278) |
| 3 | [2ec211dd8](https://github.com/icosa-foundation/open-brush/commit/2ec211dd8a2c63212225e77a2d1f7b11aa36ee72) | Preserve recursive file watching by default | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530299) |
| 4 | [1edd39ced](https://github.com/icosa-foundation/open-brush/commit/1edd39ced7968c1a704ceeda2e442b62a9944dd2) | List immediate brush directories consistently in folders and archives | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176) |
| 5 | [977f02ccf](https://github.com/icosa-foundation/open-brush/commit/977f02ccf557947a2299d8500c024a2e8c514778) | Preserve missing brush GUIDs when writing fallback metadata | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530324) |
| 6 | [0a1c6f2e8](https://github.com/icosa-foundation/open-brush/commit/0a1c6f2e8f4cf3de44d8e310999a1821488670db) | Qualify archive path comparison enum | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176) |
| 7 | [5e6fc58bd](https://github.com/icosa-foundation/open-brush/commit/5e6fc58bdf69180fae31ae47441a461fad4dfd0a) | Escape the archive path separator character | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176) |
| 8 | [7815f928e](https://github.com/icosa-foundation/open-brush/commit/7815f928e73230d8ccfe73a09ddb75ccf4e3ba3f) | Keep brush preview drawings off sketch canvases | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-brush-editing/BRANCH-REVIEW.md>) |
| 9 | [1f6db0c25](https://github.com/icosa-foundation/open-brush/commit/1f6db0c255b81d44f362f59286c1a6c94a74b3c8) | Preserve unchanged user brush texture references on save | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-brush-editing/BRANCH-REVIEW.md>) |
| 10 | [79810c5be](https://github.com/icosa-foundation/open-brush/commit/79810c5be893a1a5dd359b077c9b82608d0ed7d6) | Enable brush saving after every material edit | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-brush-editing/BRANCH-REVIEW.md>) |
| 11 | [7552c6b8d](https://github.com/icosa-foundation/open-brush/commit/7552c6b8da0c236bd2f5ed2aad8e861f54c49770) | Select duplicated user brushes by their new library location | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-brush-editing/BRANCH-REVIEW.md>) |
| 12 | [6582f4884](https://github.com/icosa-foundation/open-brush/commit/6582f4884a77ad954375bee3bbaf2f51fa7acb0d) | Publish brush configs atomically and retain failed edits | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-brush-editing/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/Util/SerializableGuid.cs:83

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530086) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:27:37Z · Resolved

> GetHashCode() can throw NullReferenceException when m_storage is null (eg default(SerializableGuid)). Also, hashing the raw string makes equality/hash dependent on string casing/format instead of the underlying Guid value.

Mapped commits: [a209c62e3](https://github.com/icosa-foundation/open-brush/commit/a209c62e38af8f550f5a10608e45113a19b3dd69).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4341953648) explicitly references the mapped commit.

#### R2 — Assets/Scripts/Save/FolderOrZipReader.cs:160

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:27:38Z · Resolved · Outdated code location

> GetContentsAt() has a couple of correctness issues: (1) Path.GetDirectoryName(...) can return null/empty for entries directly under `path`, so `subpart[0]` can throw. (2) In the folder case it returns absolute file paths, but callers (eg SaveLoadScript loading embedded brushes) expect child names/relative paths. Returning immediate child names for both zip and folder cases avoids crashes and makes the API consistent.

Mapped commits: [1edd39ced](https://github.com/icosa-foundation/open-brush/commit/1edd39ced7968c1a704ceeda2e442b62a9944dd2), [0a1c6f2e8](https://github.com/icosa-foundation/open-brush/commit/0a1c6f2e8f4cf3de44d8e310999a1821488670db), [5e6fc58bd](https://github.com/icosa-foundation/open-brush/commit/5e6fc58bdf69180fae31ae47441a461fad4dfd0a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4341956756), [resolution reply 2](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4342103790) explicitly references the mapped commits.

#### R3 — Assets/Scripts/Util/SerializableGuid.cs:131

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530203) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:27:39Z · Resolved · Outdated code location

> Equals(object) currently compares string representations via obj.ToString(), which makes equality case/format sensitive and can produce surprising results (eg comparing to a non-SerializableGuid object). Comparing underlying System.Guid values is safer and consistent with GetHashCode()/operators.

Mapped commits: [a209c62e3](https://github.com/icosa-foundation/open-brush/commit/a209c62e38af8f550f5a10608e45113a19b3dd69).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4341954687) explicitly references the mapped commit.

#### R4 — Assets/Scripts/GUI/BrushGrid.cs:542

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530278) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:27:39Z · Resolved · Outdated code location

> Mathf.Clamp arguments are in the wrong order (Clamp(value, min, max)). As written, `Mathf.Clamp(0, page, max)` can return an out-of-range page when page > max. Also consider early-returning if the brush isn't found (brushIndex == -1).

Mapped commits: [8a9a9a75b](https://github.com/icosa-foundation/open-brush/commit/8a9a9a75b519afbb2bf5453085aee8d4102a31be).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4341958597) explicitly references the mapped commit.

#### R5 — Assets/Scripts/FileWatcher.cs:33

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530299) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:27:39Z · Resolved · Outdated code location

> This class previously always set IncludeSubdirectories = true. With the new parameter defaulting to false, existing call sites that don't pass includeSubdirectories (eg model library watchers) will stop detecting changes in nested folders. If you want to preserve prior behavior, default the parameter to true.

Mapped commits: [2ec211dd8](https://github.com/icosa-foundation/open-brush/commit/2ec211dd8a2c63212225e77a2d1f7b11aa36ee72).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4341959591) explicitly references the mapped commit.

#### R6 — Assets/Scripts/Save/SketchWriter.cs:225

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530324) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:27:40Z · Resolved · Outdated code location

> BrushCatalog.m_Instance.GetBrush(brushGuid) can return null (eg if the sketch contains a missing brush guid), which would throw here. Since this is a fallback mechanism, it should handle null and fall back to brushGuid (or Guid.Empty).

Mapped commits: [977f02ccf](https://github.com/icosa-foundation/open-brush/commit/977f02ccf557947a2299d8500c024a2e8c514778).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r4341960726) explicitly references the mapped commit.

<a id="branch-dome-camera"></a>

## 5. feature/dome-camera — PR #1114

1 commits; 0 distinct linked review comments.

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1114) · [Local branch review](<F:/Unity Projects/open-brush-dome-camera/BRANCH-REVIEW.md>)

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [66729e0ee](https://github.com/icosa-foundation/open-brush/commit/66729e0eeb7344af1183a8926a0291e3da9b94c3) | Declare Android multicast access required by NDI | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-dome-camera/BRANCH-REVIEW.md>) |

<a id="branch-editable-platform-settings"></a>

## 6. feature/editable-platform-settings — PR #643

[Pull request](https://github.com/icosa-foundation/open-brush/pull/643) · [Local branch review](<F:/Unity Projects/open-brush-editable-platform-settings/BRANCH-REVIEW.md>)

3 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [10876b9dd](https://github.com/icosa-foundation/open-brush/commit/10876b9dd681d25fcba381629533461f39e4311d) | Reject non-finite positive performance overrides | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-editable-platform-settings/BRANCH-REVIEW.md>) |
| 2 | [a12356f73](https://github.com/icosa-foundation/open-brush/commit/a12356f73bb574b6a8c7b79c5a5810060ca99d94) | Restore multicam API preview using performance overrides | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-editable-platform-settings/BRANCH-REVIEW.md>) |
| 3 | [8c303662f](https://github.com/icosa-foundation/open-brush/commit/8c303662f61ffbcb9a64436f1a1ee12f0347a04a) | Apply image-limit overrides across all reference image loaders | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-editable-platform-settings/BRANCH-REVIEW.md>) |

<a id="branch-fix-media-import-paths"></a>

## 7. feature/fix-media-import-paths — PR #1121

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1121) · [Local branch review](<F:/Unity Projects/open-brush-fix-media-import-paths/BRANCH-REVIEW.md>)

6 commits; 3 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [be7a2c081](https://github.com/icosa-foundation/open-brush/commit/be7a2c0816d6abaffcf16fb9ded242f19a553397) | Constrain relative media resolution to its configured root | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571368) |
| 2 | [b1e3285b3](https://github.com/icosa-foundation/open-brush/commit/b1e3285b32e7b84d49e517c8eb5f4c2e250d29bd) | Require a directory boundary when matching model roots | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571409) |
| 3 | [5b2a87ccb](https://github.com/icosa-foundation/open-brush/commit/5b2a87ccb50976fc08fe65c861ca9be27186fed7) | Keep temporary media import review notes local | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571429) |
| 4 | [599adf41e](https://github.com/icosa-foundation/open-brush/commit/599adf41e2eba490ed294036f6b0e81730dfc14b) | Save image paths relative to their configured media root | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-fix-media-import-paths/BRANCH-REVIEW.md>) |
| 5 | [ad2036316](https://github.com/icosa-foundation/open-brush/commit/ad203631697e11fdcc79c0343ecaa650ddf56a90) | Retain model root ownership when duplicate paths are skipped | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-fix-media-import-paths/BRANCH-REVIEW.md>) |
| 6 | [7c135c137](https://github.com/icosa-foundation/open-brush/commit/7c135c1371173cab9460a125cb634ebf7558b3d6) | Deduplicate equivalent media root spellings before image scans | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-fix-media-import-paths/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/App.cs:2354

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571368) · Author: copilot-pull-request-reviewer · Created: 2026-07-29T11:05:55Z · Resolved · Outdated code location

> ResolveMediaPath() combines roots with an unvalidated relativePath and returns the first existing file. If relativePath contains traversal (e.g. "../"), Path.Combine() can escape the intended root and File.Exists() can succeed on a file outside configured media roots. This is then used for model/video resolution (sketch load), so it should enforce "must remain under root" the same way ApiMethods.GetSafeRelativePathInDirectory() does.

Mapped commits: [be7a2c081](https://github.com/icosa-foundation/open-brush/commit/be7a2c0816d6abaffcf16fb9ded242f19a553397).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r4341967522) explicitly references the mapped commit.

#### R2 — Assets/Scripts/ModelCatalog.cs:124

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571409) · Author: copilot-pull-request-reviewer · Created: 2026-07-29T11:05:55Z · Resolved

> GetModelRoot() uses a plain StartsWith(directory, OrdinalIgnoreCase) check. With the new MediaRoots-based multi-root support, this can misclassify sibling directories that share a prefix (e.g. "C:/Models" vs "C:/Models-Backup"), which then breaks relative-path derivation and catalog grouping.

Mapped commits: [b1e3285b3](https://github.com/icosa-foundation/open-brush/commit/b1e3285b32e7b84d49e517c8eb5f4c2e250d29bd).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r4341968497) explicitly references the mapped commit.

#### R3 — MEDIA_IMPORT_PATHS_BRANCH_REVIEW.md:60

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571429) · Author: copilot-pull-request-reviewer · Created: 2026-07-29T11:05:56Z · Resolved · Outdated code location

> This document claims API import endpoints accept absolute filesystem paths and that App.cs adds in-memory Add*Root() registration methods. In the current code, ApiMethods.GetSafeRelativePathInDirectory() rejects Path.IsPathRooted() inputs (Assets/Scripts/API/ApiMethods.cs:1184-1191), and additional roots are read from UserConfig.MediaRoots (Assets/Scripts/UserConfig.cs:371-387) rather than Add*Root() methods. The doc should be updated to reflect the actual implementation so it doesn't mislead future readers.

Mapped commits: [5b2a87ccb](https://github.com/icosa-foundation/open-brush/commit/5b2a87ccb50976fc08fe65c861ca9be27186fed7).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r4341969730) explicitly references the mapped commit.

<a id="branch-gltf-export-profiles"></a>

## 8. feature/gltf-export-profiles — PR #1117

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1117) · [Local branch review](<F:/Unity Projects/open-brush-gltf-export-profiles/BRANCH-REVIEW.md>)

15 commits; 17 distinct linked review comments.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [cfb56c949](https://github.com/icosa-foundation/open-brush/commit/cfb56c949b475982049293bc709e07c8cb2df609) | Isolate static GLB export failures like other formats | Original | [R17](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3992531425) |
| 2 | [b52459117](https://github.com/icosa-foundation/open-brush/commit/b5245911725b0a35d561fc8f51f7a3f7d3eeb5d1) | Preserve undeformed meshes in OpenBrush GLB exports | Original | [R14](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3980017909) |
| 3 | [2e16d57ed](https://github.com/icosa-foundation/open-brush/commit/2e16d57ed909d42e2223521d06c5edd1fdc2f346) | Retain batch material overrides during static GLB export | Original | [R10](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3914877048) |
| 4 | [b13ad9325](https://github.com/icosa-foundation/open-brush/commit/b13ad932543fadeb9a27fc0833aa363428fec189) | Export linear additive stroke colors and sub-unit emission gains | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3822139203), [R15](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860868) |
| 5 | [54cd125ea](https://github.com/icosa-foundation/open-brush/commit/54cd125ea62e82c4175f1160a49d8fc8c549c8e5) | Remap batch timestamps through static mesh vertex expansion | Original | [R16](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860875) |
| 6 | [9100836b9](https://github.com/icosa-foundation/open-brush/commit/9100836b93b4ea6f4e7e889d20618a4b359dc5ce) | Retain stored particle billboard rotation in static bakes | Original | [R11](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3914877055) |
| 7 | [6a532997d](https://github.com/icosa-foundation/open-brush/commit/6a532997d186972ff14cfc22306a655acd549a4a) | Freeze shader time and neutralize audio inputs during texture bakes | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112368) |
| 8 | [1889a761d](https://github.com/icosa-foundation/open-brush/commit/1889a761d0af2e628f1c651183b687f2d3384079) | Preserve repeated UV continuity and filtering gutters in emissive atlases | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112364), [R4](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201345) |
| 9 | [27c1725fb](https://github.com/icosa-foundation/open-brush/commit/27c1725fb363273f12c857bfe0f7d24117c1251e) | Bake procedural additive shader output into emissive textures | Original | [R7](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3711377578), [R12](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794441), [R13](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794448) |
| 10 | [3a1dda65f](https://github.com/icosa-foundation/open-brush/commit/3a1dda65fb3c084b27e0d6b31ea9cd46d8829d88) | Consume baked particle colors and bubble highlights in emissive atlases | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112365), [R5](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201350), [R6](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201351) |
| 11 | [7a2478b1e](https://github.com/icosa-foundation/open-brush/commit/7a2478b1e69f2a0598fcbacceff578cc6d4729e8) | Bake Snow lifetime opacity and Embers sparkle colors at export time | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112365), [R6](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201351) |
| 12 | [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199) | Provide core alpha coverage approximation for static additive materials | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201350), [R7](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3711377578), [R8](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3822139203), [R9](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3876496156), [R15](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860868) |
| 13 | [62ee6e60b](https://github.com/icosa-foundation/open-brush/commit/62ee6e60b9de0f4dc1b10c387477b3ef5695724b) | Preserve atlas alpha coverage with UnityGLTF texture export settings | Original | [R9](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3876496156) |
| 14 | [5a57a5253](https://github.com/icosa-foundation/open-brush/commit/5a57a5253cfc8c393b3e9c89ac136af9ad9c5fd6) | Retain stroke-dependent ChromaticWave line phases in static atlases | Original | [R13](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794448) |
| 15 | [08bdca9fa](https://github.com/icosa-foundation/open-brush/commit/08bdca9fa733650b1784b6ada7f6908d9f08bf5c) | Restore live GLB export state after exceptional exits | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-gltf-export-profiles/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:625

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112364) · Author: chatgpt-codex-connector · Created: 2026-07-26T18:13:06Z · Resolved · Outdated code location

> **[P2]  Preserve UV continuity across atlas wrap seams**
> 
> For repeating additive textures, wrapping each vertex independently breaks interpolation whenever a triangle crosses an integer UV boundary: endpoints such as `0.9` and `1.1` become `0.9` and `0.1`, so the rasterizer traverses most of the atlas tile instead of crossing the texture seam. Long brush ribbons commonly use continuously increasing UVs, causing their patterns to stretch or run backward at every repeat; split geometry at wrap boundaries or otherwise retain continuous coordinates when constructing the atlas.

Mapped commits: [1889a761d](https://github.com/icosa-foundation/open-brush/commit/1889a761d0af2e628f1c651183b687f2d3384079).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342071185) explicitly references the mapped commit.

#### R2 — Assets/Shaders/BrushBaker Compute/Static/bakeEmbersStatic.compute:75

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112365) · Author: chatgpt-codex-connector · Created: 2026-07-26T18:13:06Z · Resolved

> **[P2]  Bake Embers' per-particle color animation**
> 
> At the fixed export time, `t01` and `seed` also drive the large sparkle, lifetime fade, and brightness changes in `Assets/Resources/Brushes/Basic/Embers/Embers.shader` lines 139-152, but this kernel only moves vertices and the prefab sets `ModifyColor` to false. Consequently the static material atlas applies only the stroke color, leaving particles fully emissive when the live snapshot would make them dim or invisible; write the fixed-time RGB/alpha result and arrange for the static material to consume it.

Mapped commits: [3a1dda65f](https://github.com/icosa-foundation/open-brush/commit/3a1dda65fb3c084b27e0d6b31ea9cd46d8829d88), [7a2478b1e](https://github.com/icosa-foundation/open-brush/commit/7a2478b1e69f2a0598fcbacceff578cc6d4729e8).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342072624) explicitly references the mapped commits.

#### R3 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:1403

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112368) · Author: chatgpt-codex-connector · Created: 2026-07-26T18:13:06Z · Resolved · Outdated code location

> **[P2]  Freeze animated shader inputs during texture baking**
> 
> When baking the Neon Pulse and Electricity policies, this renders the live material without enabling its `_TimeBlend` override or neutralizing audio globals. Those shaders read `GetTime()` and `_BeatOutput`, so identical sketches exported at different instants can produce different baked textures, and the texture phase can disagree with geometry explicitly frozen at time zero (or be unusable during a quiet audio frame). Render a temporary material with a fixed time override and deterministic neutral audio inputs before calling `SetPass`.

Mapped commits: [6a532997d](https://github.com/icosa-foundation/open-brush/commit/6a532997d186972ff14cfc22306a655acd549a4a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342073649) explicitly references the mapped commit.

#### R4 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:695

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201345) · Author: chatgpt-codex-connector · Created: 2026-07-26T19:01:01Z · Resolved · Outdated code location

> **[P2]  Add filtering gutters between emissive atlas tiles**
> 
> When a batch contains multiple stroke colors and the source texture uses bilinear or trilinear filtering, mapping each tile over its full `[0,1]` range places UV endpoints directly on the boundary between differently tinted tiles. Common brush vertices with UV 0 or 1 will therefore sample neighboring stroke colors, producing colored seams; inset the coordinates by half a texel and duplicate edge pixels into gutters.

Mapped commits: [1889a761d](https://github.com/icosa-foundation/open-brush/commit/1889a761d0af2e628f1c651183b687f2d3384079).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342075098) explicitly references the mapped commit.

#### R5 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:790

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201350) · Author: chatgpt-codex-connector · Created: 2026-07-26T19:01:01Z · Resolved · Outdated code location

> **[P2]  Fold bubble highlight alpha into the atlas RGB**
> 
> For batched Bubbles and Rising Bubbles, their shaders add `_MainTex.a` to RGB as an untinted white highlight, but this atlas stores only `pixel.rgb * tint` in RGB and leaves the highlight in alpha. glTF emissive textures consume RGB rather than alpha, so every `KeepStrokes=false` static export loses the bubbles' characteristic white highlight; bake that alpha contribution into the atlas RGB for these brushes.

Mapped commits: [3a1dda65f](https://github.com/icosa-foundation/open-brush/commit/3a1dda65fb3c084b27e0d6b31ea9cd46d8829d88), [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342076804) explicitly references the mapped commits.

#### R6 — Assets/Shaders/BrushBaker Compute/Static/bakeSnowStatic.compute:70

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201351) · Author: chatgpt-codex-connector · Created: 2026-07-26T19:01:01Z · Resolved

> **[P2]  Bake Snow's fixed-time lifetime alpha**
> 
> For Snow particles, this `t` also drives `o.color.a = pow(1 - abs(2 * (t - .5)), 3)` in `Snow.shader`, which controls the brush's `SrcAlpha One` lifetime fade. The static mapping leaves `ModifyColor` false and this kernel writes only positions, so the exported mesh retains the random seed in vertex alpha instead of the opacity at the frozen phase, making particles incorrectly bright or dim; write the computed alpha and enable color readback.

Mapped commits: [3a1dda65f](https://github.com/icosa-foundation/open-brush/commit/3a1dda65fb3c084b27e0d6b31ea9cd46d8829d88), [7a2478b1e](https://github.com/icosa-foundation/open-brush/commit/7a2478b1e69f2a0598fcbacceff578cc6d4729e8).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342078018) explicitly references the mapped commits.

#### R7 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:1591

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3711377578) · Author: chatgpt-codex-connector · Created: 2026-08-04T10:12:28Z · Resolved

> **[P2]  Route unlit additive bakes through the emissive texture**
> 
> For static exports of Electricity and Neon Pulse, whose policies use `UvUnlit`, this stores the baked center-line/pulse only in `baseColorTexture`. `AfterPrimitiveExport` subsequently handles these additive materials by cloning them with an emissive texture sourced from raw `_MainTex`/the color atlas (or no texture for Neon Pulse), so the emissive factor illuminates the whole ribbon and the baked procedural pattern never modulates emission. Export the `UvUnlit` result as the emissive texture, including when constructing per-stroke color atlases.

Mapped commits: [27c1725fb](https://github.com/icosa-foundation/open-brush/commit/27c1725fb363273f12c857bfe0f7d24117c1251e), [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342079150) explicitly references the mapped commits.

#### R8 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:632

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3822139203) · Author: chatgpt-codex-connector · Created: 2026-08-20T13:49:16Z · Resolved · Outdated code location

> **[P2]  Apply sub-unit emissive gain to additive materials**
> 
> For additive brushes whose configured emission gain is below 1, this condition drops the gain entirely: Comet uses `m_EmissiveFactor: 0.5`, Sparks uses `0.2`, and Rain uses `0.3`, but their cloned materials receive the unscaled stroke color as `EmissiveFactor`. The atlas path has the same behavior with a white factor, so both `KeepStrokes` modes render these brushes two to five times brighter than configured; multiply the emissive factor by gains up to 1 and reserve `KHR_materials_emissive_strength` for values above 1.

Mapped commits: [b13ad9325](https://github.com/icosa-foundation/open-brush/commit/b13ad932543fadeb9a27fc0833aa363428fec189), [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342080124) explicitly references the mapped commits.

#### R9 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:1060

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3876496156) · Author: chatgpt-codex-connector · Created: 2026-08-27T22:55:23Z · Resolved · Outdated code location

> **[P2]  Provide a portable fallback for additive blending**
> 
> For `static-glb` files opened by generic glTF consumers such as Blender, this optional repository-specific `EXT_blend_operations` extension is ignored, leaving only `AlphaMode.BLEND`. Standard alpha blending attenuates/replaces the destination color, whereas these brushes require `One One` or `SrcAlpha One`, so Fire, Stars, Embers, Smoke, Snow, and other additive strokes render incorrectly over non-black backgrounds. Because this profile is explicitly intended for generic consumers, its baked material data needs a core-compatible approximation that does not depend on this custom extension.

Mapped commits: [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199), [62ee6e60b](https://github.com/icosa-foundation/open-brush/commit/62ee6e60b9de0f4dc1b10c387477b3ef5695724b).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342081467) explicitly references the mapped commits.

#### R10 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:348

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3914877048) · Author: chatgpt-codex-connector · Created: 2026-09-02T13:57:54Z · Resolved · Outdated code location

> **[P2]  Preserve runtime material overrides in static exports**
> 
> When a sketch uses `Stroke.SetShaderFloat`, `SetShaderColor`, `SetShaderTexture`, clipping, or the equivalent Layer API, those values and keywords are stored on `Batch.InstantiatedMaterial`. Replacing it with the catalog's `brush.Material` before UnityGLTF processes the batch discards those overrides, so the static GLB exports default textures and parameters rather than the currently rendered appearance; restoring the instance afterward only repairs the live scene. Export the instantiated material, or deduplicate only instances with identical effective properties.

Mapped commits: [2e16d57ed](https://github.com/icosa-foundation/open-brush/commit/2e16d57ed909d42e2223521d06c5edd1fdc2f346).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342083837) explicitly references the mapped commit.

#### R11 — Assets/Shaders/BrushBaker Compute/Static/bakeSnowStatic.compute:53

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3914877055) · Author: chatgpt-codex-connector · Created: 2026-09-02T13:57:54Z · Resolved

> **[P2]  Apply stored particle rotation when baking billboards**
> 
> For Snow particles with a nonzero `uv0.z` rotation, this reconstructs every quad as an unrotated axis-aligned square. The live shader passes that component as `rotation` to `OrientParticle`, so asymmetric snowflake textures are visibly reoriented in the static export. The same reconstruction is repeated in the Bubbles, Embers, and Rising Bubbles kernels; rotate the baked corner offset by the stored angle before writing the vertex.

Mapped commits: [9100836b9](https://github.com/icosa-foundation/open-brush/commit/9100836b93b4ea6f4e7e889d20618a4b359dc5ce).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342085234) explicitly references the mapped commit.

#### R12 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:1221

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794441) · Author: chatgpt-codex-connector · Created: 2026-09-08T15:55:12Z · Resolved

> **[P2]  Bake Comet's actual AlphaMask output**
> 
> For static exports of Comet in either `KeepStrokes` mode, this treats `_MainTex` as the emitted image, but the committed Comet fragment shader uses `_MainTex` only to calculate a lookup coordinate and takes its final RGB from `_AlphaMask` (`Support/GlTFShaders/Generators/Comet-1caa6d7d-f015-3f54-3a4b-8b5354d39f81-v10.0-fragment.glsl`, lines 41-58). The exported GLB therefore shows the packed noise texture rather than the comet gradient; add a Comet-specific fixed-time bake that combines both textures.
> 
> AGENTS.md reference: [AGENTS.md:L4-L4](https://github.com/icosa-foundation/open-brush/blob/68d536c980aea41424f56d74f8c3b4bd80531e18/AGENTS.md#L4-L4)

Mapped commits: [27c1725fb](https://github.com/icosa-foundation/open-brush/commit/27c1725fb363273f12c857bfe0f7d24117c1251e).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342086213) explicitly references the mapped commit.

#### R13 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:724

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794448) · Author: chatgpt-codex-connector · Created: 2026-09-08T15:55:12Z · Resolved · Outdated code location

> **[P2]  Bake textureless procedural additive brushes**
> 
> When a batched static export contains ChromaticWave or Rainbow, `textureSource.Texture` is null because their normal brush materials have no source texture in `Support/exportManifest.json`, so this path generates a one-pixel white tile and applies only the stroke tint. Their committed fragment shaders instead generate sparse multicolor lines procedurally from UV and time, meaning these distinctive brushes become solid single-color ribbons; provide a fixed-phase emissive bake rather than using the solid fallback.
> 
> AGENTS.md reference: [AGENTS.md:L4-L4](https://github.com/icosa-foundation/open-brush/blob/68d536c980aea41424f56d74f8c3b4bd80531e18/AGENTS.md#L4-L4)

Mapped commits: [27c1725fb](https://github.com/icosa-foundation/open-brush/commit/27c1725fb363273f12c857bfe0f7d24117c1251e), [5a57a5253](https://github.com/icosa-foundation/open-brush/commit/5a57a5253cfc8c393b3e9c89ac136af9ad9c5fd6).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342087469) explicitly references the mapped commits.

#### R14 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:385

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3980017909) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:16:09Z · Resolved · Outdated code location

> **[P2]  Skip deformation bakes in the OpenBrush profile**
> 
> For an OpenBrush-profile round trip, this branch still runs every legacy compute bake before export, but the new `TB_BrushGuid` import path subsequently restores `brush.Material` and its live vertex shader in `OpenBrushMaterialImportPlugin.ApplyBrushMaterial`. For example, `bakeDoubleTaperedX.compute` subtracts `uv1 * (1 - sin(uv0.x * pi))`, and the restored Double Tapered shader performs the same taper again, so importing the exported GLB into Open Brush deforms those strokes twice; preserve the original mesh for the OpenBrush profile and reserve these bakes for the static profile.
> 
> AGENTS.md reference: [AGENTS.md:L4-L4](https://github.com/icosa-foundation/open-brush/blob/3a29ea2e95b937a0a19b52d56a9cd79915b717d5/AGENTS.md#L4-L4)

Mapped commits: [b52459117](https://github.com/icosa-foundation/open-brush/commit/b5245911725b0a35d561fc8f51f7a3f7d3eeb5d1).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342088938) explicitly references the mapped commit.

#### R15 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:688

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860868) · Author: chatgpt-codex-connector · Created: 2026-09-10T19:44:36Z · Resolved · Outdated code location

> **[P2]  Linearize additive stroke colors before export**
> 
> When `KeepStrokes=true` and an additive stroke contains mid-range color components, `mesh.colors32` contains Open Brush's sRGB vertex color, but these lines copy those bytes directly into glTF's linear `emissiveFactor`; for example, byte value 128 becomes 0.502 instead of approximately 0.216. This makes colored additive brushes substantially too bright in standards-compliant consumers, so convert the stroke color to linear before assigning the factor. This was checked against the brushes' normal `m_Material` path.
> 
> AGENTS.md reference: [AGENTS.md:L4-L4](https://github.com/icosa-foundation/open-brush/blob/e370999445140429c9266561862292f4ad7bba46/AGENTS.md#L4-L4)

Mapped commits: [b13ad9325](https://github.com/icosa-foundation/open-brush/commit/b13ad932543fadeb9a27fc0833aa363428fec189), [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342090124) explicitly references the mapped commits.

#### R16 — Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:367

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860875) · Author: chatgpt-codex-connector · Created: 2026-09-10T19:44:36Z · Resolved · Outdated code location

> **[P2]  Preserve source timestamps when splitting faceted vertices**
> 
> For a batched static FacetedTube export with `ExportStrokeTimestamp` enabled (the default), the face-color bake duplicates vertices in triangle order and returns `sourceVertexIndices`, but this stores only each remapped subset's new range. `CreateTimestampData` then linearly resamples timestamps across the expanded vertex count, so duplicate vertices originating from the same source vertex receive different timestamps—for example, source order `[0,1,2,0,2,3]` is treated as six sequential points. Carry the source-index mapping into timestamp generation so the exported `_TB_TIMESTAMP` attribute continues to match the geometry.

Mapped commits: [54cd125ea](https://github.com/icosa-foundation/open-brush/commit/54cd125ea62e82c4175f1160a49d8fc8c549c8e5).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342091174) explicitly references the mapped commit.

#### R17 — Assets/Scripts/Export/Export.cs:367

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3992531425) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:10Z · Resolved · Outdated code location

> **[P2]  Route static GLB through the export isolation wrapper**
> 
> When `static-glb` is enabled and UnityGLTF or a bake throws, this block bypasses the `RunExport` exception-isolation wrapper used by every other format. The exception therefore escapes `ExportScene`, leaves the static progress item incomplete, and skips the success UI and README creation even when the other requested formats were exported successfully; invoke this format through `RunExport("static-glb", ...)` as well.

Mapped commits: [cfb56c949](https://github.com/icosa-foundation/open-brush/commit/cfb56c949b475982049293bc709e07c8cb2df609).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r4342092031) explicitly references the mapped commit.

<a id="branch-icosa-collections"></a>

## 9. feature/icosa-collections — PR #1095

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1095) · [Local branch review](<F:/Unity Projects/open-brush-icosa-collections/BRANCH-REVIEW.md>)

3 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [972f6b97f](https://github.com/icosa-foundation/open-brush/commit/972f6b97f3d0c54969e4b865d1cb09f93380fcd9) | Refresh selected collections when forced or their query changes | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-icosa-collections/BRANCH-REVIEW.md>) |
| 2 | [f6f38b57c](https://github.com/icosa-foundation/open-brush/commit/f6f38b57cf1c45b792959efb2ed3e391e7378fb1) | Clear recycled asset models when showing collection tiles | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-icosa-collections/BRANCH-REVIEW.md>) |
| 3 | [51376818e](https://github.com/icosa-foundation/open-brush/commit/51376818eae4dc6487dcb5404ae9c5faec5b40f6) | Escape Icosa collection query values | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-icosa-collections/BRANCH-REVIEW.md>) |

<a id="branch-inspector"></a>

## 10. feature/inspector — PR #1150

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1150) · [Local branch review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>)

7 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [041df13a2](https://github.com/icosa-foundation/open-brush/commit/041df13a2493a47a73c8a0a6e061d93bb8aefb38) | Initialize inspector tabs safely before inactive selection notifications | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |
| 2 | [7e3ecc069](https://github.com/icosa-foundation/open-brush/commit/7e3ecc0697997e05ae4f5ddb353517573dba07bc) | Convert absolute inspector edits to relative selection transforms | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |
| 3 | [d3f916bcc](https://github.com/icosa-foundation/open-brush/commit/d3f916bccad84d102e4fd450db5f3703d0d9afb1) | Interpolate inspector counts for multi-widget selections | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |
| 4 | [bda3296e4](https://github.com/icosa-foundation/open-brush/commit/bda3296e4a5a513739387c16a91b2abdfdecc8d3) | Generate finite model UVs and release compute buffers on failure | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |
| 5 | [aaf39f8cc](https://github.com/icosa-foundation/open-brush/commit/aaf39f8ccabc963dbabb2316c6b190236962f1d4) | Remove whitespace from UV generation buffer cleanup | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |
| 6 | [943fdbede](https://github.com/icosa-foundation/open-brush/commit/943fdbede0081a173833409f7a4836eed941169c) | Keep Inspector edits in the selection transform | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |
| 7 | [dd50d5a56](https://github.com/icosa-foundation/open-brush/commit/dd50d5a56bc613df3f631017f380781dd7d90fd6) | Include widgets in Inspector selection bounds | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-inspector/BRANCH-REVIEW.md>) |

<a id="branch-jitter-plugins"></a>

## 11. feature/jitter-plugins — PR #1027

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1027) · [Local branch review](<F:/Unity Projects/open-brush-jitter-plugins/BRANCH-REVIEW.md>)

4 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [51b62f33e](https://github.com/icosa-foundation/open-brush/commit/51b62f33eed29708f3f431fa9dcf9a1dc32039d8) | Reject unknown jitter script names before changing activation | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-jitter-plugins/BRANCH-REVIEW.md>) |
| 2 | [4f56b53dc](https://github.com/icosa-foundation/open-brush/commit/4f56b53dcffcbe525e627cbe039c75709ef7365e) | Bundle and document the color jitter script example | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-jitter-plugins/BRANCH-REVIEW.md>) |
| 3 | [d937f2d04](https://github.com/icosa-foundation/open-brush/commit/d937f2d048109e171e5f7d19b3ac1d37dec68f42) | Keep disabled script categories stopped during hot reload | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-jitter-plugins/BRANCH-REVIEW.md>) |
| 4 | [69caa7b1e](https://github.com/icosa-foundation/open-brush/commit/69caa7b1eb7e7c5abf2e877186ae7241ff6881d4) | End replaced Lua scripts during hot reload | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-jitter-plugins/BRANCH-REVIEW.md>) |

<a id="branch-mp-stroke-streaming"></a>

## 12. feature/mp-stroke-streaming — PR #1137

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1137) · [Local branch review](<F:/Unity Projects/open-brush-mp-stroke-streaming/BRANCH-REVIEW.md>)

9 commits; 8 distinct linked review comments.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [b6550cd78](https://github.com/icosa-foundation/open-brush/commit/b6550cd78d6e06b9af50fa0d121568988ea67f05) | Restore a finite sketch clock after timestamp quick-load | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310361) |
| 2 | [6f83010fb](https://github.com/icosa-foundation/open-brush/commit/6f83010fb274fa4d5cb3f642e402bb877c9cf33a) | Start timestamp and real-time playback safely for empty sketches | Original | [R7](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310368) |
| 3 | [8cbd6c071](https://github.com/icosa-foundation/open-brush/commit/8cbd6c071b08fac362d88d6cbb7ec3c6f81d79da) | Resolve real-time timeline sessions for strokes arriving during playback | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4184371124) |
| 4 | [c711c1571](https://github.com/icosa-foundation/open-brush/commit/c711c15714b6124ba1bf79123a00d85d8c866d38) | Keep multiplayer streaming planning documents untracked | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4006215350) |
| 5 | [9b329fecd](https://github.com/icosa-foundation/open-brush/commit/9b329fecde383561d1b94e556a1a629403d29d74) | Snapshot scene stroke membership before late-join capability discovery | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r3996228080) |
| 6 | [e819f2e96](https://github.com/icosa-foundation/open-brush/commit/e819f2e96295f8e232a75c3350cd5d0107ee2c82) | Complete outgoing live strokes after synchronous simplification finishes | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4057218855) |
| 7 | [159eba378](https://github.com/icosa-foundation/open-brush/commit/159eba37801e56cadb46a2a3e92cce4238929458) | Recompute owner voice policy after the authoritative data-room join | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r3996228079) |
| 8 | [6b4c605f4](https://github.com/icosa-foundation/open-brush/commit/6b4c605f4dfb8634d8917e89a680d8449efbdf0b) | Restore legacy-owner voice after capability timeout without overriding modern policy | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4006215363) |
| 9 | [8878a36b7](https://github.com/icosa-foundation/open-brush/commit/8878a36b765609da0773ff4254468251d3e70d01) | Snapshot multiplayer fallback recipients across awaited sends | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-mp-stroke-streaming/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/Multiplayer/MultiplayerManager.cs:279

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r3996228079) · Author: chatgpt-codex-connector · Created: 2026-09-12T12:43:39Z · Resolved

> **[P2]  Recompute voice state after establishing room ownership**
> 
> When `multiplayer.join` creates a room through the HTTP API on a fresh client, `isUserRoomOwner` is still false here because that path never calls `DoesRoomNameExist`; ownership is established only by `OnLocalPlayerJoined` during the later data-room join. Consequently this permanently sets `m_IsVoiceEnabled` false for the new owner, the voice join is skipped, and no remote owner exists to send a room-voice RPC that would correct it. Recompute the effective voice state after the data join has determined authoritative ownership.

Mapped commits: [159eba378](https://github.com/icosa-foundation/open-brush/commit/159eba37801e56cadb46a2a3e92cce4238929458).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342208422) explicitly references the mapped commit.

#### R2 — Assets/Scripts/Multiplayer/MultiplayerManager.cs:709

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r3996228080) · Author: chatgpt-codex-connector · Created: 2026-09-12T12:43:39Z · Resolved

> **[P2]  Deduplicate strokes drawn during capability discovery**
> 
> For a legacy late joiner, this deliberately waits the full two seconds before starting scene synchronization, but ordinary `OnCommandPerformed` delivery continues targeting that already-connected player during the wait. Any stroke completed in that interval is therefore received once through the legacy command RPC and then included again when `SendStrokesToPlayer` snapshots current memory; the large-data receive path adds every stroke without a GUID or seed duplicate check, producing duplicate geometry. Snapshot at join time, suspend delta delivery until synchronization completes, or deduplicate the overlap.

Mapped commits: [9b329fecd](https://github.com/icosa-foundation/open-brush/commit/9b329fecde383561d1b94e556a1a629403d29d74).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342209463) explicitly references the mapped commit.

#### R3 — LIVE-MULTIPLAYER-STROKE-STREAMING-PLAN.md:1

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4006215350) · Author: chatgpt-codex-connector · Created: 2026-09-14T14:29:03Z · Resolved · Outdated code location

> **[P1]  Remove temporary planning documents from the commit**
> 
> This commit adds both `LIVE-MULTIPLAYER-STROKE-STREAMING-PLAN.md` and `MULTIPLAYER-STROKE-CLOCK-COMPATIBILITY-PLAN.md`, including unfinished validation and future-work notes, even though repository policy explicitly requires temporary design and planning documents to remain untracked unless the user requests versioned project documentation. Remove these files from the commit.
> 
> AGENTS.md reference: [AGENTS.md:L3-L3](https://github.com/icosa-foundation/open-brush/blob/d268918771afb18d94f90cf92e96af8c2642adc2/AGENTS.md#L3-L3)

Mapped commits: [c711c1571](https://github.com/icosa-foundation/open-brush/commit/c711c15714b6124ba1bf79123a00d85d8c866d38).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342210630) explicitly references the mapped commit.

#### R4 — Assets/Scripts/Multiplayer/MultiplayerManager.cs:279

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4006215363) · Author: chatgpt-codex-connector · Created: 2026-09-14T14:29:03Z · Resolved

> **[P2]  Fall back to voice when the room owner is legacy**
> 
> When joining a room owned by a version that predates capability advertisement, `isUserRoomOwner` is false here, so this disables voice and skips joining the voice room. The legacy owner ignores the new capability command and cannot reply with `RPC_SetRoomVoiceEnabled`, leaving the new client without incoming or outgoing voice for the entire session even when voice is allowed. Add a compatibility timeout/default that joins voice when no authoritative response can be received from a legacy owner.

Mapped commits: [6b4c605f4](https://github.com/icosa-foundation/open-brush/commit/6b4c605f4dfb8634d8917e89a680d8449efbdf0b).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342211650) explicitly references the mapped commit.

#### R5 — Assets/Scripts/Multiplayer/MultiplayerManager.cs:1172

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4057218855) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:50:17Z · Resolved

> **[P2]  Wait for simplification before completing live strokes**
> 
> When user-stroke simplification is enabled for a supported brush, this check runs during `CommandPerformed`, while `MemorizeBatchedBrushStroke` has only initialized every drop flag to `false`; `PointerScript.DetachLine` does not recreate the stroke and calculate the actual flags until afterward. Consequently `fullyStreamedRecipients` is never cleared, and those peers finalize all control points with an all-false drop array while the sender subsequently simplifies its copy, producing different geometry and persisted stroke data. Complete the live stream after simplification, or transmit the finalized drop flags.

Mapped commits: [e819f2e96](https://github.com/icosa-foundation/open-brush/commit/e819f2e96295f8e232a75c3350cd5d0107ee2c82).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342212466) explicitly references the mapped commit.

#### R6 — Assets/Scripts/App.cs:2353

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310361) · Author: chatgpt-codex-connector · Created: 2026-10-04T11:31:24Z · Resolved · Outdated code location

> **[P1]  Reset the sketch clock after timestamp quick-load**
> 
> When Quick Load is invoked during timestamp playback, `QuickLoadRemaining` assigns `float.MaxValue` to `CurrentSketchTime`; the new clamped quick-load path now lets playback finish, but this condition skips restoring the clock specifically for `Timestamps`. Drawing then resumes with the enormous clock, so `BeginPendingStrokeTimeSession` and command timestamp casts produce invalid values that can corrupt saved timing metadata. Fresh evidence after the earlier overflow fix is that playback now completes while this timestamp-only branch still leaves `float.MaxValue` installed.

Mapped commits: [b6550cd78](https://github.com/icosa-foundation/open-brush/commit/b6550cd78d6e06b9af50fa0d121568988ea67f05).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342213345) explicitly references the mapped commit.

#### R7 — Assets/Scripts/SketchMemoryScript.cs:1490

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310368) · Author: chatgpt-codex-connector · Created: 2026-10-04T11:31:24Z · Resolved · Outdated code location

> **[P2]  Handle empty sketches before real-time fallback**
> 
> When `load.realtime` loads a valid sketch containing no strokes, `RealTimeStrokePlaybackTimeline.TryCreate` returns false and this fallback calls `GetEarliestTimestamp()`, which explicitly throws when the memory list is empty. The new endpoint therefore aborts instead of successfully loading a blank sketch; handle the empty collection before requesting its earliest timestamp.

Mapped commits: [6f83010fb](https://github.com/icosa-foundation/open-brush/commit/6f83010fb274fa4d5cb3f642e402bb877c9cf33a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342214199) explicitly references the mapped commit.

#### R8 — Assets/Scripts/Playback/ScenePlaybackByTime.cs:124

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4184371124) · Author: chatgpt-codex-connector · Created: 2026-10-05T13:14:25Z · Resolved · Outdated code location

> **[P2]  Support strokes added during real-time playback**
> 
> When `load.realtime` is still animating and a multiplayer stroke arrives, `MemoryListAdd` passes the new stroke to `ScenePlaybackByTimeLayered.AddStroke`, but this dictionary contains only the strokes captured by `TryCreate` at playback startup. Inserting into the rendered sorted list—or later rewinding it—therefore calls this lookup for the new stroke and throws `KeyNotFoundException`, interrupting playback/network processing. Register newly added strokes with the real-time timeline or resolve their session dynamically.

Mapped commits: [8cbd6c071](https://github.com/icosa-foundation/open-brush/commit/8cbd6c071b08fac362d88d6cbb7ec3c6f81d79da).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4342215029) explicitly references the mapped commit.

<a id="branch-multipath-toolscript-previews"></a>

## 13. feature/multipath-toolscript-previews — PR #1164

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1164) · [Local branch review](<F:/Unity Projects/open-brush-multipath-toolscript-previews/BRANCH-REVIEW.md>)

3 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [cfde2400d](https://github.com/icosa-foundation/open-brush/commit/cfde2400dd7db45b6382112c558396f496ca9310) | Preview tool script symmetry copies and preserve their source colors | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-multipath-toolscript-previews/BRANCH-REVIEW.md>) |
| 2 | [837d27a8e](https://github.com/icosa-foundation/open-brush/commit/837d27a8ef4c5012ed042f69fe5c64fcfc793d5e) | Rebuild tool-script preview brushes when the brush changes | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-multipath-toolscript-previews/BRANCH-REVIEW.md>) |
| 3 | [65024639d](https://github.com/icosa-foundation/open-brush/commit/65024639dfe10f68cab7b671e6b713ae5efaee0f) | Expose all Tool Script preview control-point paths | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-multipath-toolscript-previews/BRANCH-REVIEW.md>) |

<a id="branch-native-imm"></a>

## 14. feature/native-imm — PR #1192

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1192) · [Local branch review](<F:/Unity Projects/open-brush-native-imm/BRANCH-REVIEW.md>)

19 commits; 19 distinct linked review comments.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [ed78f17b1](https://github.com/icosa-foundation/open-brush/commit/ed78f17b16436ccea66aa9e09b6945fa15115179) | Show native Quill import only when an IMM source file exists | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4264820507) |
| 2 | [8638b4eeb](https://github.com/icosa-foundation/open-brush/commit/8638b4eeb846fa33dd9f590e19df6137cfa8dbef) | Unload native IMM before restoring a replacement sketch environment | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053283) |
| 3 | [3790d6788](https://github.com/icosa-foundation/open-brush/commit/3790d6788954f786a4a4ceb269201a2b1d88c1bc) | Bound whole-sketch native IMM loading to a two-minute deadline | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541402) |
| 4 | [18ede7cd9](https://github.com/icosa-foundation/open-brush/commit/18ede7cd9bee7cf40359b290b2fa7883d98a2b97) | Track explicit native playback edits with snapshot-aware sketch save revisions | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053294), [R7](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541416) |
| 5 | [57bd906bc](https://github.com/icosa-foundation/open-brush/commit/57bd906bc6f13ac6a090bcc1bf2dac90dcef0178) | Preserve saved native IMM stop timestamps during playback restoration | Original | [R9](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4306910297) |
| 6 | [194c90c8f](https://github.com/icosa-foundation/open-brush/commit/194c90c8fe001bd01171e3e763caa73db611f24b) | Reject incomplete native viewpoint snapshot retries | Original | [R11](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4335695143) |
| 7 | [fb1145d51](https://github.com/icosa-foundation/open-brush/commit/fb1145d51fafd4c111dd2eb67fad9f6334acac9b) | Undo model viewpoint resizing and scene movement as one operation | Original | [R12](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342145641) |
| 8 | [270259a79](https://github.com/icosa-foundation/open-brush/commit/270259a79132d5bd3f5ab681f625ffb0fef25d51) | Wrap playable animation sample times and rewind completed clips on Play | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4303764969), [R10](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4335695127) |
| 9 | [5191ba18a](https://github.com/icosa-foundation/open-brush/commit/5191ba18a7c7d4ed7446c146f5c6146c8d9afac1) | Register native IMM rendering for screenshot and capture cameras | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4276887936) |
| 10 | [7a48bd71a](https://github.com/icosa-foundation/open-brush/commit/7a48bd71a3fce6ee5c9374f0497eaaafa40fc3b3) | Persist IMM gallery model format and restore through the IMM-only loader | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541391) |
| 11 | [7a2e48a4c](https://github.com/icosa-foundation/open-brush/commit/7a2e48a4c4a9f838c5e0f612f896ce75a73218aa) | Preserve pending native document unloads when a waiting import is canceled | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541402) |
| 12 | [7a7be3979](https://github.com/icosa-foundation/open-brush/commit/7a7be397964b0a36f8d63a9a90e0ba9cb591f419) | Preserve mixed model playback position and playing state | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-native-imm/BRANCH-REVIEW.md>) |
| 13 | [787000641](https://github.com/icosa-foundation/open-brush/commit/7870006414a710615b9bead9a6b66794a6be6043) | Preserve legacy gallery download format preference | Update | [R13](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4361408516) |
| 14 | [a317bb8b0](https://github.com/icosa-foundation/open-brush/commit/a317bb8b06fc93b9f70a4fce587b29c141324a0d) | Bound native document unload waits during sketch restoration | Update | [R14](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4361408522) |
| 15 | [c9eab7318](https://github.com/icosa-foundation/open-brush/commit/c9eab7318fb0d0a67f3d38e9644c0873de625e3e) | Schedule autosave for authored playback edits without undo commands | Update | [R15](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4364793520) |
| 16 | [9f514b56a](https://github.com/icosa-foundation/open-brush/commit/9f514b56afff65cccceef54a5925c99911ae3eac) | Bound native IMM document readiness waits during sketch restore | Update | [R16](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4364793513) |
| 17 | [58f45ffd8](https://github.com/icosa-foundation/open-brush/commit/58f45ffd8e60c11c518abac57a9692770b1ad300) | Keep cached IMM files out of legacy model load requests | Update | [R17](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365880266) |
| 18 | [05cf3fb6e](https://github.com/icosa-foundation/open-brush/commit/05cf3fb6eb8d6597d71324896a46d91620f2e172) | Release displaced catalog model ownership when publishing IMM | Update | [R18](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365880269) |
| 19 | [378ac7c1b](https://github.com/icosa-foundation/open-brush/commit/378ac7c1bb0d1f43df8c7de410af76ee5f378648) | Restore playback position when delayed glTF audio clips arrive | Update | [R19](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365880262) |

### Review comment text

#### R1 — Assets/Scripts/GUI/QuillLibraryPanel.cs:237

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4264820507) · Author: chatgpt-codex-connector · Created: 2026-10-13T22:40:59Z · Resolved · Outdated code location

> **[P2]  Hide the native action when no IMM file is available**
> 
> For every selected Quill project this unconditionally exposes and enables the native-IMM action, although the catalog recognizes a valid Quill project solely by `Quill.json`. If that directory has no top-level `*.imm`, the handler passes the directory itself to `CanLoad`, so clicking the enabled action only logs “IMM file not found” and does nothing. Gate the action on an IMM source or on finding an actual embedded IMM file.

Mapped commits: [ed78f17b1](https://github.com/icosa-foundation/open-brush/commit/ed78f17b16436ccea66aa9e09b6945fa15115179).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342308523) explicitly references the mapped commit.

#### R2 — Assets/Scripts/Save/SaveLoadScript.cs:894

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053283) · Author: chatgpt-codex-connector · Created: 2026-10-14T11:16:12Z · Resolved · Outdated code location

> **[P1]  Unload the native IMM when replacing the scene**
> 
> When a native IMM is active and the user opens an ordinary `.tilt` sketch, `LoadSketch` does not call `NewSketch`, and this conditional only replaces the native document when the new sketch also embeds one. The previous `s_Active` document therefore keeps rendering over the newly loaded sketch; it also causes the earlier environment load to be rejected by `AllowsEnvironment`. Unload the active native document for every successful non-additive scene replacement, including sketches without `NativeImmFile`.

Mapped commits: [8638b4eeb](https://github.com/icosa-foundation/open-brush/commit/8638b4eeb846fa33dd9f590e19df6137cfa8dbef).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342309580) explicitly references the mapped commit.

#### R3 — Assets/Scripts/NativeImmImport.cs:160

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053294) · Author: chatgpt-codex-connector · Created: 2026-10-14T11:16:12Z · Resolved

> **[P2]  Register native imports as save-relevant changes**
> 
> A direct native load follows `NewSketch`, which clears the operation stack, but creating `s_Active` records no command or other dirty state. Consequently a native-only document with a black backdrop has no strokes, widgets, or `EnvironmentChanged`, so `SketchHasChanges()` leaves Save New and New Sketch unavailable; moreover, subsequent viewpoint changes still leave `IsMemoryDirty()` false, allowing another Quill load to discard the saved pose without an unsaved-changes prompt. Native document presence and pose changes need to participate in the sketch's save/dirty lifecycle.

Mapped commits: [18ede7cd9](https://github.com/icosa-foundation/open-brush/commit/18ede7cd9bee7cf40359b290b2fa7883d98a2b97).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342310775) explicitly references the mapped commit.

#### R4 — Assets/Scripts/NativeImmImport.cs:496

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4276887936) · Author: chatgpt-codex-connector · Created: 2026-10-14T22:19:45Z · Resolved · Outdated code location

> **[P2]  Render native IMM content through capture cameras**
> 
> When saving a thumbnail or using another capture camera, `ScreenshotManager.RenderToTexture` renders its own `LeftInfo.camera`, not the VR camera stored here. On the compatibility path this callback explicitly ignores that camera, while on the RenderGraph path only the VR camera receives an `ImmCamera`, so native IMM geometry is omitted from save thumbnails, hi-res/GIF captures, and other secondary-camera output even though it is visible in the headset. Register or render the IMM for each capture camera as well.

Mapped commits: [5191ba18a](https://github.com/icosa-foundation/open-brush/commit/5191ba18a7c7d4ed7446c146f5c6146c8d9afac1).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342311706) explicitly references the mapped commit.

#### R5 — Assets/Scripts/Poly/IcosaAssetCatalog.cs:235

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541391) · Author: chatgpt-codex-connector · Created: 2026-10-16T15:50:59Z · Resolved

> **[P1]  Persist the selected IMM format for gallery references**
> 
> When an Icosa asset offers both IMM and another preferred format, a widget inserted through `IcosaModelButton` uses the IMM, but its save metadata retains only the asset ID. On a fresh machine, restoration calls the ordinary mixed-format downloader; `TryGetDownloadFormat` searches `isPreferredForDownload` formats before honoring this ordering, so a preferred glTF/VOX representation can be restored instead of the saved IMM, silently changing the model and losing IMM-specific playback/viewpoints. Persist the chosen format or explicitly request IMM when restoring an IMM reference.

Mapped commits: [7a48bd71a](https://github.com/icosa-foundation/open-brush/commit/7a48bd71a3fce6ee5c9374f0497eaaafa40fc3b3).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342317384) explicitly references the mapped commit.

#### R6 — Assets/Scripts/SketchControlsScript.cs:4349

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541402) · Author: chatgpt-codex-connector · Created: 2026-10-16T15:51:00Z · Resolved

> **[P2]  Bound the native IMM loading wait**
> 
> When the native player leaves a document in `Loading` indefinitely—or the preceding document never finishes unloading—`NativeImmImport.IsLoading` never becomes false, so the `GlobalCommands.LoadQuillFile` path keeps `RunInCompositor` active forever after already clearing the sketch. The reference-widget loader has a 120-second failure timeout, but this whole-sketch path has none; add a deadline that unloads and reports the failure so the user can leave the loading overlay.

Mapped commits: [3790d6788](https://github.com/icosa-foundation/open-brush/commit/3790d6788954f786a4a4ceb269201a2b1d88c1bc), [7a2e48a4c](https://github.com/icosa-foundation/open-brush/commit/7a2e48a4c4a9f838c5e0f612f896ce75a73218aa).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342318441) explicitly references the mapped commits.

#### R7 — Assets/Scripts/ModelPlaybackController.cs:145

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541416) · Author: chatgpt-codex-connector · Created: 2026-10-16T15:51:00Z · Resolved · Outdated code location

> **[P2]  Record playback edits as save-relevant changes**
> 
> When HTTP or Lua changes a model's pause state, time, volume, clip, loop, or chapter in an already-loaded sketch, these mutators update only the playback component and never add a save-relevant command. `SketchMemoryScript.IsMemoryDirty()` therefore remains false: overwrite Save stays disabled and loading another sketch skips the unsaved-changes prompt, even though `PlaybackStates` would serialize the new values if a save were initiated for some unrelated reason. Mark successful playback mutations dirty or record an undoable command.

Mapped commits: [18ede7cd9](https://github.com/icosa-foundation/open-brush/commit/18ede7cd9bee7cf40359b290b2fa7883d98a2b97).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342319375) explicitly references the mapped commit.

#### R8 — Assets/Scripts/PlayGltfAnimationClip.cs:96

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4303764969) · Author: chatgpt-codex-connector · Created: 2026-10-17T17:38:55Z · Resolved

> **[P2]  Implement looping instead of extending playable duration**
> 
> For non-legacy glTF clips, setting `model.loop=true` only changes the playable's duration; it does not make the animation clip wrap. Unity documents [`SetDuration`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Playables.PlayableExtensions.SetDuration.html) as changing duration, while looping is controlled by the clip's loop setting. Consequently, a non-looping imported clip remains visually on its final frame even though `Time` starts reporting modulo-wrapped values, so the HTTP/Lua loop control reports a loop that is not actually occurring.

Mapped commits: [270259a79](https://github.com/icosa-foundation/open-brush/commit/270259a79132d5bd3f5ab681f625ffb0fef25d51).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342324875) explicitly references the mapped commit.

#### R9 — Assets/Scripts/ModelPlaybackController.cs:284

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4306910297) · Author: chatgpt-codex-connector · Created: 2026-10-18T11:53:05Z · Resolved · Outdated code location

> **[P2]  Restore the saved native stop time**
> 
> When restoring a paused IMM or cloning an IMM with `restorePlayingPosition: true`, `ModelPlaybackState.ForRestore` deliberately preserves the nonzero `StopTime` returned by `ImmDocument.GetTime`, but this call always supplies `0` as the stop timestamp. The restored document therefore loses its saved playback interval or pending authored stop, so continuing it can play past the point represented by the serialized state; pass the validated saved stop time to `RequestSetTime` instead.

Mapped commits: [57bd906bc](https://github.com/icosa-foundation/open-brush/commit/57bd906bc6f13ac6a090bcc1bf2dac90dcef0178).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342325926) explicitly references the mapped commit.

#### R10 — Assets/Scripts/PlayGltfAnimationClip.cs:59

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4335695127) · Author: chatgpt-codex-connector · Created: 2026-10-21T10:26:12Z · Resolved

> **[P2]  Rewind completed Playable clips when Play is requested**
> 
> For a non-looping, non-legacy glTF clip that has reached its final frame, setting `Playing` to true only calls `graph.Play()` without resetting `m_Playable`'s time. Since the graph can remain in its playing state after its clip is done, `Playing` also continues to report true while the model is frozen, and the UI/API Play action cannot restart the animation as the legacy branch does. Detect completion and rewind the playable before resuming.

Mapped commits: [270259a79](https://github.com/icosa-foundation/open-brush/commit/270259a79132d5bd3f5ab681f625ffb0fef25d51).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342327022) explicitly references the mapped commit.

#### R11 — Assets/Scripts/ModelPlaybackController.cs:421

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4335695143) · Author: chatgpt-codex-connector · Created: 2026-10-21T10:26:12Z · Resolved · Outdated code location

> **[P2]  Reject a second failed viewpoint snapshot**
> 
> When viewpoint availability changes between the initial undersized-buffer call and this retry, the second `TryGetSpawnAreaSnapshot` can also return false, but its result is ignored and the incomplete snapshot is returned. Callers then use `snapshot.availableCount` as the valid length for `m_ViewpointIds`; the model-panel refresh does this every frame, so an increased count can make `Array.IndexOf` throw and break the playback controls. Retry with the newly required size or report the snapshot as unavailable instead of consuming it.

Mapped commits: [194c90c8f](https://github.com/icosa-foundation/open-brush/commit/194c90c8fe001bd01171e3e763caa73db611f24b).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342327973) explicitly references the mapped commit.

#### R12 — Assets/Scripts/ModelPlaybackController.cs:334

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342145641) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:26:11Z · Resolved · Outdated code location

> **[P2]  Include the scene move in viewpoint undo**
> 
> When a user navigates to an IMM viewpoint, only the model resize is recorded in `MoveWidgetCommand`; the following direct assignment to `App.Scene.Pose` is outside the command. Undo therefore restores the model's old transform while leaving the scene translated and rotated to the viewpoint, producing a partial undo that no longer preserves the authored view. Record both transformations as one undoable operation, or avoid putting only the model half on the undo stack.

Mapped commits: [fb1145d51](https://github.com/icosa-foundation/open-brush/commit/fb1145d51fafd4c111dd2eb67fad9f6334acac9b).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342328975) explicitly references the mapped commit.


#### R13 — Assets/Scripts/Poly/IcosaAssetCatalog.cs:235

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4361408516) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:47:16Z · Resolved · Outdated code location

> **[P1] Preserve legacy gallery format selection**
>
> Keep IMM out of the ordinary gallery download preference: on a fresh machine, an older sketch has no `ImmModel` flag and therefore restores through `RequestModelLoad`, but this newly preferred format can select IMM for a mixed-format asset when no non-IMM format is marked preferred. Such sketches previously restored VOX/glTF/OBJ and will now silently load a different representation with different geometry and playback behavior; only the explicit `ImmModel` restoration path should request IMM.

Mapped commits: [787000641](https://github.com/icosa-foundation/open-brush/commit/7870006414a710615b9bead9a6b66794a6be6043).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4364775685) cites [787000641](https://github.com/icosa-foundation/open-brush/commit/7870006414a710615b9bead9a6b66794a6be6043).

#### R14 — Assets/Scripts/NativeImmImport.cs:220

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4361408522) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:47:16Z · Resolved

> **[P2] Bound unload waits during saved IMM restoration**
>
> Add a deadline to this wait when restoring an IMM-backed `.tilt` immediately after another native sketch: if the old native document never becomes inactive, `GetSketchModelAsync` waits for this coroutine indefinitely and `CreateMediaWidgetsFromLoadDataCoroutine` never finishes, leaving the standard sketch-loading overlay stuck. The new evidence beyond the previously fixed direct-file path is that `SaveLoadScript.LoadFromSketch` reaches this loop without going through `LoadNativeImmCoroutine` and its 120-second timeout.

Mapped commits: [a317bb8b0](https://github.com/icosa-foundation/open-brush/commit/a317bb8b06fc93b9f70a4fce587b29c141324a0d).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4364776229) cites [a317bb8b0](https://github.com/icosa-foundation/open-brush/commit/a317bb8b06fc93b9f70a4fce587b29c141324a0d).

#### R15 — Assets/Scripts/SketchMemoryScript.cs:305

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4364793520) · Author: chatgpt-codex-connector · Created: 2026-10-23T22:36:51Z · Resolved

> **[P2]  Schedule autosaves for unrecorded playback edits**
> 
> After an autosave completes, changing only a model's playback state, chapter, or volume increments this revision but never schedules another autosave. `SaveLoadScript` schedules autosaves through its `OperationStackChanged` subscription or active stroke creation; neither occurs for these new unrecorded edits. Consequently, crash recovery retains stale playback settings indefinitely even though the sketch is correctly marked dirty for manual saving. Notify the autosave lifecycle when marking an unrecorded change without treating internal restoration or per-frame playback as edits.

Mapped commits: [c9eab7318](https://github.com/icosa-foundation/open-brush/commit/c9eab7318fb0d0a67f3d38e9644c0873de625e3e).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365866488) explicitly references the mapped commit.


#### R16 — Assets/Scripts/NativeImmImport.cs:193

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4364793513) · Author: chatgpt-codex-connector · Created: 2026-10-23T22:36:51Z · Resolved

> **[P2]  Bound document loading during saved IMM restoration**
> 
> When restoring an IMM-backed `.tilt`, if the native document remains loading or never becomes sequence-ready, this loop never completes because `m_Model` is assigned only after preparation succeeds. `CreateMediaWidgetsFromLoadDataCoroutine` awaits this task, so the sketch-loading overlay remains stuck indefinitely. The saved-sketch path bypasses `LoadNativeImmCoroutine` and its deadline; the timeout in `BeginLoadAfterUnload` covers only the previous document's unloading, not the new document's loading. Add a deadline that unloads the pending import and lets widget restoration report failure.

Mapped commits: [9f514b56a](https://github.com/icosa-foundation/open-brush/commit/9f514b56afff65cccceef54a5925c99911ae3eac).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365870567) explicitly references the mapped commit.

#### R17 — Assets/Scripts/Poly/IcosaAssetCatalog.cs:1216

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365880266) · Author: chatgpt-codex-connector · Created: 2026-10-24T03:45:21Z · Resolved

> **[P1]  Keep cached IMM files out of legacy model restoration**
> 
> When an IMM representation has previously been downloaded and the ordinary model cache has no entry for that asset, this branch makes `RequestModelLoad` select the cached IMM even for an older sketch without `ImmModel`. `CreateModelsFromAssetId` then restores that native representation instead of the original glTF/VOX/OBJ, potentially changing geometry and invalidating saved subtree paths. The fresh evidence beyond the previously fixed download-preference issue is this explicit `GetCachedImmPath` shortcut, which bypasses `GetSupportedIcosaFormats` entirely. Restrict this cache lookup to explicit IMM requests so legacy restoration retains its original format selection.

Mapped commits: [58f45ffd8](https://github.com/icosa-foundation/open-brush/commit/58f45ffd8e60c11c518abac57a9692770b1ad300).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365890240) explicitly references the mapped commit.


#### R18 — Assets/Scripts/Poly/IcosaAssetCatalog.cs:873

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365880269) · Author: chatgpt-codex-connector · Created: 2026-10-24T03:45:21Z · Resolved

> **[P2]  Release the previous model when replacing its catalog entry**
> 
> If a mixed-format asset already has a loaded non-IMM model, requesting its IMM representation overwrites the sole catalog entry without calling `ReleaseFromCatalog` on the previous model. Its inactive template hierarchy and procedural resources remain allocated, and removing its last widget does not unload them because `Model.ReleaseUsage` only does so after catalog ownership has been released. The old model is also no longer reachable by catalog eviction or teardown. Release the displaced model's catalog ownership before replacing it, allowing existing widgets to retain it until their final release.

Mapped commits: [05cf3fb6e](https://github.com/icosa-foundation/open-brush/commit/05cf3fb6eb8d6597d71324896a46d91620f2e172).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365891691) explicitly references the mapped commit.


#### R19 — Assets/Scripts/ModelPlaybackController.cs:126

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365880262) · Author: chatgpt-codex-connector · Created: 2026-10-24T03:45:21Z · Resolved

> **[P2]  Wait for audio readiness before completing playback restoration**
> 
> When restoring a glTF model containing both animation and audio, `Initialized` becomes true as soon as the animation is ready, while `GltfAudioSource.LoadAndPlay` may still be awaiting its audio request. `SeekInternal` skips sources without clips, but this branch then clears the saved state permanently. A sketch saved paused at a nonzero time therefore restores the animation correctly while its audio remains at zero; cloning a playing model can likewise desynchronize the audio. Retain the pending restoration until all relevant audio sources have loaded, or apply the saved position individually as each source becomes ready.

Mapped commits: [378ac7c1b](https://github.com/icosa-foundation/open-brush/commit/378ac7c1bb0d1f43df8c7de410af76ee5f378648).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4365900626) explicitly references the mapped commit.


<a id="branch-polling-stroke-listener"></a>

## 15. feature/polling-stroke-listener — PR #1025

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1025) · [Local branch review](<F:/Unity Projects/open-brush-polling-stroke-listener/BRANCH-REVIEW.md>)

2 commits; 1 distinct linked review comments.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [646b88d3e](https://github.com/icosa-foundation/open-brush/commit/646b88d3ee2d9dd9ccefdee7b6915cca72318d2d) | Decode polling POST command values once | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1025#discussion_r4006159104) |
| 2 | [0911c04fc](https://github.com/icosa-foundation/open-brush/commit/0911c04fc0eab475cd296476a2b768831ad0d75f) | Expire inactive polling stroke listeners | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-polling-stroke-listener/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/API/ApiManager.cs:703

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1025#discussion_r4006159104) · Author: chatgpt-codex-connector · Created: 2026-09-14T14:23:11Z · Resolved · Outdated code location

> **[P2]  Decode POST command values only once**
> 
> When a polling client ID contains a literal percent escape (for example, `team%20one`) and registration is sent as form-encoded POST, `ParseFormCommands` decodes `team%2520one` to `team%20one` here, then `EnqueueCommand` decodes it again and registers `team one`; the polling GET decodes its value only once and looks up `team%20one`, so it always reports that the listener is unregistered. Fresh evidence is that the new test exercises `ParseFormCommands` directly but never passes its result through `EnqueueCommand`; decode each POST field only once end-to-end.

Mapped commits: [646b88d3e](https://github.com/icosa-foundation/open-brush/commit/646b88d3ee2d9dd9ccefdee7b6915cca72318d2d).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1025#discussion_r4341926013) explicitly references the mapped commit.

<a id="branch-sculpting"></a>

## 16. feature/sculpting — PR #857

[Pull request](https://github.com/icosa-foundation/open-brush/pull/857) · [Local branch review](<F:/Unity Projects/open-brush-sculpting/BRANCH-REVIEW.md>)

11 commits; 12 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [5401a4979](https://github.com/icosa-foundation/open-brush/commit/5401a4979e5ea8199b99a95b3d9fb55dc08bba7c) | Validate sculpted vertex and normal payloads before saving | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552121), [R12](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552567) |
| 2 | [04699457d](https://github.com/icosa-foundation/open-brush/commit/04699457d293bca54735c7b129842b6519485e02) | Recover safely from missing or mismatched sculpt geometry | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552194), [R3](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552220) |
| 3 | [31da0901a](https://github.com/icosa-foundation/open-brush/commit/31da0901a655926bf71306f3130cfcbb40b70cbe) | Retain edited-mesh state only when sculpt geometry is reapplied | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552308) |
| 4 | [b9870bbd9](https://github.com/icosa-foundation/open-brush/commit/b9870bbd90b1f912ea228940ce935c8eee7f6271) | Encapsulate and guard legacy sculpt subtool selection | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552347), [R6](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552361), [R7](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552388) |
| 5 | [ae5e4e1fb](https://github.com/icosa-foundation/open-brush/commit/ae5e4e1fbf527912f755c404ec9e22ed33306f21) | Emit sculpt feedback once per modified stroke | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552416) |
| 6 | [671bd503a](https://github.com/icosa-foundation/open-brush/commit/671bd503a5efe1d68170fe8fd9609af66d02ca65) | Compute sculpt rotation axis without mutating the tool transform | Original | [R9](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552464) |
| 7 | [4c93c04a1](https://github.com/icosa-foundation/open-brush/commit/4c93c04a19426a20796a29958150e7516cf1a7fd) | Use the Batch geometry property from sculpting code | Original | [R10](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552513) |
| 8 | [57b5fd7ba](https://github.com/icosa-foundation/open-brush/commit/57b5fd7baa2b9985a54282b517a5c4b72999c982) | Remove unused mesh saver collection imports | Original | [R11](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552553) |
| 9 | [910297acb](https://github.com/icosa-foundation/open-brush/commit/910297acb721dd23098e6ec795f208190c3c1a49) | Use the empty sculpt payload sentinel for zero-vertex geometry | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552121) |
| 10 | [ef2ae5b16](https://github.com/icosa-foundation/open-brush/commit/ef2ae5b16e60abaf949557d146ecc14233cabf8e) | Resolve current stroke geometry when undoing sculpt edits | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-sculpting/BRANCH-REVIEW.md>) |
| 11 | [6f25ff687](https://github.com/icosa-foundation/open-brush/commit/6f25ff6872f26c2c5d7e93adc3c0054f19b7f4eb) | Restore prior sculpt edited state on undo | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-sculpting/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/Save/SketchWriter.cs:381

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552121) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:31Z · Resolved · Outdated code location

> This writer only checks `vertices != null` before writing sculpted geometry, but then unconditionally reads `normals.Count` and iterates normals. If normals is null (or counts differ), this will throw or write inconsistent data. Consider guarding both lists and validating equal counts before writing; otherwise write a 0 length.

Mapped commits: [5401a4979](https://github.com/icosa-foundation/open-brush/commit/5401a4979e5ea8199b99a95b3d9fb55dc08bba7c), [910297acb](https://github.com/icosa-foundation/open-brush/commit/910297acb721dd23098e6ec795f208190c3c1a49).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342105265), [resolution reply 2](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342237427) explicitly references the mapped commits.

#### R2 — Assets/Scripts/SketchMemoryScript.cs:1277

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552194) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:32Z · Resolved · Outdated code location

> `RestoreAllSculptedGeometry()` assumes `m_SavedSculptedGeometry` has at least one entry for every `stroke.m_MeshIsEdited` stroke. If the queue count is smaller than expected (corrupt/partial file), `Dequeue()` will throw and abort load. Also `index` is unused. Add a Count guard and bail out gracefully.

Mapped commits: [04699457d](https://github.com/icosa-foundation/open-brush/commit/04699457d293bca54735c7b129842b6519485e02).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342107538) explicitly references the mapped commit.

#### R3 — Assets/Scripts/SketchMemoryScript.cs:1237

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552220) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:32Z · Resolved · Outdated code location

> `InsertSculptedGeometry` validates vertex count but not normals count. If normals are missing or mismatched, the loop will throw with an out-of-range access when assigning `m_Normals`. Validate normals length as well before applying.

Mapped commits: [04699457d](https://github.com/icosa-foundation/open-brush/commit/04699457d293bca54735c7b129842b6519485e02).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342108638) explicitly references the mapped commit.

#### R4 — Assets/Scripts/Commands/RepaintStrokeCommand.cs:93

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552308) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:33Z · Resolved · Outdated code location

> This block sets `stroke.m_MeshIsEdited = true` before attempting to reapply geometry, even if the recreated stroke has a different vertex count/topology (eg, brush/size changed) and `InsertSculptedGeometry` will no-op. Add an explicit topology check (including normals) and only keep `m_MeshIsEdited` true when the sculpt data can actually be applied.

Mapped commits: [31da0901a](https://github.com/icosa-foundation/open-brush/commit/31da0901a655926bf71306f3130cfcbb40b70cbe).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342112520) explicitly references the mapped commit.

#### R5 — Assets/Scripts/Tools/SculptTool.cs:33

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552347) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:33Z · Resolved · Outdated code location

> `m_ActiveSubTool` is serialized but publicly mutable. Since other code assumes it is always non-null, keeping it private helps prevent accidental reassignment and makes it easier to enforce invariants (and add null guards).

Mapped commits: [b9870bbd9](https://github.com/icosa-foundation/open-brush/commit/b9870bbd90b1f912ea228940ce935c8eee7f6271).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342113508) explicitly references the mapped commit.

#### R6 — Assets/Scripts/Tools/SculptTool.cs:58

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552361) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:33Z · Resolved · Outdated code location

> `HideTool` and `SetSubTool` assume `m_ActiveSubTool` is non-null and that switching subtools only needs to disable the old one. In practice, switching subtools while the tool is visible will leave the new subtool inactive (and can NRE if the reference is missing). Activate the new subtool based on current hidden state and add null guards.

Mapped commits: [b9870bbd9](https://github.com/icosa-foundation/open-brush/commit/b9870bbd90b1f912ea228940ce935c8eee7f6271).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342114567) explicitly references the mapped commit.

#### R7 — Assets/Scripts/Tools/SculptTool.cs:93

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552388) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:34Z · Resolved · Outdated code location

> `HandleIntersectionWithBatchedStroke` declares `var stroke = rGroup.m_Stroke;` but never uses it, causing an unused-variable warning.

Mapped commits: [b9870bbd9](https://github.com/icosa-foundation/open-brush/commit/b9870bbd90b1f912ea228940ce935c8eee7f6271).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342115706) explicitly references the mapped commit.

#### R8 — Assets/Scripts/Tools/SculptTool.cs:121

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552416) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:34Z · Resolved · Outdated code location

> The per-vertex loop triggers audio and haptics for every vertex that moves and uses `Vector3.Distance` (sqrt) each iteration. This can be very expensive for dense strokes and can spam haptics. Consider using squared distance and triggering sound/haptics once per stroke if any vertex was modified.

Mapped commits: [ae5e4e1fb](https://github.com/icosa-foundation/open-brush/commit/ae5e4e1fbf527912f755c404ec9e22ed33306f21).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342116737) explicitly references the mapped commit.

#### R9 — Assets/Scripts/Tools/Sculpting/RotateSubTool.cs:42

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552464) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:34Z · Resolved · Outdated code location

> `RotateSubTool.ManipulateVertex` temporarily mutates `toolTransform.rotation` for every vertex and then restores it. This introduces avoidable side effects and extra work inside a hot loop. Compute the rotation axis in canvas space without modifying the transform.

Mapped commits: [671bd503a](https://github.com/icosa-foundation/open-brush/commit/671bd503a5efe1d68170fe8fd9609af66d02ca65).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342117767) explicitly references the mapped commit.

#### R10 — Assets/Scripts/Batching/Batch.cs:32

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552513) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:35Z · Resolved · Outdated code location

> `m_Geometry` was changed from private to public, but `Batch` already exposes `public GeometryPool Geometry { get { return m_Geometry; } }`. Prefer keeping the field private and updating call sites to use the property (also: typo "scultping").

Mapped commits: [4c93c04a1](https://github.com/icosa-foundation/open-brush/commit/4c93c04a19426a20796a29958150e7516cf1a7fd).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342119843) explicitly references the mapped commit.

#### R11 — Assets/MeshSaver/Editor/MeshSaverEditor.cs:4

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552553) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:35Z · Resolved · Outdated code location

> Unused `using System.Collections` / `using System.Collections.Generic` directives (nothing from these namespaces is referenced). Removing them avoids warnings and keeps the file tidy.

Mapped commits: [57b5fd7ba](https://github.com/icosa-foundation/open-brush/commit/57b5fd7baa2b9985a54282b517a5c4b72999c982).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342122051) explicitly references the mapped commit.

#### R12 — Assets/Scripts/Save/SketchWriter.cs:96

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552567) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:31:36Z · Resolved · Outdated code location

> Comment says the sculpted payload stores "vertices and triangles", but the struct/type and serialization logic store vertices + normals (no triangles). Update the comment to avoid misleading future format changes.

Mapped commits: [5401a4979](https://github.com/icosa-foundation/open-brush/commit/5401a4979e5ea8199b99a95b3d9fb55dc08bba7c).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r4342123039) explicitly references the mapped commit.

<a id="branch-sdf-guides"></a>

## 17. feature/sdf-guides — PR #1119

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1119) · [Local branch review](<F:/Unity Projects/open-brush-sdf-guides/BRANCH-REVIEW.md>)

41 commits; 49 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [0d8356ab7](https://github.com/icosa-foundation/open-brush/commit/0d8356ab77fdc3d52131b02f43da2850989445df) | Invalidate guide distance cache when guides show or hide | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3952875893), [R26](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363934) |
| 2 | [480fffaea](https://github.com/icosa-foundation/open-brush/commit/480fffaeaf4f1ca30dccbcedc9a49aa2279b8195) | Measure guide distances using the owning canvas and finite plane surface | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069366), [R17](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530520) |
| 3 | [15bd80797](https://github.com/icosa-foundation/open-brush/commit/15bd8079718df3a94c394927412ccf919b2faaf2) | Check projected guide points against the combined volume boundary | Original | [R20](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804669) |
| 4 | [ab1a2223a](https://github.com/icosa-foundation/open-brush/commit/ab1a2223ad012d9ea7f6aded22eb506eef1ebdc1) | Transform HTTP guide queries and explicit SDF poses through scene space | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3959800195), [R7](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3959800203) |
| 5 | [7da4580f1](https://github.com/icosa-foundation/open-brush/commit/7da4580f1d8a76713dcbd4ae159bb0ac517d5277) | Preserve complete world scales when converting models and SDF guides | Original | [R25](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4177451766), [R28](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363957) |
| 6 | [7782c8531](https://github.com/icosa-foundation/open-brush/commit/7782c85316a4af44b1042270150d98171c522ced) | Clear empty SDF previews and reject stale conversion meshes | Original | [R13](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3982852267), [R27](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363944) |
| 7 | [150c7d7b2](https://github.com/icosa-foundation/open-brush/commit/150c7d7b240f1eb4dd45cdd3eae96b6f6e93ece8) | Unregister malformed SDF guides before destroying rejected instances | Original | [R21](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804681) |
| 8 | [d2a4a8543](https://github.com/icosa-foundation/open-brush/commit/d2a4a854371667952a5eb4ef7671ee18df760114) | Record API model-to-guide conversions on the undo stack | Original | [R12](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980434) |
| 9 | [de0eaf3d9](https://github.com/icosa-foundation/open-brush/commit/de0eaf3d9f989d85f0e96fb5fed33e6c45952372) | Build model guides from selected subtrees and baked skinned geometry | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3968491517), [R14](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3982852278) |
| 10 | [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a) | Query model guide surfaces and selection bounds from actual geometry | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069373), [R5](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3955597741), [R10](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980421), [R11](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980431), [R16](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530514) |
| 11 | [a277aacf7](https://github.com/icosa-foundation/open-brush/commit/a277aacf7212d5b5c326d2a1dfb3beffd10da789) | Replace model-guide placeholders and register actual guide renderers | Original | [R9](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980413), [R18](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530529) |
| 12 | [9f8719191](https://github.com/icosa-foundation/open-brush/commit/9f87191915fae83e7b87795f1b75cad2e0cc799b) | Release model-guide runtime SDF assets and retained source meshes | Original | [R22](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804686) |
| 13 | [a6e2793a1](https://github.com/icosa-foundation/open-brush/commit/a6e2793a122c2b4e7a871a52106fa25349bc77c7) | Persist and clone model guides with self-contained geometry snapshots | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069346), [R23](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4057121719) |
| 14 | [2cb07c5fb](https://github.com/icosa-foundation/open-brush/commit/2cb07c5fbf489d0de5d95da3543ff46f7d0dfbe3) | Track forced guide-snapping ownership across all Lua script categories | Original | [R19](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530541) |
| 15 | [626a86919](https://github.com/icosa-foundation/open-brush/commit/626a869195849df06ab2799ea69e2d35dc7c98ed) | Record Lua SDF primitive mutations as undoable guide edit commands | Original | [R24](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4057121722) |
| 16 | [be9bfa027](https://github.com/icosa-foundation/open-brush/commit/be9bfa0272c47dfad6c21942cb39f405cc5628c4) | Exclude empty SDF surfaces and require successful current conversion previews | Original | [R15](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530502), [R27](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363944) |
| 17 | [9d256b8fc](https://github.com/icosa-foundation/open-brush/commit/9d256b8fc1362f90e3125796f0deb840971c59ff) | Reject model guide conversion when the requested subtree is missing | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3968491517) |
| 18 | [db0ab9428](https://github.com/icosa-foundation/open-brush/commit/db0ab942875716e5045d3309f6b9dc912b5d4be0) | Omit MoonSharp-injected parameters from generated Lua API documentation | Original | [R19](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530541) |
| 19 | [4e7a45a9e](https://github.com/icosa-foundation/open-brush/commit/4e7a45a9e3112e491cf24256320b2a04e390bce7) | Keep HTTP primitive update transforms in guide-local space | Original | [R29](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106955) |
| 20 | [8f324f161](https://github.com/icosa-foundation/open-brush/commit/8f324f16144622a34d460eb6b19f6550487d9deb) | Allow skinned-only model selections to create guides | Original | [R31](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106970) |
| 21 | [d8446dc19](https://github.com/icosa-foundation/open-brush/commit/d8446dc19191f56cad8c36eedb744d5ef5fd764d) | Keep Lua primitive handles bound to stable component identities across edits | Original | [R30](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106961) |
| 22 | [a4e8fd214](https://github.com/icosa-foundation/open-brush/commit/a4e8fd2146fc83b80189a34528064f9d6d486b27) | Release editable-guide runtime SDF assets after guides and undo snapshots expire | Original | [R32](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106979) |
| 23 | [1ac441c85](https://github.com/icosa-foundation/open-brush/commit/1ac441c8547e9b173083bc6e60d0f2152afba237) | Project stationary SDF queries from bounded directional probes | Original | [R33](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342224947) |
| 24 | [84e547f12](https://github.com/icosa-foundation/open-brush/commit/84e547f1206118bd2c127127b8622a19a6f63df0) | Generate runtime SDFs from restored model guide geometry | Original | [R34](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342224955) |
| 25 | [ae89c0d23](https://github.com/icosa-foundation/open-brush/commit/ae89c0d231a1b153a60a27798ff093ea0823fab4) | Fit SDF model guide broad-phase colliders to retained mesh bounds | Intermediate | [R35](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342367798) |
| 26 | [da9a39fd6](https://github.com/icosa-foundation/open-brush/commit/da9a39fd62003c6d325f684e3202a9d886cf2a43) | Preserve edited SDF component definitions when cloning primitive guides | Intermediate | [R36](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342367800) |
| 27 | [0ce9fa017](https://github.com/icosa-foundation/open-brush/commit/0ce9fa017f022669006e09b6496c3e444f0de22c) | Release restored SDF mesh assets when guide validation fails | Audit | [R38](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342769319) |
| 28 | [f1f4cd8da](https://github.com/icosa-foundation/open-brush/commit/f1f4cd8da16ba10f4771d97e407fcdfae3fab3ce) | Exclude guides without surface query backends from snapping | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-sdf-guides/BRANCH-REVIEW.md>) |
| 29 | [1c88ce592](https://github.com/icosa-foundation/open-brush/commit/1c88ce59236b298d688273c8a641c647d82883d0) | Revive retained Lua primitive wrappers when deletion is undone | Latest | [R41](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997712) |
| 30 | [43db2f490](https://github.com/icosa-foundation/open-brush/commit/43db2f4907fb661198a6bd77d6a63f9f7f617fc3) | Use model fallback mesh for grab and snapping broad phases | Latest | [R39](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997696) |
| 31 | [e22ba8ea0](https://github.com/icosa-foundation/open-brush/commit/e22ba8ea0b24b87c409b3283e54734a9674d93d9) | Release model source when asynchronous preview generation completes | Latest | [R40](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997705) |
| 32 | [16f902779](https://github.com/icosa-foundation/open-brush/commit/16f902779775c8c0587920e9f41a85263dfe9bdc) | Resize only the original standalone guide primitive operand | Latest | [R37](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342769306) |
| 33 | [759a15f3c](https://github.com/icosa-foundation/open-brush/commit/759a15f3c4523072e78d6c5493dc7a3e728483f5) | Release replaced Lua script snapping ownership on reload | Update | [R45](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564300) |
| 34 | [435ef17f7](https://github.com/icosa-foundation/open-brush/commit/435ef17f7a6b961b850e7f5f0c29f09a01fc26ab) | Preserve the stencil layer on model fallback colliders | Update | [R43](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564274) |
| 35 | [8371f9127](https://github.com/icosa-foundation/open-brush/commit/8371f9127f662d9e5af2cf1d5848fe14fe84d3f0) | Restore source guide renderer bindings after preview failure | Update | [R44](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564288) |
| 36 | [d642a33da](https://github.com/icosa-foundation/open-brush/commit/d642a33da85482c483c45dcd239e4db161afb859) | Project custom guide queries onto retained mesh triangles | Update | [R42](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564261) |
| 37 | [39f849230](https://github.com/icosa-foundation/open-brush/commit/39f849230f65b9c3f73714ba4f6e56f3d984fd68) | Preserve guide layer coordinates through save and reload | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-sdf-guides/BRANCH-REVIEW.md>) |
| 38 | [58f0b9ffb](https://github.com/icosa-foundation/open-brush/commit/58f0b9ffb50907293e9462aab3a9df487e65d77e) | Detach inherited baked meshes from new SDF guides | Update | [R46](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4358329129) |
| 39 | [2e1d1d6f7](https://github.com/icosa-foundation/open-brush/commit/2e1d1d6f774466bea166df5333739d9a58e952f3) | Save guides with compacted live-layer indices | Update | [R47](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4361400109) |
| 40 | [37c3e3a21](https://github.com/icosa-foundation/open-brush/commit/37c3e3a21d8c0fa9e8ec90c8ea4862ceaa464bd2) | Rebuild SDF component handles when ordered dimension layout changes | Update | [R48](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4364800059) |
| 41 | [680b04532](https://github.com/icosa-foundation/open-brush/commit/680b04532ba8d647cd9ad8af0f3671a70bd62818) | Refresh the open SDF editor after external component changes | Update | [R49](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4364800064) |

### Review comment text

#### R1 — Assets/Scripts/Widgets/ModelStencil.cs:102

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069346) · Author: chatgpt-codex-connector · Created: 2026-09-02T12:30:01Z · Resolved

> **[P1]  Persist model-backed guides before saving**
> 
> Model guides are tagged as `Custom` but do not override `PopulateSaveState` or otherwise serialize the source model/SDF. Consequently, saving and reopening a sketch routes this state through `FromGuideIndex` as an ordinary `CustomStencil`, replacing the model guide with the default custom stencil and losing the user's guide geometry.

Mapped commits: [a6e2793a1](https://github.com/icosa-foundation/open-brush/commit/a6e2793a122c2b4e7a871a52106fa25349bc77c7).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342070557) explicitly references the mapped commit.

#### R2 — Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:226

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069366) · Author: chatgpt-codex-connector · Created: 2026-09-02T12:30:01Z · Resolved

> **[P2]  Compute plane distance from the plane surface**
> 
> For every plane guide, this treats `Extents` as the dimensions of a solid box, even though `PlaneStencil.FindClosestPointOnSurface` defines a finite planar surface at its layering offset. The new signed-distance APIs therefore report points up to roughly half an extent in front of or behind the plane as being inside a volume, and return distances to a fictitious box rather than to the guide surface.

Mapped commits: [480fffaea](https://github.com/icosa-foundation/open-brush/commit/480fffaeaf4f1ca30dccbcedc9a49aa2279b8195).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342086026) explicitly references the mapped commit.

#### R3 — Assets/Scripts/Widgets/ModelStencil.cs:727

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069373) · Author: chatgpt-codex-connector · Created: 2026-09-02T12:30:01Z · Resolved · Outdated code location

> **[P2]  Project interior fallback queries onto the mesh surface**
> 
> When runtime SDF generation is unavailable and a model uses the MeshCollider fallback, `Collider.ClosestPoint` returns the query point itself for points inside the collider. This branch then treats that interior point as the surface (`dist == 0`), so painting/snapping and the closest-point API fail to project interior controller positions onto the model boundary and derive an unrelated center-based normal.

Mapped commits: [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342087348) explicitly references the mapped commit.

#### R4 — Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:153

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3952875893) · Author: chatgpt-codex-connector · Created: 2026-09-07T22:19:14Z · Resolved

> **[P2]  Invalidate the guide cache when hidden guides are restored**
> 
> The cache is rebuilt only after registration/removal or when a cached guide is observed inactive. If a global surface query rebuilds the cache while a guide is hidden by undo/delete, the guide is omitted and `sm_dirty` becomes false; undoing that action calls `Show(true)` without registering or invalidating it, so subsequent `guides.signeddistance` and `guides.nextsurfacepoint` calls permanently ignore the restored guide until another guide is added or removed.

Mapped commits: [0d8356ab7](https://github.com/icosa-foundation/open-brush/commit/0d8356ab77fdc3d52131b02f43da2850989445df).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342090913) explicitly references the mapped commit.

#### R5 — Assets/Scripts/Widgets/ModelStencil.cs:798

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3955597741) · Author: chatgpt-codex-connector · Created: 2026-09-08T07:55:32Z · Resolved · Outdated code location

> **[P1]  Use the generated SDF when scoring model guides**
> 
> When runtime SDF generation succeeds, `GenerateSDFPreviewMesh` destroys `m_ModelInstance` and no `m_MeshCollider` is created, so this branch falls through and returns `-1` for every controller position. Consequently the normal compute-shader path can render a model guide and answer direct surface queries, but it can never become the active stencil for grabbing or painting; score against `m_SDFMeshAsset` or the generated preview instead.

Mapped commits: [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342094998) explicitly references the mapped commit.

#### R6 — Assets/Scripts/API/ApiMethods.cs:1957

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3959800195) · Author: chatgpt-codex-connector · Created: 2026-09-08T15:55:50Z · Resolved · Outdated code location

> **[P2]  Convert HTTP guide queries through the scene pose**
> 
> When the scene has been translated, rotated, or scaled, these HTTP endpoints treat API-space points as tracking-world coordinates and return world-space positions and distances. The equivalent Lua methods explicitly transform points through `ActiveCanvas.Pose`, transform results back, and divide distances by the canvas scale; without those conversions, `guide.closestpoint`, both signed-distance endpoints, and `guides.nextsurfacepoint` query the wrong location and return values in the wrong coordinate system.

Mapped commits: [ab1a2223a](https://github.com/icosa-foundation/open-brush/commit/ab1a2223ad012d9ea7f6aded22eb506eef1ebdc1).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342101314) explicitly references the mapped commit.

#### R7 — Assets/Scripts/API/ApiMethods.SdfGuides.cs:56

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3959800203) · Author: chatgpt-codex-connector · Created: 2026-09-08T15:55:50Z · Resolved

> **[P2]  Transform explicit SDF guide poses into world space**
> 
> When `guide.sdf.create` includes a `transform` and `App.Scene.Pose` is non-identity, the parsed API/canvas-space transform is passed directly to `CreateWidgetCommand`, whose `spawnXf` is world-space. The omitted-transform branch correctly uses `_CurrentBrushTransform()`, which applies `App.Scene.Pose`, so explicitly specifying the current brush pose unexpectedly creates the guide elsewhere or at the wrong scale; apply the scene pose to parsed root transforms too.

Mapped commits: [ab1a2223a](https://github.com/icosa-foundation/open-brush/commit/ab1a2223ad012d9ea7f6aded22eb506eef1ebdc1).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342102268) explicitly references the mapped commit.

#### R8 — Assets/Scripts/Widgets/ModelWidget.cs:1153

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3968491517) · Author: chatgpt-codex-connector · Created: 2026-09-09T12:47:30Z · Resolved · Outdated code location

> **[P2]  Preserve model subtrees when converting to guides**
> 
> When the selected model widget represents one part of a broken-apart model (`Subtree` is non-empty), passing only `m_Model` recreates the complete imported hierarchy in `ModelStencil.CreateFromModel` rather than the subtree displayed by this widget. Thus `model.toguide` on a chair, wheel, or other split component unexpectedly produces a guide for the entire source model; propagate and apply `Subtree` (or generate the guide from the widget's filtered mesh hierarchy).

Mapped commits: [de0eaf3d9](https://github.com/icosa-foundation/open-brush/commit/de0eaf3d9f989d85f0e96fb5fed33e6c45952372), [9d256b8fc](https://github.com/icosa-foundation/open-brush/commit/9d256b8fc1362f90e3125796f0deb840971c59ff).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342103991) explicitly references the mapped commits.

#### R9 — Assets/Scripts/Widgets/ModelStencil.cs:335

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980413) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:12:28Z · Resolved

> **[P2]  Register the generated renderer for guide visibility**
> 
> When runtime SDF generation succeeds, this component creates its own generated `MeshRenderer`, but that renderer is never added to `m_TintableMeshes`. `WidgetManager.StencilsDisabled` hides guides only through `StencilWidget.RefreshVisibility`, which iterates that array, so the generated model surface remains visible when the user disables guides and also misses active/inactive stencil tinting.

Mapped commits: [a277aacf7](https://github.com/icosa-foundation/open-brush/commit/a277aacf7212d5b5c326d2a1dfb3beffd10da789).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342104954) explicitly references the mapped commit.

#### R10 — Assets/Scripts/Widgets/ModelStencil.cs:765

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980421) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:12:28Z · Resolved · Outdated code location

> **[P2]  Orient fallback normals away from exterior queries**
> 
> For an exterior query using the MeshCollider fallback, `toSurface` points from the query toward the surface, so this condition deliberately aligns the computed normal with an inward-pointing vector; the fallback on line 771 does the same. Besides returning a reversed orientation from `guide.closestpoint`, `DistanceViaSurfaceQuery` dots that normal with the opposite surface-to-query vector and therefore reports exterior points as having a negative signed distance.

Mapped commits: [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342105904) explicitly references the mapped commit.

#### R11 — Assets/Scripts/Widgets/ModelStencil.cs:725

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980431) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:12:28Z · Resolved

> **[P2]  Convert world bounds directly into selection-canvas space**
> 
> When the model guide is translated, rotated, or scaled, `m_MeshCollider.bounds` and renderer bounds are already world-space, but this composes the canvas inverse with `boundsTransform` and then applies that transform to the world-space center and corners. That applies the widget transform a second time, placing the reported selection bounds elsewhere and causing selection/group-bound operations to miss or incorrectly encompass model guides.

Mapped commits: [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106721) explicitly references the mapped commit.

#### R12 — Assets/Scripts/Widgets/ModelStencil.cs:833

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980434) · Author: chatgpt-codex-connector · Created: 2026-09-10T14:12:28Z · Resolved

> **[P2]  Record model-guide creation on the undo stack**
> 
> Both HTTP and Lua model-to-guide conversion reach this direct `Instantiate` path rather than creating the widget through `CreateWidgetCommand` and `PerformAndRecordCommand`. Consequently, after converting a model, Undo skips the newly created guide and instead reverts the preceding user operation, unlike every other guide-creation path.

Mapped commits: [d2a4a8543](https://github.com/icosa-foundation/open-brush/commit/d2a4a854371667952a5eb4ef7671ee18df760114).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342108246) explicitly references the mapped commit.

#### R13 — Assets/Scripts/Widgets/SdfStencil.cs:813

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3982852267) · Author: chatgpt-codex-connector · Created: 2026-09-10T19:43:33Z · Resolved

> **[P2]  Clear the generated mesh when the SDF becomes empty**
> 
> When `ClearPrimitives()` is called, or the final primitive is deleted, this branch marks the visual revision complete without asking the mesh generator to update or clearing its existing `MeshFilter`. Since `AutoUpdate` was explicitly disabled, a mesh generated for the previous primitive set remains visible indefinitely even though surface queries now see an empty SDF; clear or disable the generated mesh here.

Mapped commits: [7782c8531](https://github.com/icosa-foundation/open-brush/commit/7782c85316a4af44b1042270150d98171c522ced).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342109561) explicitly references the mapped commit.

#### R14 — Assets/Scripts/Widgets/ModelStencil.cs:218

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3982852278) · Author: chatgpt-codex-connector · Created: 2026-09-10T19:43:33Z · Resolved · Outdated code location

> **[P2]  Include skinned meshes when constructing model guides**
> 
> For rigged models whose geometry is represented by `SkinnedMeshRenderer`—a supported model representation tracked by `ObjModelScript`—this returns no source meshes because it searches only for `MeshFilter`. `CountTotalTriangles` and the collider fallback make the same omission, so skinned-only models produce a visible guide with neither an SDF nor a collider, while mixed models silently omit their skinned portions; bake and combine the skinned renderers as well.

Mapped commits: [de0eaf3d9](https://github.com/icosa-foundation/open-brush/commit/de0eaf3d9f989d85f0e96fb5fed33e6c45952372).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342110598) explicitly references the mapped commit.

#### R15 — Assets/Scripts/Widgets/SdfStencil.cs:637

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530502) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:03Z · Resolved · Outdated code location

> **[P2]  Ignore empty SDF guides in surface queries**
> 
> When `NewCustomSDF` or `ClearPrimitives` leaves this manager empty, there is no surface to project onto, but the IsoMesh query yields the input point with no usable normal. `DistanceViaSurfaceQuery` consequently treats that result as distance zero, so an empty guide can dominate `guides.signeddistance` everywhere and interfere with `NextPointOnSurface`. Check `m_SdfManager.IsEmpty` and exclude the guide from distance and projection queries.

Mapped commits: [be9bfa027](https://github.com/icosa-foundation/open-brush/commit/be9bfa0272c47dfad6c21942cb39f405cc5628c4).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342111634) explicitly references the mapped commit.

#### R16 — Assets/Scripts/Widgets/ModelStencil.cs:699

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530514) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:03Z · Resolved · Outdated code location

> **[P2]  Handle queries outside the sampled SDF volume**
> 
> For model guides using the generated SDF, `Sample` clamps the lookup to the finite SDF bounds, but the returned distance is then applied to the original, unclamped `localPos`. For a query farther outside the volume than the configured padding, the sampled gradient can also collapse to zero, causing the fallback up-normal to project to an arbitrary point still far from the model. Clamp explicitly and account for the query-to-volume displacement, or use a surface-query method that remains valid outside the sampled bounds.

Mapped commits: [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342113299) explicitly references the mapped commit.

#### R17 — Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:196

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530520) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:03Z · Resolved · Outdated code location

> **[P2]  Use the guide's actual parent scale for analytic distances**
> 
> When a guide is selected and the selection is uniformly scaled, its world size changes through `SelectionCanvas.Pose.scale` while `Extents` remains in the guide's canvas space. Using the active canvas scale here therefore leaves sphere, cube, ellipsoid, and capsule distance queries at their pre-selection size; the same mismatch occurs for any guide whose canvas scale differs from the active canvas. Derive the scale from the guide's own canvas/parent transform.

Mapped commits: [480fffaea](https://github.com/icosa-foundation/open-brush/commit/480fffaeaf4f1ca30dccbcedc9a49aa2279b8195).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342114549) explicitly references the mapped commit.

#### R18 — Assets/Prefabs/Stencils/StencilModel.prefab:139

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530529) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:03Z · Resolved

> **[P2]  Disable the placeholder sphere in model guides**
> 
> Every model-guide instance retains this enabled built-in sphere renderer while `LoadModel` also adds the source model, or while `GenerateSDFPreviewMesh` adds the generated model surface. No model-loading path disables or replaces this renderer, so converted guides display an unrelated stencil sphere over the requested model geometry.

Mapped commits: [a277aacf7](https://github.com/icosa-foundation/open-brush/commit/a277aacf7212d5b5c326d2a1dfb3beffd10da789).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342115752) explicitly references the mapped commit.

#### R19 — Assets/Scripts/API/Lua/LuaManager.cs:698

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530541) · Author: chatgpt-codex-connector · Created: 2026-09-11T19:06:03Z · Resolved · Outdated code location

> **[P2]  Reset forced snapping when any owning script ends**
> 
> `Brush:ForceGuideSnappingOff` is registered for every Lua script category, but cleanup occurs only when a pointer script ends. If a tool, symmetry, or background script enables the flag and then terminates, its normal `EndActiveScript`/`EndScript` path leaves guide snapping disabled indefinitely, until a pointer script happens to end or Lua is deinitialized. Restrict the API to pointer scripts or track and clear the state for whichever script enabled it.

Mapped commits: [2cb07c5fb](https://github.com/icosa-foundation/open-brush/commit/2cb07c5fbf489d0de5d95da3543ff46f7d0dfbe3), [db0ab9428](https://github.com/icosa-foundation/open-brush/commit/db0ab942875716e5045d3309f6b9dc912b5d4be0).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342116690) explicitly references the mapped commits.

#### R20 — Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:100

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804669) · Author: chatgpt-codex-connector · Created: 2026-09-14T13:44:33Z · Resolved · Outdated code location

> **[P2]  Project overlapping guides onto the union boundary**
> 
> When the stepped candidate is within overlapping guides, `TryFindClosestActiveStencil` selects one contributor by minimum signed distance and this projects onto only that guide. The resulting point can still be inside another guide, so `guides.nextsurfacepoint` returns an internal surface rather than the documented combined-volume boundary; projection needs to account for all contributing guides.

Mapped commits: [15bd80797](https://github.com/icosa-foundation/open-brush/commit/15bd8079718df3a94c394927412ccf919b2faaf2).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342118924) explicitly references the mapped commit.

#### R21 — Assets/Scripts/Widgets/StencilWidget.cs:308

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804681) · Author: chatgpt-codex-connector · Created: 2026-09-14T13:44:33Z · Resolved

> **[P2]  Unregister rejected SDF instances before destroying them**
> 
> When loading a malformed SDF state after `WidgetManager` is initialized, the instantiated stencil has already registered during `GrabWidget.Awake`; disabling and directly destroying it here never calls `UnregisterGrabWidget`. This leaves a destroyed entry in `m_StencilWidgets`, and the unfiltered snapping loop in `WidgetManager.MagnetizeToStencils` later dereferences that entry while painting.

Mapped commits: [150c7d7b2](https://github.com/icosa-foundation/open-brush/commit/150c7d7b240f1eb4dd45cdd3eae96b6f6e93ece8).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342119995) explicitly references the mapped commit.

#### R22 — Assets/Scripts/Widgets/RuntimeSDFGenerator.cs:258

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804686) · Author: chatgpt-codex-connector · Created: 2026-09-14T13:44:33Z · Resolved

> **[P2]  Release runtime-generated SDF assets with their model guides**
> 
> For every runtime-generated model guide, this stores the newly combined mesh on a newly created `SDFMeshAsset`, including a potentially tens-of-megabytes sample array. `ModelStencil.OnDestroy` destroys only component GameObjects and never destroys this transient ScriptableObject or its source mesh, so repeatedly converting and deleting models accumulates substantial native and managed memory for the remainder of the session.

Mapped commits: [9f8719191](https://github.com/icosa-foundation/open-brush/commit/9f87191915fae83e7b87795f1b75cad2e0cc799b).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342120972) explicitly references the mapped commit.

#### R23 — Assets/Scripts/Widgets/ModelStencil.cs:102

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4057121719) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:01:57Z · Resolved

> **[P1]  Persist model-derived guide geometry**
> 
> Model guides are saved as `Custom` guides, but `ModelStencil` does not override `PopulateSaveState` to preserve either the source model or its generated SDF. Consequently, saving and reopening a sketch groups this instance under `StencilType.Custom` with `state.Sdf == null`, so `StencilWidget.FromGuideIndex` instantiates the ordinary custom-guide prefab and the model-derived guide geometry is lost.

Mapped commits: [a6e2793a1](https://github.com/icosa-foundation/open-brush/commit/a6e2793a122c2b4e7a871a52106fa25349bc77c7).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342122702) explicitly references the mapped commit.

#### R24 — Assets/Scripts/API/Lua/Wrappers/GuideApiWrapper.cs:275

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4057121722) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:01:57Z · Resolved · Outdated code location

> **[P2]  Record Lua SDF mutations as edit commands**
> 
> When a Lua script adds a primitive after the sketch was saved, this directly mutates the stencil without pushing an `EditSdfGuideCommand`; `ClearPrimitives` and the setters/update/delete methods in `SdfPrimitiveApiWrapper` do the same. These edits therefore leave `IsMemoryDirty()` unchanged and make Undo revert an unrelated earlier operation, even though the equivalent HTTP and editor paths now record commands.

Mapped commits: [626a86919](https://github.com/icosa-foundation/open-brush/commit/626a869195849df06ab2799ea69e2d35dc7c98ed).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342125138) explicitly references the mapped commit.

#### R25 — Assets/Scripts/Widgets/SdfGuideConversion.cs:143

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4177451766) · Author: chatgpt-codex-connector · Created: 2026-10-04T12:02:02Z · Resolved · Outdated code location

> **[P2]  Preserve widget scale when expanding an existing SDF**
> 
> When a resized SDF is merged with another guide, the first SDF is expanded component-by-component through `GuidePose`, whose scale is only the canvas scale rather than the guide transform's actual world scale. Because these component geometries remain in guide-local units, a guide scaled to 2× is recreated at 1× in the merged result; use the SDF widget's full transform scale for this expansion while retaining the current extent-based handling for legacy guides.

Mapped commits: [7da4580f1](https://github.com/icosa-foundation/open-brush/commit/7da4580f1d8a76713dcbd4ae159bb0ac517d5277).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342126233) explicitly references the mapped commit.

#### R26 — Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:158

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363934) · Author: chatgpt-codex-connector · Created: 2026-10-06T19:04:39Z · Resolved

> **[P2]  Invalidate the cache when a guide becomes visible again**
> 
> When a guide is hidden, this branch eventually rebuilds the cache without it, but showing or restoring that still-registered guide does not call `InvalidateCache`. After deleting and undoing a guide, or undoing and redoing its creation, global `guides.signeddistance`/surface queries can therefore ignore the visible guide indefinitely until an unrelated guide is registered or unregistered; invalidate on guide show/hide transitions as well.

Mapped commits: [0d8356ab7](https://github.com/icosa-foundation/open-brush/commit/0d8356ab77fdc3d52131b02f43da2850989445df).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342128281) explicitly references the mapped commit.

#### R27 — Assets/Scripts/Widgets/SdfStencil.cs:400

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363944) · Author: chatgpt-codex-connector · Created: 2026-10-06T19:04:39Z · Resolved · Outdated code location

> **[P2]  Wait for the current SDF preview before baking it**
> 
> When converting multiple SDF guides, every SDF after the first is baked from the mesh returned here. A recent component edit increments `m_RequestedVisualRevision` while leaving the previous `sharedMesh` in place during the delayed/asynchronous regeneration, so this check accepts stale geometry and the merged guide silently loses that edit. Require the completed/finalized revision to match the requested revision, or defer conversion until generation finishes.

Mapped commits: [7782c8531](https://github.com/icosa-foundation/open-brush/commit/7782c85316a4af44b1042270150d98171c522ced), [be9bfa027](https://github.com/icosa-foundation/open-brush/commit/be9bfa0272c47dfad6c21942cb39f405cc5628c4).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342129392) explicitly references the mapped commits.

#### R28 — Assets/Scripts/Commands/CreateModelGuideCommand.cs:52

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363957) · Author: chatgpt-codex-connector · Created: 2026-10-06T19:04:39Z · Resolved

> **[P2]  Preserve the selected model's world scale**
> 
> When the model's canvas or `SelectionCanvas` has a scale other than one—for example after scaling the current selection—`GetSignedWidgetSize()` is only the widget-local size, while `CreateWidgetCommand` interprets this pose's scale as world space. The resulting model guide is therefore created at the wrong size on `targetCanvas`; derive the scale from the model's full world transform (or explicitly convert between the source and target canvas scales).

Mapped commits: [7da4580f1](https://github.com/icosa-foundation/open-brush/commit/7da4580f1d8a76713dcbd4ae159bb0ac517d5277).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342130271) explicitly references the mapped commit.

#### R29 — Assets/Scripts/API/ApiMethods.SdfGuides.cs:114

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106955) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:21:35Z · Resolved · Outdated code location

> **[P2]  Keep updated primitive transforms guide-local**
> 
> When `guide.sdf.updateprimitive` supplies `transform` while the scene pose is translated, rotated, or scaled, this converts the component-local transform through `App.Scene.Pose` before storing it as local to the guide. This is inconsistent with create/add and `guide.sdf.get`, which use guide-local component transforms, and moves or scales the primitive incorrectly inside its guide.

Mapped commits: [4e7a45a9e](https://github.com/icosa-foundation/open-brush/commit/4e7a45a9e3112e491cf24256320b2a04e390bce7).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342194999) explicitly references the mapped commit.

#### R30 — Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs:126

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106961) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:21:35Z · Resolved · Outdated code location

> **[P2]  Preserve sibling primitive wrappers across edits**
> 
> When Lua retains wrappers for multiple primitives, editing one invokes `ReplaceComponents`, which destroys and recreates every component, but only the wrapper performing the edit is rebound here. Every sibling wrapper then references a removed Unity object, so a later read or edit through it throws that the primitive was deleted or is no longer part of the guide; wrappers need stable component identity or slot-based rebinding.

Mapped commits: [d8446dc19](https://github.com/icosa-foundation/open-brush/commit/d8446dc19191f56cad8c36eedb744d5ef5fd764d).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342195894) explicitly references the mapped commit.

#### R31 — Assets/Scripts/SelectionManager.cs:150

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106970) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:21:35Z · Resolved · Outdated code location

> **[P2]  Enable selected conversion for skinned-only models**
> 
> For a selected rigged model whose geometry consists only of `SkinnedMeshRenderer`s, this predicate returns false and disables the model-to-guide action because it examines only `MeshFilter`s. The generation path now explicitly accepts and bakes skinned renderers, and `CreateModelGuideCommand` also validates them, so this remaining UI gate prevents an otherwise supported conversion.

Mapped commits: [8f324f161](https://github.com/icosa-foundation/open-brush/commit/8f324f16144622a34d460eb6b19f6550487d9deb).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342196803) explicitly references the mapped commit.

#### R32 — Assets/Scripts/Widgets/SdfStencil.cs:1206

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106979) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:21:35Z · Resolved

> **[P2]  Release runtime SDF assets owned by editable guides**
> 
> When an editable guide contains a mesh operand, loading its saved state and converting model/custom guides both create in-memory `SDFMeshAsset` instances containing the sampled field, but destroying the `SdfStencil` only unsubscribes from the generator event. Repeatedly loading, converting, and deleting such guides therefore leaves the runtime ScriptableObjects and their potentially large sample arrays allocated; track ownership or references and release the assets when they are no longer used.

Mapped commits: [a4e8fd214](https://github.com/icosa-foundation/open-brush/commit/a4e8fd2146fc83b80189a34528064f9d6d486b27).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342197788) explicitly references the mapped commit.

#### R33 — Assets/Scripts/Widgets/SdfStencil.cs:1237

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342224947) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:35:41Z · Resolved · Outdated code location

> **[P2]  Handle zero-gradient SDF projections**
> 
> When the query is at a stationary point of the field—for example, the exact center of a sphere—`MapGradient` returns zero and this branch reports the original interior point as the surface. Consequently `guide.closestpoint` returns a non-surface point and `DistanceViaSurfaceQuery` sees zero displacement, making `guide.signeddistance` incorrectly return zero instead of a negative distance; use a fallback projection direction or surface search when the gradient vanishes.

Mapped commits: [1ac441c85](https://github.com/icosa-foundation/open-brush/commit/1ac441c8547e9b173083bc6e60d0f2152afba237).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342339589) explicitly references the mapped commit.

#### R34 — Assets/Scripts/Widgets/ModelStencil.cs:230

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342224955) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:35:42Z · Resolved · Outdated code location

> **[P2]  Generate an SDF for reloaded model snapshots**
> 
> When a model guide is restored from `ModelGuideState`, `ApplySaveState` creates `m_ModelInstance` and `m_SourceMesh` but never assigns `m_Model`, so this guard always skips runtime SDF generation. Reloaded snapshots above the collider fallback limit (20K triangles on desktop or 5K on mobile) therefore retain visible geometry but have neither an SDF nor a collider, causing snapping and surface queries to fail; generation should depend on the retained mesh/instance rather than the original imported `Model`.

Mapped commits: [84e547f12](https://github.com/icosa-foundation/open-brush/commit/84e547f1206118bd2c127127b8622a19a6f63df0).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342340369) explicitly references the mapped commit.

#### R35 — Assets/Scripts/Widgets/ModelStencil.cs:205

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342367798) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:53:28Z · Resolved

> **[P2]  Fit the model guide collider to its retained geometry**
> 
> When runtime SDF generation succeeds, this branch skips `SetupMeshCollider`, leaving the prefab's fixed radius-0.5 sphere as `GrabCollider`. `WidgetManager.MagnetizeToStencils` uses that collider's bounds as its broad phase, so models with authored bounds larger than the unit sphere or subtrees offset from the model origin are rejected even when the brush is directly beside their visible surface; create a bounds collider fitted to `m_SourceMesh` for the SDF path as well.

Mapped commits: [ae89c0d23](https://github.com/icosa-foundation/open-brush/commit/ae89c0d231a1b153a60a27798ff093ea0823fab4).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342738434) explicitly references the mapped commit.

#### R36 — Assets/Scripts/Widgets/SdfPrimitiveStencil.cs:136

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342367800) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:53:28Z · Resolved

> **[P2]  Preserve edited components when cloning primitive SDF guides**
> 
> If a standalone cylinder, torus, or other primitive SDF guide has been edited through the common SDF editor, duplicating it calls this override, which copies only its extents onto a fresh one-primitive prefab. Added operands and custom geometry, operations, blends, transforms, or flips are therefore silently lost in the duplicate; clone the complete component definitions as `SdfStencil.Clone` does.

Mapped commits: [da9a39fd6](https://github.com/icosa-foundation/open-brush/commit/da9a39fd62003c6d325f684e3202a9d886cf2a43).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342739166) explicitly references the mapped commit.

#### R37 — Assets/Scripts/Widgets/SdfPrimitiveStencil.cs:105

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342769306) · Author: chatgpt-codex-connector · Created: 2026-10-21T22:49:54Z · Resolved · Outdated code location

> **[P2]  Preserve edited primitive geometry during guide resizing**
> 
> When a standalone cylinder, torus, or other primitive guide has its original operand removed or replaced through the shared SDF editor/API, resizing the guide still rewrites component 0 using the prefab's fixed `m_PrimitiveType`. For example, replacing a torus's first component with a sphere and then resizing stores torus major/minor-radius data on that sphere, silently changing its intended size; resize logic should only regenerate geometry while the first component still represents the standalone guide's original primitive.

Mapped commits: [16f902779](https://github.com/icosa-foundation/open-brush/commit/16f902779775c8c0587920e9f41a85263dfe9bdc).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346515009) explicitly references the mapped commit.

#### R38 — Assets/Scripts/Widgets/SdfStencil.cs:1045

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342769319) · Author: chatgpt-codex-connector · Created: 2026-10-21T22:49:54Z · Resolved · Outdated code location

> **[P2]  Release SDF assets when saved mesh validation fails**
> 
> When loading a malformed mesh component whose sample payload is valid but whose operation is invalid, `CreateSDFAsset` allocates the runtime `ScriptableObject` before `ParseOperation` throws. The asset is never added to `m_OwnedMeshAssets`, so destroying the rejected stencil cannot release it; a 256³ field can leak tens of megabytes each time such a sketch is opened. Validate first or destroy all assets created while assembling the definitions if any later validation fails.

Mapped commits: [0ce9fa017](https://github.com/icosa-foundation/open-brush/commit/0ce9fa017f022669006e09b6496c3e444f0de22c).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346516168) explicitly references the mapped commit.

#### R39 — Assets/Scripts/Widgets/ModelStencil.cs:544

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997696) · Author: chatgpt-codex-connector · Created: 2026-10-21T23:22:50Z · Resolved

> **[P2]  Replace the placeholder collider in the mesh fallback**
> 
> When SDF generation is unavailable or fails for a model below the triangle threshold, this creates a mesh collider but leaves the prefab sphere enabled and never assigns `m_GrabCollider`. Although the successful-SDF path now installs a fitted bounds collider, this fallback still causes `WidgetManager.MagnetizeToStencils` to obtain the original sphere through `GrabCollider`, excluding model geometry outside that sphere from snapping and selection broad phases. Disable the placeholder and assign the fallback collider to `m_GrabCollider`.

Mapped commits: [43db2f490](https://github.com/icosa-foundation/open-brush/commit/43db2f4907fb661198a6bd77d6a63f9f7f617fc3).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346517276) explicitly references the mapped commit.

#### R40 — Assets/Scripts/Widgets/ModelStencil.cs:469

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997705) · Author: chatgpt-codex-connector · Created: 2026-10-21T23:22:50Z · Resolved · Outdated code location

> **[P2]  Keep checking until preview generation actually completes**
> 
> If asynchronous preview extraction takes more than 36 frames, this final check only logs and the coroutine exits permanently. The generator may subsequently publish its mesh, but `ReleaseSourceModel()` is never called, leaving the generated preview rendered over the original model and leaving the original renderers outside the guide's current tint/visibility lists. Use the generator's completion event or continue waiting while generation is pending.

Mapped commits: [e22ba8ea0](https://github.com/icosa-foundation/open-brush/commit/e22ba8ea0b24b87c409b3283e54734a9674d93d9).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346518245) explicitly references the mapped commit.

#### R41 — Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs:114

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997712) · Author: chatgpt-codex-connector · Created: 2026-10-21T23:22:51Z · Resolved · Outdated code location

> **[P2]  Allow primitive wrappers to recover after undo**
> 
> When a persistent Lua script retains a primitive wrapper, calls `Delete()`, and the user then undoes that command, `EditSdfGuideCommand` restores the component with its stable identity but this one-way flag remains true. Every subsequent access through the wrapper therefore throws that the primitive was deleted even though it is present again; rely on the identity lookup to determine whether the component currently exists so undo can revive the wrapper and redo can invalidate it again.

Mapped commits: [1c88ce592](https://github.com/icosa-foundation/open-brush/commit/1c88ce59236b298d688273c8a641c647d82883d0).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346519099) explicitly references the mapped commit.


#### R42 — Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:237

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564261) · Author: chatgpt-codex-connector · Created: 2026-10-22T09:15:28Z · Resolved

> **[P2] Project interior custom-guide queries to the boundary**
>
> When the queried guide is a legacy `CustomStencil` and the point is inside its convex `MeshCollider`, `CustomStencil.FindClosestPointOnSurface` receives the query point itself from `Collider.ClosestPoint` and produces a zero normal. Routing that result through this fallback therefore makes `guide.signeddistance` return zero and `guide.closestpoint` return an interior point, contrary to the new APIs' documented negative-distance/surface semantics. Add a custom-mesh projection path that searches its retained triangles, as the model-guide fallback already does.

Mapped commits: [d642a33da](https://github.com/icosa-foundation/open-brush/commit/d642a33da85482c483c45dcd239e4db161afb859).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4358270478) cites [d642a33da](https://github.com/icosa-foundation/open-brush/commit/d642a33da85482c483c45dcd239e4db161afb859).

#### R43 — Assets/Scripts/Widgets/ModelStencil.cs:554

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564274) · Author: chatgpt-codex-connector · Created: 2026-10-22T09:15:28Z · Resolved

> **[P2] Put the fallback collider on the stencil layer**
>
> When SDF generation is unavailable or fails for a model below the triangle threshold, this new child starts on Unity's Default layer and is never moved to `StencilLayerIndex`. `SelectionTool` raycasts only `StencilLayerMask`, so after the newly created guide is deselected it cannot be selected again through this collider. The prior follow-up assigns `m_GrabCollider`, but the fresh evidence is that the replacement object still does not copy the placeholder collider's layer; set its layer when creating it, as `SetupBoundsCollider` does.

Mapped commits: [435ef17f7](https://github.com/icosa-foundation/open-brush/commit/435ef17f7a6b961b850e7f5f0c29f09a01fc26ab).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4358271473) cites [435ef17f7](https://github.com/icosa-foundation/open-brush/commit/435ef17f7a6b961b850e7f5f0c29f09a01fc26ab).

#### R44 — Assets/Scripts/Widgets/ModelStencil.cs:452

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564288) · Author: chatgpt-codex-connector · Created: 2026-10-22T09:15:28Z · Resolved

> **[P2] Restore source renderers when preview generation fails**
>
> If asynchronous extraction reports failure or produces an empty mesh, this handler leaves the source model alive but does not restore it as the guide's active renderer set. `GenerateSDFPreviewMesh` has already replaced `m_TintableMeshes` and `m_HighlightMeshFilters` with the empty generated preview, so the visible source model no longer responds to guide visibility toggles, tinting, or highlighting (and can remain invisible if guides were disabled when generation began). Rebind the source renderers on the failure path.

Mapped commits: [8371f9127](https://github.com/icosa-foundation/open-brush/commit/8371f9127f662d9e5af2cf1d5848fe14fe84d3f0).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4358272386) cites [8371f9127](https://github.com/icosa-foundation/open-brush/commit/8371f9127f662d9e5af2cf1d5848fe14fe84d3f0).

#### R45 — Assets/Scripts/API/Lua/LuaManager.cs:951

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4346564300) · Author: chatgpt-codex-connector · Created: 2026-10-22T09:15:28Z · Resolved

> **[P2] Release snapping ownership before hot-reloading a script**
>
> When an active Lua script has called `ForceGuideSnappingOff(true)` and its file is hot-reloaded, `LoadScriptFromPath` replaces the `Script` object in the category dictionaries and initializes the replacement without calling `EndScript` on the old object. This set consequently retains the unreachable old owner; ending the replacement removes only the new object, leaving guide snapping disabled until Lua is deinitialized. End or explicitly remove the previous script's ownership before replacing it during reload.

Mapped commits: [759a15f3c](https://github.com/icosa-foundation/open-brush/commit/759a15f3c4523072e78d6c5493dc7a3e728483f5).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4358273397) cites [759a15f3c](https://github.com/icosa-foundation/open-brush/commit/759a15f3c4523072e78d6c5493dc7a3e728483f5).

#### R46 — Assets/Scenes/Main.unity:32079

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4358329129) · Author: chatgpt-codex-connector · Created: 2026-10-23T09:58:47Z · Resolved

> **[P2] Clear the baked mesh from the SDF template**
>
> When the first asynchronous preview extraction fails—for example because GPU readback is unsupported—`OnMeshGenerationFinished(false)` marks the revision handled and re-enables the renderer without clearing its existing mesh. This override seeds every cloned SDF manager with the baked default sphere/torus mesh, so a cylinder or restored composite guide can permanently display that unrelated geometry even though snapping and distance queries use its actual components. Leave the template mesh unset, or clear it before exposing a newly cloned renderer.

Mapped commits: [58f0b9ffb](https://github.com/icosa-foundation/open-brush/commit/58f0b9ffb50907293e9462aab3a9df487e65d77e).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4364785681) cites [58f0b9ffb](https://github.com/icosa-foundation/open-brush/commit/58f0b9ffb50907293e9462aab3a9df487e65d77e).

#### R47 — Assets/Scripts/Widgets/StencilWidget.cs:78

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4361400109) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:46:22Z · Resolved · Outdated code location

> **[P2] Derive LayerId from the serialized canvas list**
>
> When an earlier layer has been deleted, `GetIndexOfCanvas(savedCanvas)` still uses its position in the raw `m_LayerCanvases` list, while `SceneScript.LayerCanvasesSerialized()` omits deleted layers and compacts the saved indices. For example, a guide on the layer after a deleted layer is saved with `LayerId == 2` even though that layer occupies index 1 in the serialized metadata; reloading then creates an extra layer and places the guide there. Use the same active-canvas mapping as serialization, and fall back from a selected guide whose previous canvas was deleted.

Mapped commits: [2e1d1d6f7](https://github.com/icosa-foundation/open-brush/commit/2e1d1d6f774466bea166df5333739d9a58e952f3).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4364786282) cites [2e1d1d6f7](https://github.com/icosa-foundation/open-brush/commit/2e1d1d6f774466bea166df5333739d9a58e952f3).

#### R48 — Assets/Scripts/GUI/SdfEditorPopup.cs:843

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4364800059) · Author: chatgpt-codex-connector · Created: 2026-10-23T22:38:08Z · Resolved

> **[P2]  Rebuild handles when component layout changes**
> 
> With component handles enabled, moving a sphere operand past a cuboid leaves the total handle count unchanged, so this check reuses the old handles. The subsequent loop assigns them according to the new component order, but each handle retains its original `m_DimensionIndex` and `m_DimensionAxis`: an old transform handle becomes a dimension handle, and vice versa. Dragging these handles then modifies the wrong dimension or moves the entire operand instead of resizing it. Rebuild or fully reconfigure the handles when the ordered component layout changes, not only when its total count changes.

Mapped commits: [37c3e3a21](https://github.com/icosa-foundation/open-brush/commit/37c3e3a21d8c0fa9e8ec90c8ea4862ceaa464bd2).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4365874975) explicitly references the mapped commit.


#### R49 — Assets/Scripts/GUI/SdfEditorPopup.cs:74

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4364800064) · Author: chatgpt-codex-connector · Created: 2026-10-23T22:38:08Z · Resolved

> **[P2]  Refresh the editor after undo and redo**
> 
> While the editor remains open, adding a second operand and then undoing leaves `m_ComponentIndex` at 1 even though only component 0 exists. `EditSdfGuideCommand.OnUndo` replaces the components without changing the selection, and the editor subscribes to neither command events nor component changes, so its selection, buttons, and handles remain stale. Clicking Remove at that point passes the out-of-range index to `RemoveComponent` and throws. Refresh and clamp the editor state after undo/redo and other external component edits, rather than only after its own button actions.

Mapped commits: [680b04532](https://github.com/icosa-foundation/open-brush/commit/680b04532ba8d647cd9ad8af0f3671a70bd62818).

Mapping evidence: [resolution reply](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4365875482) explicitly references the mapped commit.


<a id="branch-sky-export"></a>

## 18. feature/sky-export — PR #1122

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1122) · [Local branch review](<F:/Unity Projects/open-brush-sky-export/BRANCH-REVIEW.md>)

3 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [9baf0a5bf](https://github.com/icosa-foundation/open-brush/commit/9baf0a5bfae373ff57ec327ffd035488a631b120) | Release generated sky gradient textures after export | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-sky-export/BRANCH-REVIEW.md>) |
| 2 | [ac6a8183d](https://github.com/icosa-foundation/open-brush/commit/ac6a8183d383a89d88b647ae6ffe40641aa161c5) | Orient exported sky gradients with the scene gradient | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-sky-export/BRANCH-REVIEW.md>) |
| 3 | [1feb8e27d](https://github.com/icosa-foundation/open-brush/commit/1feb8e27db5d719f41a8acebf49cc193133fb7a8) | Restore temporary sky state when GLB export fails | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-sky-export/BRANCH-REVIEW.md>) |

<a id="branch-straight-edge-snapping"></a>

## 19. feature/straight-edge-snapping — PR #931

[Pull request](https://github.com/icosa-foundation/open-brush/pull/931) · [Local branch review](<F:/Unity Projects/open-brush-straight-edge-snapping/BRANCH-REVIEW.md>)

3 commits; 1 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [9c03ea48b](https://github.com/icosa-foundation/open-brush/commit/9c03ea48b6505405e48aa9bdd18a43f03eb9f361) | Exclude hidden and uncreated strokes from endpoint hash registration | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/931#discussion_r3656716296) |
| 2 | [8afa17020](https://github.com/icosa-foundation/open-brush/commit/8afa17020be374f6188e623f12385e6dba025703) | Exclude hidden canvases from straight-edge endpoint snapping | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-straight-edge-snapping/BRANCH-REVIEW.md>) |
| 3 | [995d4afee](https://github.com/icosa-foundation/open-brush/commit/995d4afee3406ecfcfcfbd289b4897f3ae1f4077) | Dispose the split stroke when snip redo is discarded | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-straight-edge-snapping/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/StraightEdgeGuideScript.cs:581

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/931#discussion_r3656716296) · Author: copilot-pull-request-reviewer · Created: 2026-07-27T11:38:37Z · Resolved

> RebuildAllCanvasHashes() adds every CreatedWithStraightEdge stroke with >=2 control points, even if the stroke is currently hidden or has m_Type == NotCreated. That will repopulate the snap hash with ineligible endpoints after loading/merging, causing snapping to erased/undone strokes.

Mapped commits: [9c03ea48b](https://github.com/icosa-foundation/open-brush/commit/9c03ea48b6505405e48aa9bdd18a43f03eb9f361).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/931#discussion_r4342039977) explicitly references the mapped commit.

<a id="branch-symmetry-stroke-linking"></a>

## 20. feature/symmetry-stroke-linking — PR #1175

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1175) · [Local branch review](<F:/Unity Projects/open-brush-symmetry-stroke-linking/BRANCH-REVIEW.md>)

6 commits; 5 distinct linked review comments.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [2ee1063d5](https://github.com/icosa-foundation/open-brush/commit/2ee1063d5e877092555378c86ab44a86a421006e) | Keep snapshot deselection out of undo history | Latest | [R1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367734) |
| 2 | [19e3d3ed8](https://github.com/icosa-foundation/open-brush/commit/19e3d3ed8b0924ca5763dd5a8bbe362817d5d8ff) | Link real symmetry strokes after straight-edge proxy replacement | Latest | [R2](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367749) |
| 3 | [fd2d30bbd](https://github.com/icosa-foundation/open-brush/commit/fd2d30bbdab8ade2b02430d8eeb7c38f1beedbf6) | Keep the temporary symmetry editing plan local | Latest | [R3](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367757) |
| 4 | [237d6a517](https://github.com/icosa-foundation/open-brush/commit/237d6a517dd22bdb2064c18f93bfa17bfd64091b) | Preserve per-point colors when snipping strokes | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-symmetry-stroke-linking/BRANCH-REVIEW.md>) |
| 5 | [b5cd9d3b4](https://github.com/icosa-foundation/open-brush/commit/b5cd9d3b4a037183cd64c494c8c91d081d62b854) | Restore saved guides through shared widget loading | Update | [R4](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4361416280) |
| 6 | [30870cdbe](https://github.com/icosa-foundation/open-brush/commit/30870cdbee5ad8b425e7a797d898b9e77a512754) | Restore widget selection joins when undoing deselection | Update | [R5](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4361416299) |

### Review comment text

#### R1 — Assets/Scripts/Save/SketchSnapshot.cs:108

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367734) · Author: chatgpt-codex-connector · Created: 2026-10-22T08:53:54Z · Resolved · Outdated code location

> **[P1]  Keep snapshot capture out of the undo stack**
> 
> When saving or exporting while anything is selected, `ClearActiveSelection()` records a deselection command via `PerformAndRecordCommand`, but the snapshot code later restores the selection directly rather than undoing or removing that command. The next user Undo therefore targets this artificial save-time command, whose recorded redo state no longer matches the restored scene, instead of undoing the user's previous edit; a transformed selection can also merge save-time geometry movement into history. Use a non-recording deselection path for snapshot capture.

Mapped commits: [2ee1063d5](https://github.com/icosa-foundation/open-brush/commit/2ee1063d5e877092555378c86ab44a86a421006e).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346505035) explicitly references the mapped commit.

#### R2 — Assets/Scripts/PointerManager.cs:2525

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367749) · Author: chatgpt-codex-connector · Created: 2026-10-22T08:53:54Z · Resolved

> **[P2]  Preserve the symmetry group during straight-edge replacement**
> 
> With linked symmetry and the straight-edge guide enabled, the proxy stroke is finalized with `discard: true`, which reaches this unconditional call and clears `m_ActiveStrokeMirror`; `Transition_RecordingInput_ProcessingStraightEdge` then creates the replacement strokes directly without calling `BeginSymmetryStrokeGroup()`. Consequently the later `FinalizeLine()` cannot attach the actual straight-edge copies to a linked group, so peer edits do not propagate for these strokes.

Mapped commits: [19e3d3ed8](https://github.com/icosa-foundation/open-brush/commit/19e3d3ed8b0924ca5763dd5a8bbe362817d5d8ff).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346505578) explicitly references the mapped commit.

#### R3 — SYMMETRY-PEER-EDITING-PLAN.md:1

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367757) · Author: chatgpt-codex-connector · Created: 2026-10-22T08:53:54Z · Resolved · Outdated code location

> **[P1]  Remove the temporary stabilization plan**
> 
> This file is explicitly a stabilization plan containing deferred work and future implementation decisions, but repository policy requires temporary design, planning, and future-work documents to remain untracked unless the user explicitly requests versioned project documentation. Remove it from the commit rather than shipping it as a repository artifact.
> 
> AGENTS.md reference: [AGENTS.md:L3-L3](https://github.com/icosa-foundation/open-brush/blob/e7852f6ef91027d695aa614f1ed456050df8a02c/AGENTS.md#L3-L3)

Mapped commits: [fd2d30bbd](https://github.com/icosa-foundation/open-brush/commit/fd2d30bbdab8ade2b02430d8eeb7c38f1beedbf6).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346506115) explicitly references the mapped commit.


#### R4 — Assets/Scripts/Save/SaveLoadScript.cs:859

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4361416280) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:48:07Z · Resolved

> **[P1] Restore guide metadata during sketch loading**
>
> When a sketch contains `GuideIndex`, this replacement loading helper never calls `StencilWidget.FromGuideIndex`, even though the previous load path did. Consequently every normal load silently omits all saved stencil/guide widgets, and saving the opened sketch can permanently remove them from the file; restore the guide-loading branch here (and remap its layer/group fields for additive loads).

Mapped commits: [b5cd9d3b4](https://github.com/icosa-foundation/open-brush/commit/b5cd9d3b4a037183cd64c494c8c91d081d62b854).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4364777255) cites [b5cd9d3b4](https://github.com/icosa-foundation/open-brush/commit/b5cd9d3b4a037183cd64c494c8c91d081d62b854).

#### R5 — Assets/Scripts/Commands/SelectCommand.cs:338

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4361416299) · Author: chatgpt-codex-connector · Created: 2026-10-23T15:48:07Z · Resolved · Outdated code location

> **[P2] Restore widget join baselines when undoing deselection**
>
> Move this restoration to the undo path for `m_Deselect`: `GatherWidgetPeers` populates `m_WidgetJoins` only for deselection commands, so this call in the selection-redo branch always receives empty maps. When a moved linked widget is deselected and that action is undone, `SelectWidgets` therefore records the final selection transform as its new join point while the child undo has restored its peer to the pre-move position; the peer preview then applies an identity delta and no longer follows the restored selection correctly.

Mapped commits: [30870cdbe](https://github.com/icosa-foundation/open-brush/commit/30870cdbee5ad8b425e7a797d898b9e77a512754).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4364781353) cites [30870cdbe](https://github.com/icosa-foundation/open-brush/commit/30870cdbee5ad8b425e7a797d898b9e77a512754).

<a id="branch-text-tool"></a>

## 21. feature/text-tool — PR #512

[Pull request](https://github.com/icosa-foundation/open-brush/pull/512) · [Local branch review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>)

12 commits; 6 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [27c9e785e](https://github.com/icosa-foundation/open-brush/commit/27c9e785e3e21172cc1312d29cc217eff6d44516) | Index text widgets using the active text widget count | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558434) |
| 2 | [34c40526b](https://github.com/icosa-foundation/open-brush/commit/34c40526be933421bc3130f5cbe9d3e4402db58b) | Define the serialized text tool enum value | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558454) |
| 3 | [d7c60b676](https://github.com/icosa-foundation/open-brush/commit/d7c60b6760ea5e0f35ec4494d5e13d570f4bcfdd) | Close keyboard sessions on panel hide and ignore key release events | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558358), [R3](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558379) |
| 4 | [8011bddae](https://github.com/icosa-foundation/open-brush/commit/8011bddae0b771660f049ec31631e458973dfbb3) | Remove exporter debug logging and name the output directory argument | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558190) |
| 5 | [464368483](https://github.com/icosa-foundation/open-brush/commit/464368483419b04e20117af8f658a91e1d88acfb) | Remove the duplicated shared localization entry | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558407) |
| 6 | [456980291](https://github.com/icosa-foundation/open-brush/commit/456980291e3c88621593e595cc5a3f4cf847d7d4) | Record text API content and color edits for undo and saving | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |
| 7 | [7f417378d](https://github.com/icosa-foundation/open-brush/commit/7f417378dae02247302e15b15be3b8900a743ed8) | Preserve text content and appearance when duplicating widgets | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |
| 8 | [ca93a3813](https://github.com/icosa-foundation/open-brush/commit/ca93a381308b8d8000c7c592022b5e0d37136592) | Save text widget layer membership | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |
| 9 | [00a4446c4](https://github.com/icosa-foundation/open-brush/commit/00a4446c489d1b20b93f99c58831cfd4433d853d) | Bound text export texture allocations while retaining aspect | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |
| 10 | [e6e76d79e](https://github.com/icosa-foundation/open-brush/commit/e6e76d79e4e7da3cd53f93fe5d93dedc9115257c) | Frame exported text using centered world-space glyph bounds | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |
| 11 | [908a1fe1a](https://github.com/icosa-foundation/open-brush/commit/908a1fe1a3428d9bfe9c03374e36c535c602ebf5) | Preserve text layer coordinates through selection and reload | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |
| 12 | [11f0ce7bb](https://github.com/icosa-foundation/open-brush/commit/11f0ce7bb001ad9300e21bcbefe51c3b31acdf56) | Add text editing cancel that restores the session text | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-text-tool/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/Export/ExportGlTF.cs:86

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558190) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:32:38Z · Resolved · Outdated code location

> `GetExportPayload` call mixes named arguments with a trailing positional argument (`Path.GetDirectoryName(outputFile)`), which won’t compile in C#. Also the `Debug.Log` looks like leftover debug output for a shipping exporter.

Mapped commits: [8011bddae](https://github.com/icosa-foundation/open-brush/commit/8011bddae0b771660f049ec31631e458973dfbb3).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r4342106194) explicitly references the mapped commit.

#### R2 — Assets/Scripts/GUI/KeyboardPanel.cs:86

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558358) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:32:39Z · Resolved · Outdated code location

> `m_OnClose` is invoked only in `OnDestroy()`, but panels are typically dismissed via `PanelWidget.OnHide()` (not destroyed). As a result the close callback may never fire. Also `KeyPressed` should ignore key-up events like `KeyboardPopUpWindow` does, otherwise Enter can be processed twice.

Mapped commits: [d7c60b676](https://github.com/icosa-foundation/open-brush/commit/d7c60b6760ea5e0f35ec4494d5e13d570f4bcfdd).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r4342114467) explicitly references the mapped commit.

#### R3 — Assets/Scripts/Tools/TextTool.cs:88

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558379) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:32:40Z · Resolved · Outdated code location

> `KeyboardPanel.m_InitialText` is never set anywhere before opening the panel, so the keyboard will start with null/empty content and `m_LastInput` will reset to null. That also causes the newly created widget text to be immediately overwritten with null in `UpdateTool()`. Set initial/last input before opening, and null-check the returned panel to avoid an NRE if the panel isn’t available.

Mapped commits: [d7c60b676](https://github.com/icosa-foundation/open-brush/commit/d7c60b6760ea5e0f35ec4494d5e13d570f4bcfdd).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r4342116128) explicitly references the mapped commit.

#### R4 — Assets/Settings/Localization/Strings/Strings Shared Data.asset:3324

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558407) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:32:40Z · Resolved · Outdated code location

> This Shared Data asset appears to have merge/corruption: `m_EditorClassIdentifier:` is embedded inside the `m_Entries` list, and several entries are duplicated (e.g. ENVIRONMENT_PANEL_* and KEYBOARD_PANEL_DESCRIPTION). This can break localization table loading in Unity.

Mapped commits: [464368483](https://github.com/icosa-foundation/open-brush/commit/464368483419b04e20117af8f658a91e1d88acfb).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r4342117166) explicitly references the mapped commit.

#### R5 — Assets/Scripts/API/ApiMethods.cs:2022

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558434) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:32:40Z · Resolved

> This new API endpoint relies on `_GetActiveTextWidget(index)`. That helper currently applies negative-index conversion using `ActiveImageWidgets` count (see `ApiMethods.Utils.cs`), so negative indices for text widgets will produce incorrect results or throw.

Mapped commits: [27c9e785e](https://github.com/icosa-foundation/open-brush/commit/27c9e785e3e21172cc1312d29cc217eff6d44516).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r4342118336) explicitly references the mapped commit.

#### R6 — Assets/Scripts/Tools/BaseTool.cs:58

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558454) · Author: copilot-pull-request-reviewer · Created: 2026-06-11T16:32:40Z · Resolved · Outdated code location

> `ToolType` doesn’t define the value used by the new Text tool/button (`5400` in prefabs). Without an enum entry, the inspector shows a raw number and code can’t reference the tool type symbolically. Consider adding an explicit enum member for the Text tool.

Mapped commits: [34c40526b](https://github.com/icosa-foundation/open-brush/commit/34c40526be933421bc3130f5cbe9d3e4402db58b).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r4342119336) explicitly references the mapped commit.

<a id="branch-texture-painting"></a>

## 22. feature/texture-painting — PR #1111

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1111) · [Local branch review](<F:/Unity Projects/open-brush-texture-painting/BRANCH-REVIEW.md>)

5 commits; 0 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [a4871117a](https://github.com/icosa-foundation/open-brush/commit/a4871117aafc93d71afdf00e692ab3d3a630041c) | Give each paint canvas independent per-material state | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-texture-painting/BRANCH-REVIEW.md>) |
| 2 | [aff99623b](https://github.com/icosa-foundation/open-brush/commit/aff99623b4c9bbe8b279fb2cd78fa49d44d56e27) | Initialize texture painting on guide and image mesh children | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-texture-painting/BRANCH-REVIEW.md>) |
| 3 | [fd19ad009](https://github.com/icosa-foundation/open-brush/commit/fd19ad009435b27e31d4855d5ae4704420fd0dbf) | Reject repeated conversion of existing paint canvases | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-texture-painting/BRANCH-REVIEW.md>) |
| 4 | [ad00cafb0](https://github.com/icosa-foundation/open-brush/commit/ad00cafb09120c26d3f63e9015a277cc31f97f1f) | Preserve original widget identity when undoing paint conversion | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-texture-painting/BRANCH-REVIEW.md>) |
| 5 | [96dfed4fa](https://github.com/icosa-foundation/open-brush/commit/96dfed4fa68e95dfe0cda039a581d4a6df18d9bb) | Restore texture paint layers after widget deselection | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-texture-painting/BRANCH-REVIEW.md>) |

<a id="branch-voxel-editing"></a>

## 23. feature/voxel-editing — PR #1024

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1024) · [Local branch review](<F:/Unity Projects/open-brush-voxel-editing/BRANCH-REVIEW.md>)

17 commits; 13 distinct linked review comments.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [3d90f5d4b](https://github.com/icosa-foundation/open-brush/commit/3d90f5d4b3a6d9e0775aa82bbb85901f34af04c1) | Allocate generated VOX shape IDs after all transform IDs | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125979) |
| 2 | [de28d730e](https://github.com/icosa-foundation/open-brush/commit/de28d730ec2859f9b49f18fd647760e3cc6a4872) | Keep voxel editing active when choosing a brush color | Original | [R6](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195699) |
| 3 | [56b1124f0](https://github.com/icosa-foundation/open-brush/commit/56b1124f00605a8d134cda43912a9dc8a6fb22e6) | Drop hidden voxel widgets as editing targets | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195696) |
| 4 | [2de2a71a6](https://github.com/icosa-foundation/open-brush/commit/2de2a71a61554269761a767d04f6dd671c4ca232) | Batch voxel gesture visuals until editing completes | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195698) |
| 5 | [6c1eb560d](https://github.com/icosa-foundation/open-brush/commit/6c1eb560d3d4ab385f5a3c94dfbab70238772a61) | Traverse intervening cells during voxel gestures | Original | [R7](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082991) |
| 6 | [93af2b3e6](https://github.com/icosa-foundation/open-brush/commit/93af2b3e6542617f73c76b6c03c52f3e2193bf59) | Store changed voxel cells and palette state for gesture undo | Original | [R8](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082994) |
| 7 | [27088ee58](https://github.com/icosa-foundation/open-brush/commit/27088ee58b424405cd573cbea9ee005d53c3dbf1) | Track unrecorded Lua VOX edits in the persistent sketch dirty state | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125975) |
| 8 | [5d06f5739](https://github.com/icosa-foundation/open-brush/commit/5d06f5739bff707093a88f0808a59713628eb31c) | Enable automatic visuals for new Lua VOX widgets | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195692) |
| 9 | [f378bf147](https://github.com/icosa-foundation/open-brush/commit/f378bf1473abecd21a55434bb39a5b0b800e8283) | Validate all embedded VOX documents before restoring widget groups | Original | [R9](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082997) |
| 10 | [c39db389a](https://github.com/icosa-foundation/open-brush/commit/c39db389ad5930b48340b6ea51d7e7365030c093) | Include hidden model colliders in imported geometry bounds | Original | [R10](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4335577425) |
| 11 | [a8d0a4f2d](https://github.com/icosa-foundation/open-brush/commit/a8d0a4f2d92b95958f670d2ca7ba62f5a31936eb) | Avoid reviving hidden voxel targets when ending a gesture | Original | [R4](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195696) |
| 12 | [f88a7c28f](https://github.com/icosa-foundation/open-brush/commit/f88a7c28f5a69036a7a2d013b6decd6d2c873ac6) | Keep voxel command refresh state consistent with document wrappers | Original | [R5](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195698) |
| 13 | [135431b09](https://github.com/icosa-foundation/open-brush/commit/135431b09a8ed42e09c713232dc0f388693a1248) | Preserve unrecorded voxel edits made during background saves | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125975) |
| 14 | [8c28668d6](https://github.com/icosa-foundation/open-brush/commit/8c28668d6877a67e6da616281c7c02f8e6cd5f49) | Track scripted VOX transforms without dirtying tool placement | Original | [R11](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342222952) |
| 15 | [5c16844cf](https://github.com/icosa-foundation/open-brush/commit/5c16844cf533a869086d24300586cb381d2ed9de) | Track scripted VOX widget visibility changes in sketch dirty state | Intermediate | [R12](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342353828) |
| 16 | [9f48c36ba](https://github.com/icosa-foundation/open-brush/commit/9f48c36ba56cd6c709690af1bb00ac028773a8d9) | Deactivate voxel editing when leaving Advanced Mode | Latest | [R13](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342771295) |
| 17 | [4fb66393b](https://github.com/icosa-foundation/open-brush/commit/4fb66393bc9467eb45567f161458c89d45e681c7) | Record completed voxel edits when a target is hidden | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-voxel-editing/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:530

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125975) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:04:11Z · Resolved

> **[P1]  Record Lua voxel edits in the save-dirty state**
> 
> When Lua mutates a widget-backed document through `SetVoxel`, `SetPalette`, or `AddModel`, this only calls `SaveLoadScript.SketchChanged()`, which schedules autosave but does not affect the undo stack consulted by `SketchMemoryScript.IsMemoryDirty()`. Consequently, after saving a sketch, further scripted voxel edits leave overwrite-save disabled and allow loading another sketch without an unsaved-changes confirmation. Fresh evidence in this revision is that this mutation path has no VOX dirty flag or saveable command at all; it needs to update persistent dirty state in addition to scheduling autosave.

Mapped commits: [27088ee58](https://github.com/icosa-foundation/open-brush/commit/27088ee58b424405cd573cbea9ee005d53c3dbf1), [135431b09](https://github.com/icosa-foundation/open-brush/commit/135431b09a8ed42e09c713232dc0f388693a1248).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342200016), [resolution reply 2](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342297108) explicitly references the mapped commits.

#### R2 — Assets/Scripts/VoxSerialization/VoxWriter.cs:199

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125979) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:04:11Z · Resolved · Outdated code location

> **[P2]  Keep generated scene-node ID ranges disjoint**
> 
> For a generated document with at least 991 models, transform IDs starting at 10 reach 1000, which is also assigned to the first shape node. `AddModel` does not impose a model-count limit, so a procedural Lua document can reach this case and export a VOX scene graph with duplicate node IDs; readers may then reject the file or associate transforms with the wrong shapes. Derive the shape-node range from the model count or allocate all node IDs monotonically.

Mapped commits: [3d90f5d4b](https://github.com/icosa-foundation/open-brush/commit/3d90f5d4b3a6d9e0775aa82bbb85901f34af04c1).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342201311) explicitly references the mapped commit.

#### R3 — Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:621

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195692) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:38:52Z · Resolved · Outdated code location

> **[P1]  Enable visuals by default for NewWidget**
> 
> `Vox:NewWidget(...):Model():SetVoxel(...)` leaves `WidgetViewState.AutoVisuals` false, so `OnDocumentMutated` never reaches `Refresh`/`TryCreateWidget`; the document remains detached from any widget and is therefore neither visible nor included in sketch saves. This contradicts the method's promise that painting the first voxel creates a normal widget, and the bundled example explicitly disables automatic visuals before batching, implying they should initially be enabled.

Mapped commits: [5d06f5739](https://github.com/icosa-foundation/open-brush/commit/5d06f5739bff707093a88f0808a59713628eb31c).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342203077) explicitly references the mapped commit.

#### R4 — Assets/Scripts/Tools/VoxelTool.cs:406

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195696) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:38:52Z · Resolved

> **[P1]  Drop targets whose widgets have been hidden**
> 
> When the current voxel widget is hidden by undoing its creation or by a delete/toss command, `isValid` remains true because the document still references the hidden widget, and this check retains it as the edit target. Triggering inside its old volume then mutates and re-shows that widget; recording the new edit clears the redo stack, whose disposed creation command destroys the resurrected widget, leaving the new undo command pointing at a destroyed object. Clear the target when its widget is no longer showing or active.

Mapped commits: [56b1124f0](https://github.com/icosa-foundation/open-brush/commit/56b1124f00605a8d134cda43912a9dc8a6fb22e6), [a8d0a4f2d](https://github.com/icosa-foundation/open-brush/commit/a8d0a4f2d92b95958f670d2ca7ba62f5a31936eb).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342200544) explicitly references the mapped commits.

#### R5 — Assets/Scripts/Tools/VoxelTool.cs:380

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195698) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:38:52Z · Resolved

> **[P2]  Defer mesh rebuilds until the voxel gesture ends**
> 
> For a selected widget this enables automatic visuals throughout the gesture, so every cell crossed by `ApplyAt` calls `OnDocumentMutated` and synchronously runs `RefreshEditableVoxMeshes`, rebuilding every model mesh, material assignment, collider bounds, and accounting data. Dragging over a large or spatially spread VOX model therefore performs repeated full-volume greedy meshes in one gesture and can cause severe frame stalls; disable automatic rebuilding during the gesture and refresh once in `EndGesture`.

Mapped commits: [2de2a71a6](https://github.com/icosa-foundation/open-brush/commit/2de2a71a61554269761a767d04f6dd671c4ca232), [f88a7c28f](https://github.com/icosa-foundation/open-brush/commit/f88a7c28f5a69036a7a2d013b6decd6d2c873ac6).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342201576) explicitly references the mapped commits.

#### R6 — Assets/Scripts/Tools/BaseTool.cs:55

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195699) · Author: chatgpt-codex-connector · Created: 2026-09-20T14:38:52Z · Resolved

> **[P2]  Keep the voxel tool active while choosing a color**
> 
> The new tool consumes `App.BrushColor.CurrentColor` for Add and Paint, but `ColorPicker` calls `SketchSurfacePanel.VerifyValidToolWithColorUpdate`, whose preservation allowlist does not include `VoxelTool`; the first color-picker update therefore switches back to the default tool. Add this tool type to that allowlist so users can choose a painting color without immediately leaving voxel mode.

Mapped commits: [de28d730e](https://github.com/icosa-foundation/open-brush/commit/de28d730ec2859f9b49f18fd647760e3cc6a4872).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342204428) explicitly references the mapped commit.

#### R7 — Assets/Scripts/Tools/VoxelTool.cs:255

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082991) · Author: chatgpt-codex-connector · Created: 2026-10-03T01:20:02Z · Resolved

> **[P2]  Interpolate cells between pointer samples**
> 
> When the controller moves by more than one voxel between frames, this edits only the rounded cell at the latest sample; `m_LastCell` merely suppresses duplicates and the intervening cells are never visited. Add, Erase, and Paint gestures therefore leave gaps when users move quickly or select a small voxel size. Rasterize the segment from the previous cell to the current cell and apply the operation to every crossed cell.

Mapped commits: [6c1eb560d](https://github.com/icosa-foundation/open-brush/commit/6c1eb560d3d4ab385f5a3c94dfbab70238772a61).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342203037) explicitly references the mapped commit.

#### R8 — Assets/Scripts/Tools/VoxelTool.cs:206

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082994) · Author: chatgpt-codex-connector · Created: 2026-10-03T01:20:02Z · Resolved · Outdated code location

> **[P2]  Avoid serializing the entire document for every gesture**
> 
> For a dense editable or imported VOX model, pressing the trigger synchronously copies and sorts every voxel through `ToVoxBytes()` on the Unity main thread, and `EndGesture` repeats the work while the undo command retains both full byte arrays. A 128³ model already has over two million entries, so even a one-cell edit can cause a substantial VR frame stall and tens of megabytes of transient and undo memory; record the changed cells/palette state instead of snapshotting the complete document.

Mapped commits: [93af2b3e6](https://github.com/icosa-foundation/open-brush/commit/93af2b3e6542617f73c76b6c03c52f3e2193bf59).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342204680) explicitly references the mapped commit.

#### R9 — Assets/Scripts/Widgets/ModelWidget.cs:1346

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082997) · Author: chatgpt-codex-connector · Created: 2026-10-03T01:20:02Z · Resolved · Outdated code location

> **[P2]  Roll back partially restored embedded widget groups**
> 
> When a later payload in a multi-widget entry is corrupt or otherwise throws during `FromBytes`/`CreateModel`, widgets created by earlier iterations remain registered in the scene, while the catch returns `false` and `CreateModelFromSaveData` records the entire entry as missing. The loaded sketch is consequently left with a partial valid group plus missing-model records for all transforms, which can produce duplicates on subsequent recovery or save. Parse all runtime documents before instantiating widgets, or destroy already-created widgets on failure.

Mapped commits: [f378bf147](https://github.com/icosa-foundation/open-brush/commit/f378bf1473abecd21a55434bb39a5b0b800e8283).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342205396) explicitly references the mapped commit.

#### R10 — Assets/Scripts/VoxImporter.cs:153

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4335577425) · Author: chatgpt-codex-connector · Created: 2026-10-21T10:13:06Z · Resolved

> **[P2]  Include hidden VOX nodes when calculating import bounds**
> 
> When every model in a valid VOX scene is hidden by its node or layer, this deactivates all model objects before `Model.CalcBoundsNonGltf` runs. That method uses `GetComponentsInChildren<BoxCollider>()` without `includeInactive`, finds no geometry, and `EndCreatePrefab` rejects and destroys the entire import as “No usable geometry.” Calculate bounds with inactive children included, or defer applying visibility until after import validation.

Mapped commits: [c39db389a](https://github.com/icosa-foundation/open-brush/commit/c39db389ad5930b48340b6ea51d7e7365030c093).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342206333) explicitly references the mapped commit.

#### R11 — Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:473

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342222952) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:35:27Z · Resolved

> **[P1]  Track scripted transform changes in the dirty state**
> 
> When Lua calls `SetTransform` on an existing widget after a successful save, this path only schedules autosave through `SketchChanged()` and never calls `MarkUnrecordedChange()` or records an undo command. `IsMemoryDirty()` therefore remains false, so overwrite-save can stay disabled and loading another sketch can discard the moved/scaled widget without an unsaved-changes warning.

Mapped commits: [8c28668d6](https://github.com/icosa-foundation/open-brush/commit/8c28668d6877a67e6da616281c7c02f8e6cd5f49).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342317317) explicitly references the mapped commit.

#### R12 — Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:505

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342353828) · Author: chatgpt-codex-connector · Created: 2026-10-21T21:51:41Z · Resolved · Outdated code location

> **[P1]  Preserve hidden widgets or mark Hide dirty**
> 
> When Lua calls `Hide()` on a visible VOX document after a successful save, this only starts the widget's hide transition and neither marks an unrecorded change nor schedules a save. Once hidden, the widget becomes inactive and `MetadataUtils.GetTiltModels()` excludes it via the `gameObject.activeSelf` filter, so a subsequent save omits the entire document while `IsMemoryDirty()` can remain false, allowing the hidden VOX data to be lost without an unsaved-changes warning. Either keep explicitly hidden documents in persistence or track this as a persistent deletion.

Mapped commits: [5c16844cf](https://github.com/icosa-foundation/open-brush/commit/5c16844cf533a869086d24300586cb381d2ed9de).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342737813) explicitly references the mapped commit.

#### R13 — Assets/Scripts/GUI/SketchSurfacePanel.cs:298

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342771295) · Author: chatgpt-codex-connector · Created: 2026-10-21T22:50:10Z · Resolved

> **[P2]  Deactivate voxel editing when leaving advanced mode**
> 
> When Advanced Mode is turned off while this tool is active, `SketchControlsScript` calls `EnsureUserHasBasicToolEnabled()`, but that method's advanced-tool guard does not include `VoxelTool`. Because its only button is on `AdvancedToolsPanel.prefab`, the controls disappear while trigger input continues editing voxels in Basic Mode; add `VoxelTool` to the guard so switching modes returns to the default tool.

Mapped commits: [9f48c36ba](https://github.com/icosa-foundation/open-brush/commit/9f48c36ba56cd6c709690af1bb00ac028773a8d9).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4346492052) explicitly references the mapped commit.

<a id="branch-websocket-client"></a>

## 24. feature/websocket-client — PR #1113

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1113) · [Local branch review](<F:/Unity Projects/open-brush-websocket-client/BRANCH-REVIEW.md>)

7 commits; 3 distinct linked review comments.

Changes without a matching GitHub review are linked to the local branch review; they are not assigned an invented review comment.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [5b2294864](https://github.com/icosa-foundation/open-brush/commit/5b2294864390c6e00cbdc8eb25a68001618862d6) | Disambiguate the Unity update awaiter from NativeWebSocket | Original | [R2](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381365) |
| 2 | [1d044e946](https://github.com/icosa-foundation/open-brush/commit/1d044e9464bb179946b693195b45c0fe597cd63f) | Include listener URL in the outgoing GET size limit | Original | [R3](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381370) |
| 3 | [0183171b6](https://github.com/icosa-foundation/open-brush/commit/0183171b6dc3c40639505febe0bc53fdfd90822f) | Bound WebSocket connection backlog and remove closed listeners | Original | [R1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381341) |
| 4 | [84bb2d099](https://github.com/icosa-foundation/open-brush/commit/84bb2d099fd08e75eddf70fcdc4c6679deb721db) | Drain incoming frames for outgoing WebSocket listeners | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-websocket-client/BRANCH-REVIEW.md>) |
| 5 | [87be94c7f](https://github.com/icosa-foundation/open-brush/commit/87be94c7ffb4d70c39a38c681084161a39fac923) | Stop outgoing WebSocket connections when the API manager ends | Audit | Source audit; [local review](<F:/Unity Projects/open-brush-websocket-client/BRANCH-REVIEW.md>) |
| 6 | [ac3d19d9d](https://github.com/icosa-foundation/open-brush/commit/ac3d19d9dd0cd7b530e86a189040feadc85e91f2) | Observe outgoing WebSocket operation failures | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-websocket-client/BRANCH-REVIEW.md>) |
| 7 | [055018885](https://github.com/icosa-foundation/open-brush/commit/0550188857372eae7616a88fe06cce66abcc917e) | Preserve numeric stroke commands across system cultures | Update | No matching GitHub review; [local branch review](<F:/Unity Projects/open-brush-websocket-client/BRANCH-REVIEW.md>) |

### Review comment text

#### R1 — Assets/Scripts/API/ApiManager.cs:845

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381341) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:51:20Z · Resolved · Outdated code location

> This queues commands for permanently `Closed` sockets as well as sockets that are still connecting. A failed initial connection or later disconnect is never retried or removed anywhere, so every subsequent stroke grows this queue indefinitely and is never delivered. Handle closed sockets with reconnection/removal and bound or clear their pending queue rather than treating every non-open state as transient.

Mapped commits: [0183171b6](https://github.com/icosa-foundation/open-brush/commit/0183171b6dc3c40639505febe0bc53fdfd90822f).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r4341933324) explicitly references the mapped commit.

#### R2 — Assets/ThirdParty/AsyncAwaitUtil/Source/WaitForUpdate.cs:12

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381365) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:51:21Z · Resolved

> Moving this type into `UnityAsyncAwaitUtil` changes the unqualified receiver in `IEnumeratorAwaitExtensions.GetAwaiter(this WaitForUpdate ...)` to resolve to NativeWebSocket's global `WaitForUpdate`, not this type. As a result, every newly qualified `await new UnityAsyncAwaitUtil.WaitForUpdate()` has no applicable `GetAwaiter` and will not compile. Fully qualify that extension receiver (or move the extension into this namespace) as part of this change.

Mapped commits: [5b2294864](https://github.com/icosa-foundation/open-brush/commit/5b2294864390c6e00cbdc8eb25a68001618862d6).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r4341934542) explicitly references the mapped commit.

#### R3 — Assets/Scripts/API/ApiManager.cs:821

[Original review comment](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381370) · Author: copilot-pull-request-reviewer · Created: 2026-09-12T13:51:21Z · Resolved · Outdated code location

> This condition now measures only the command text, whereas the actual GET URL also includes the listener URL and separator. A long listener URL can therefore exceed the intended conservative limit while still taking the GET path; retain the previous behavior by checking the complete `uri`.

Mapped commits: [1d044e946](https://github.com/icosa-foundation/open-brush/commit/1d044e9464bb179946b693195b45c0fe597cd63f).

Mapping evidence: [resolution reply 1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r4341935570) explicitly references the mapped commit.

<a id="branch-maquette-compat"></a>

## 25. open-brush-maquette-compat — PR #1204

[Pull request](https://github.com/icosa-foundation/open-brush/pull/1204)

48 commits; 0 distinct linked review comments.

Newly matched PR worktree. These are PR implementation/documentation commits, not review-fix commits; no matching inline review threads were present at refresh. No local BRANCH-REVIEW.md exists in this worktree.

| # | Commit | Change | Batch | Review comment(s), full text below |
|---|---|---|---|---|
| 1 | [c36244666](https://github.com/icosa-foundation/open-brush/commit/c362446666dfe6c3dd83a06259d4ca8754554259) | Document Maquette and ShapesXR import requirements and geometry schema | PR implementation | No matching GitHub review |
| 2 | [d42490ee7](https://github.com/icosa-foundation/open-brush/commit/d42490ee7dcca2f58b28990f21869fbed8615641) | Read native Maquette projects and resolve scene entity states | PR implementation | No matching GitHub review |
| 3 | [5a06dd2b9](https://github.com/icosa-foundation/open-brush/commit/5a06dd2b9eb7cd1a5b192c8e430744fc50046ea1) | Decode native ShapesXR spaces and resolve snapshot properties | PR implementation | No matching GitHub review |
| 4 | [46324159b](https://github.com/icosa-foundation/open-brush/commit/46324159b73b4beb51fc272d0ccf32854e69500e) | Record native reader validation and remaining import work | PR implementation | No matching GitHub review |
| 5 | [174785ec6](https://github.com/icosa-foundation/open-brush/commit/174785ec651ce0d957bc7d2b4638570472db354c) | Encode shared geometry recipes and translate ShapesXR panel snapshots | PR implementation | No matching GitHub review |
| 6 | [b70ab3067](https://github.com/icosa-foundation/open-brush/commit/b70ab3067166895d78ec1cdf5d0a5e8c81a3a195) | Translate Maquette procedural builders into shared geometry recipes | PR implementation | No matching GitHub review |
| 7 | [effb231b1](https://github.com/icosa-foundation/open-brush/commit/effb231b1ea85c8b92edf9b392f496d0f5773894) | Preserve text content and appearance when duplicating widgets | PR implementation | No matching GitHub review |
| 8 | [823188ecc](https://github.com/icosa-foundation/open-brush/commit/823188ecceb0ebb6d85fc28f84ad26d2ed80ccc2) | Preserve text layer coordinates through selection and reload | PR implementation | No matching GitHub review |
| 9 | [9ad01fe1a](https://github.com/icosa-foundation/open-brush/commit/9ad01fe1ac90d14e49f7e372a7a11bb839755b16) | Port generated model identity and loading from 3D Shapes | PR implementation | No matching GitHub review |
| 10 | [25b288217](https://github.com/icosa-foundation/open-brush/commit/25b2882172bae7b084af21493d29f286c07ad92d) | Keep local import analysis outside Unity disposable Temp | PR implementation | No matching GitHub review |
| 11 | [4d20631f3](https://github.com/icosa-foundation/open-brush/commit/4d20631f3321b1ef9dcd51b0c78ea1c4265939a8) | Port editable model runtime and shared recipe persistence from 3D Shapes | PR implementation | No matching GitHub review |
| 12 | [d01d09ffd](https://github.com/icosa-foundation/open-brush/commit/d01d09ffd104192ea1fe4fde0fd64458def49426) | Resolve bundled and installed Maquette model and image resources | PR implementation | No matching GitHub review |
| 13 | [d50718776](https://github.com/icosa-foundation/open-brush/commit/d50718776292aa2c8ce4b8da6a2e73f7c9bea418) | Match legacy Maquette frame borders and ancestor builder scales | PR implementation | No matching GitHub review |
| 14 | [4c2823b59](https://github.com/icosa-foundation/open-brush/commit/4c2823b5942f18a93444679c9e474f2950caa244) | Normalize legacy Maquette library references and asset loading flags | PR implementation | No matching GitHub review |
| 15 | [869c2a0ea](https://github.com/icosa-foundation/open-brush/commit/869c2a0eae575accd0d6275b156bf8c557bf984c) | Preserve authored text layout and intrinsic transforms in sketches | PR implementation | No matching GitHub review |
| 16 | [e52aea65e](https://github.com/icosa-foundation/open-brush/commit/e52aea65ef8bd3b948f69e5a2357ce07d6e84a4e) | Preserve Maquette hierarchy shear through shared recipe transforms | PR implementation | No matching GitHub review |
| 17 | [137140a86](https://github.com/icosa-foundation/open-brush/commit/137140a86a53143c41ad0042e570bcbc0bf18c4f) | Translate native Maquette text boxes and glyph placement | PR implementation | No matching GitHub review |
| 18 | [9bdb6c857](https://github.com/icosa-foundation/open-brush/commit/9bdb6c85721e77f184173160a239f38c25c79ff7) | Import Maquette procedural and text objects through existing widgets | PR implementation | No matching GitHub review |
| 19 | [95c756d62](https://github.com/icosa-foundation/open-brush/commit/95c756d625bc0231e4051d8665035e373df4bccf) | Load external text fonts from their actual OpenType files | PR implementation | No matching GitHub review |
| 20 | [65b126a3e](https://github.com/icosa-foundation/open-brush/commit/65b126a3e7218717a4636fcfefc710e6de9afe79) | Record native asset text and placement import validation | PR implementation | No matching GitHub review |
| 21 | [9efcb71fd](https://github.com/icosa-foundation/open-brush/commit/9efcb71fd4c25fc47dc44090f1b47ce150ced57a) | Include intrinsic glyph transforms in text distance field scale | PR implementation | No matching GitHub review |
| 22 | [18a20ab58](https://github.com/icosa-foundation/open-brush/commit/18a20ab583bf132aee36404c882c8f624f048a4a) | Stage native archives with relative model and texture dependencies | PR implementation | No matching GitHub review |
| 23 | [e80407737](https://github.com/icosa-foundation/open-brush/commit/e80407737b11f15b8dd16b68f357f6941e7e515a) | Cache glTF resources once and retain shared data across object variants | PR implementation | No matching GitHub review |
| 24 | [833f7f8fe](https://github.com/icosa-foundation/open-brush/commit/833f7f8fe1f1e3c64e8ce68ff4f72b06fa268bba) | Create imported widgets at authored poses without model intro resizing | PR implementation | No matching GitHub review |
| 25 | [109f16cc2](https://github.com/icosa-foundation/open-brush/commit/109f16cc2ba966b2e14892ac945cbc4eae2ddd72) | Import Maquette models asynchronously with shared glTF resources and authored scale | PR implementation | No matching GitHub review |
| 26 | [94025b7b5](https://github.com/icosa-foundation/open-brush/commit/94025b7b50002df359e37949cf9866afd300697e) | Record model cache coverage and remaining runtime validation | PR implementation | No matching GitHub review |
| 27 | [a2a10d778](https://github.com/icosa-foundation/open-brush/commit/a2a10d77885f31b2ffa1285fbee160b04db1a4cd) | Persist intrinsic image geometry tint and blend modes | PR implementation | No matching GitHub review |
| 28 | [4835cf4a3](https://github.com/icosa-foundation/open-brush/commit/4835cf4a3e8f893d9efa689903eded82eb69be31) | Map Maquette image placement and legacy default materials | PR implementation | No matching GitHub review |
| 29 | [8cb12028d](https://github.com/icosa-foundation/open-brush/commit/8cb12028d35707c7a00c229d464c24a59da9d5e1) | Keep media layer coordinates consistent through selection and reload | PR implementation | No matching GitHub review |
| 30 | [1d247ccb8](https://github.com/icosa-foundation/open-brush/commit/1d247ccb8d182dc2e6bbeb55673764746408b7c1) | Retain image tint opacity and additive blending during export | PR implementation | No matching GitHub review |
| 31 | [97ca54755](https://github.com/icosa-foundation/open-brush/commit/97ca5475526b4525794c818620b38e55c108bada) | Import Maquette images through persistent reference image widgets | PR implementation | No matching GitHub review |
| 32 | [5ece00cec](https://github.com/icosa-foundation/open-brush/commit/5ece00cecad231ca87d20ac8288063963ff7c212) | Separate explicit image blend profiles from legacy export materials | PR implementation | No matching GitHub review |
| 33 | [be683f630](https://github.com/icosa-foundation/open-brush/commit/be683f630b50f2f2370097ac294c1ed84f84ee84) | Record image import coverage and remaining appearance gaps | PR implementation | No matching GitHub review |
| 34 | [0c8c1a6e9](https://github.com/icosa-foundation/open-brush/commit/0c8c1a6e94dfaf0ef2b8baedb464791bf083f48c) | Share native hierarchy placement between document importers | PR implementation | No matching GitHub review |
| 35 | [33274a051](https://github.com/icosa-foundation/open-brush/commit/33274a051185642fc318c6addba64dc0d4d592b2) | Resolve ShapesXR initial object state and parent hierarchy | PR implementation | No matching GitHub review |
| 36 | [e1beb5e53](https://github.com/icosa-foundation/open-brush/commit/e1beb5e531e1750743012a2689207b2ab270c719) | Import ShapesXR initial-state panels as editable models | PR implementation | No matching GitHub review |
| 37 | [7edce4769](https://github.com/icosa-foundation/open-brush/commit/7edce4769cf232ce227d08930fb548da5c348570) | Document ShapesXR initial-state panel import coverage | PR implementation | No matching GitHub review |
| 38 | [91699fe92](https://github.com/icosa-foundation/open-brush/commit/91699fe92c9e066a15cc04e961d7926c9b562c56) | Resolve saved text fonts from Unity resource paths | PR implementation | No matching GitHub review |
| 39 | [254521abf](https://github.com/icosa-foundation/open-brush/commit/254521abf2f9b5d9e998a90275553032ceae9c05) | Import ShapesXR text through shared text widgets | PR implementation | No matching GitHub review |
| 40 | [77df225ea](https://github.com/icosa-foundation/open-brush/commit/77df225eacd6399efa9d6f123b221816eed080a8) | Resolve ShapesXR resource manifests and select highest-detail model nodes | PR implementation | No matching GitHub review |
| 41 | [0776f5262](https://github.com/icosa-foundation/open-brush/commit/0776f52625a2fac4830e141a5480d88bed2575b7) | Import referenced ShapesXR models and images into persistent asset caches | PR implementation | No matching GitHub review |
| 42 | [552cf3e47](https://github.com/icosa-foundation/open-brush/commit/552cf3e47a9d93766a8f38c7684e4c5ed85992a0) | Record ShapesXR text and referenced asset import coverage | PR implementation | No matching GitHub review |
| 43 | [79c070a6a](https://github.com/icosa-foundation/open-brush/commit/79c070a6ac0cacb636374ed18e0e004dc45ace08) | Initialize light widgets and preserve saved and cloned light state | PR implementation | No matching GitHub review |
| 44 | [73bae6e4b](https://github.com/icosa-foundation/open-brush/commit/73bae6e4b6fafb5a422619394d9b2c92b91afbb2) | Import Maquette point and spot lights through native light widgets | PR implementation | No matching GitHub review |
| 45 | [82f4eae7a](https://github.com/icosa-foundation/open-brush/commit/82f4eae7a6d4ab291d31510d081514cce1dd32e5) | Resolve ShapesXR material appearance in the selected object state | PR implementation | No matching GitHub review |
| 46 | [674beaf6e](https://github.com/icosa-foundation/open-brush/commit/674beaf6ec565d27bad8ab9478d4fd1030976b7f) | Load legacy ShapesXR initial views with background content | PR implementation | No matching GitHub review |
| 47 | [455c5a069](https://github.com/icosa-foundation/open-brush/commit/455c5a06964f47b9281a96590a314ae3b13155f6) | Record light import and initial-view composition coverage | PR implementation | No matching GitHub review |
| 48 | [1c5517afe](https://github.com/icosa-foundation/open-brush/commit/1c5517afe9a6a81e2abde2e190534812e28f7b6d) | Compose Maquette slides with their master-stage entity states | PR implementation | No matching GitHub review |

## Sources and limitations

1. The previous 182-commit inventory and review quotations are preserved. Later commits were enumerated from each branch's last mapped ancestor to its recorded current HEAD and verified as local Git objects; Maquette's four-commit inventory came from PR #1204.
2. New review mappings and quotations come from live GitHub inline threads fetched during this refresh (2026-10-23T22:44:54.168Z). All 25 PRs were queried successfully. The 14 added review quotations include the latest ten repaired threads and four earlier SDF recheck threads omitted from the old map.
3. Commits without a matching GitHub thread are marked explicitly. Local branch reviews remain untracked. This file does not claim a missing local review exists for Maquette or infer review provenance from a commit subject.
4. This is a traceability record. Compilation, source checks and simulated fixtures do not establish Unity, GPU, device, live-service or importer runtime behavior. See the corresponding branch reviews and replies for the actual scope of checks.
5. The document remains local and untracked. It does not stage files, commit, push, post comments, or resolve threads.

6. Latest append (2026-10-24 03:45:33 UTC): retained every prior commit row and original review block; added 48 commits using explicit resolution-reply evidence for review mappings. Other additions are labeled PR implementation. Maquette history is a read-only snapshot and may advance independently.

7. Latest append (2026-10-24 03:55:31 UTC): retained every prior commit row and original review block; added 3 commits using explicit resolution-reply evidence for review mappings. Other additions, if any, are labeled PR implementation. Committed history is a read-only snapshot and may advance independently.
