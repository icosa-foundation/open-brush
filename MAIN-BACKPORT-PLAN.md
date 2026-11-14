# Main backport audit and implementation plan

Audit date: 22 October 2026.

The session contains **182 unique fix commits**, across 23 branches with fixes in a collection of 24 PR worktrees. This plan checks all 182 against upstream main at [5591ff1aad5288903e718752c522d54420ae6d1e](https://github.com/icosa-foundation/open-brush/commit/5591ff1aad5288903e718752c522d54420ae6d1e), verified against the remote during this audit. The current checkout is a separate integration branch with unrelated local edits; its working files are not the baseline.

The proposed PR should contain only defects reachable through features main already has. Each numbered candidate below is a proposed independent implementation commit. Source commits are evidence and starting points; mixed feature commits require selected hunks or a small adaptation. Shared source fixes are consolidated once.

**Branch name and PR heading are reserved for the user.** This is a local, untracked plan. No implementation branch or PR has been created, and no implementation changes, commits or pushes were made for this audit.

The [existing commit and review map](PR-REVIEW-COMMIT-MAP.md) contains the complete original review text, including the distinction between review-driven and audit-only fixes. This plan adds main applicability, exact source evidence and implementation scope. The full per-branch appendix below accounts for every audited commit.

## Decisions

There are **14 distinct main candidates**, drawn from **16 source commits**: two include decisions and 14 adaptations. The overlaps are the save-clean marker correction and text layer persistence.

| # | Decision | Source commits |
| --- | --- | ---: |
| 1 | Include relevant existing-feature hunks | 2 |
| 2 | Adapt to main | 14 |
| 3 | Already handled on main | 13 |
| 4 | Feature-specific and excluded | 145 |
| 5 | Defer pending proof or behavior decision | 8 |
| 6 | Total audited | 182 |

“Include” means the relevant existing-feature hunk can be carried over; it does not assert the full source commit cherry-picks cleanly. “Adapt” means main needs a narrower or different implementation. “Already handled” refers to equivalent main behavior, not necessarily an ancestor commit. “Defer” requires evidence of a reachable main trigger or a deliberate behavior decision before inclusion.

## Proposed implementation order

Implement the numbered candidates in the order below, preserving a focused commit for each. Save the source commit URLs and review URLs in the new commit body, using explicit provenance such as `Source-commit:` and `Review:`; when several original fixes address the same bug, include all relevant references. Audit-only fixes should say so rather than inventing a review link.

Layer membership and pose fixes share a dependency: choose one helper for the persistent canvas of selected widgets, using `m_PreviousCanvas` where appropriate. Serialize layer, pose and size in that same canvas. A layer-only change cannot establish transformed-canvas round-trip correctness. Text and guide restoration must also bind the destination canvas before interpreting saved local coordinates. Keep those behavioral changes in the relevant persistence commits, rather than introducing animation machinery.

The playback quick-load, transformed-layer persistence and Lua symmetry candidates have explicit runtime validation gates. Hold any candidate out of the PR if its focused gate fails or remains unverified. The remaining candidates can still proceed independently. The duplicate localization entry is optional low-priority asset cleanup.

## Candidate commits

### 1 Keep the sketch dirty after failed or selected-only saves

The commit mixes a VOX-only unrecorded-change revision mechanism with a generic save-completion correction. Main unconditionally marks memory clean after WriteSnapshotToFile returns, including error and selected-only saves; those two triggers exist without VOX editing.

Native revision machinery is feature-only, but its independent save-success/full-save guard fixes main clearing dirty state after failed writes and SaveSelected.

**Source and review provenance:**

1. [135431b09](https://github.com/icosa-foundation/open-brush/commit/135431b09a8ed42e09c713232dc0f388693a1248) (feature/voxel-editing) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125975)
2. [18ede7cd9](https://github.com/icosa-foundation/open-brush/commit/18ede7cd9bee7cf40359b290b2fa7883d98a2b97) (feature/native-imm) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053294), [review 2](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541416)

**Scope:** Port only the error==null && !selectedOnly guard around SetLastOperationStackCount and InitialSketchTransform. Retain the main no-argument method. Do not port VOX dirty revisions, snapshot properties or wrapper changes. Treat concurrent-edit revision handling as separate unproven scope. Guard only SetLastOperationStackCount and InitialSketchTransform with error == null && !selectedOnly. Consolidate with voxel135431b09 under this key. Do not import native/VOX unrecorded revisions or playback state.

**Main evidence:**

1. [Assets/Scripts/Save/SaveLoadScript.cs:463](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L463) — SaveSelected calls SaveLow(... selectedOnly:true), reaching ThreadedSave.
2. [Assets/Scripts/Save/SaveLoadScript.cs:540](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L540) — ThreadedSave obtains the snapshot write result, then lines549-552 unconditionally call SetLastOperationStackCount and update InitialSketchTransform before testing error.
3. [Assets/Scripts/SketchMemoryScript.cs:288](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L288) — SetLastOperationStackCount stores the count used by dirty comparison at332.
4. [Assets/Scripts/Save/SaveLoadScript.cs:549](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L549) — After writeFuture completes, error is stored; lines551-552 unconditionally mark sketch clean and reset initial pose.

**Focused validation before inclusion:**

1. During implementation cover a failed full save retaining dirty state, a successful selected-only save retaining full-scene dirty state, and a successful full save marking clean; compile and narrowly exercise the existing save flow.
2. Failed write leaves original sketch dirty
3. Successful SaveSelected leaves original sketch dirty
4. Successful full save marks clean
5. Static C# check

### 2 Keep temporary transform canvases out of the layer list

The animation-specific ActiveCanvas failure is absent, but a separate main bug is proven: temporary transform canvases are registered as layers and synchronously cached by layer UI; destruction removes the layer without notifying that UI. Transforming selection can leave a phantom destroyed scratch layer in the panel.

**Source and review provenance:**

1. [9f4ef1257](https://github.com/icosa-foundation/open-brush/commit/9f4ef1257bf0abaf8cbc88ae168b5679d8229877) (feature/animation) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129359), [review 2](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531980)

**Scope:** Extract a neutral canvas allocation helper from main AddLayerNow, preserving parent identity, MainCanvas Unity layer and CanvasScript initialization but omitting layer-list registration/events. Use it in all three TransformItems scratch paths and dispose owned scratch batches/canvas without adding timeline APIs. Keep real AddLayerNow behavior unchanged.

**Main evidence:**

1. [Assets/Scripts/Commands/TransformItems.cs:16](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/TransformItems.cs#L16) — Transform/TransformSelected/TransformList all allocate registered scratch layers.
2. [Assets/Scripts/SceneScript.cs:255](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L255) — AddLayerNow registers the temporary layer and immediately emits LayerCanvasesUpdate.
3. [Assets/Scripts/Layers/LayerUI_Manager.cs:54](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Layers/LayerUI_Manager.cs#L54) — ResetUI caches the scratch layer synchronously through OnLayerCanvasesUpdate.
4. [Assets/Scripts/SceneScript.cs:262](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L262) — DestroyLayer removes it without a LayerCanvasesUpdate; cached UI still retains it.

**Focused validation before inclusion:**

1. Future: transform strokes/widgets with layer panel open; verify no phantom layer, correct active canvas, layer count and pose. Check scratch teardown and existing transform tests.

### 3 Save selected widgets in their authored canvas coordinates

Main reproduces authored-layer loss when selected widgets are saved: selection reparents widgets to SelectionCanvas, GetIndexOfCanvas maps that unlisted canvas to 0, and serializers write that index. A layer-only port is incomplete because saved local transforms still belong to SelectionCanvas.

**Source and review provenance:**

1. [c4f0cce4b](https://github.com/icosa-foundation/open-brush/commit/c4f0cce4b56fb4363c764618185578b807a83e66) (feature/animation) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935463)

**Scope:** Add a main-only authored-canvas/location helper using m_PreviousCanvas for selected widgets, and update model/audio/portal/Gaussian/video/light/image serializers. Preserve their pose and signed-size contracts: convert selected-canvas local save data into the chosen authored canvas or use a complete non-recording snapshot deselect/restore transaction. Do not add FrameId, timeline APIs or silently change layer ID alone.

**Main evidence:**

1. [Assets/Scripts/Save/MetadataUtils.cs:120](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/MetadataUtils.cs#L120) — Selected model widgets currently serialize SelectionCanvas as layer 0, rather than their remembered authored layer.
2. [Assets/Scripts/SelectionManager.cs:914](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SelectionManager.cs#L914) — SelectWidget remembers the authored canvas and then reparents to SelectionCanvas.
3. [Assets/Scripts/SceneScript.cs:280](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L280) — GetIndexOfCanvas returns 0 for SelectionCanvas because it is absent from m_LayerCanvases.
4. [Assets/Scripts/Widgets/ModelWidget.cs:832](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L832) — Model save pose is current-canvas local, requiring coordinate/size conversion when selecting authored layer.

**Focused validation before inclusion:**

1. Future: selected/unselected mixed widget save/reopen on translated, rotated and scaled layers; verify world pose, signed size, layer identity and undo/selection state.
2. c4f0 itself changes serializer layer lookup only; its animation helper is not a complete transformed-canvas main port.

### 4 Restore text layer membership and local pose

Reachable inherited main save bug: text on a nondefault layer serializes no LayerId, then reload defaults to layer0. The source hunk restores metadata membership, but main also applies a canvas-local saved pose before world-preserving SetCanvas, so a complete transformed-layer round-trip needs a small feature-free restore-order adaptation rather than a pose-correctness claim from the one-line patch.

Main TiltText already has LayerId and its loader reads it, but GetTiltText never assigns it. Text on non-main layers reloads on layer 0. Main loader also assigns saved local pose under main before world-preserving SetCanvas, so transformed-layer round trips require loader-order repair.

**Source and review provenance:**

1. [ca93a3813](https://github.com/icosa-foundation/open-brush/commit/ca93a381308b8d8000c7c592022b5e0d37136592) (feature/text-tool) — No matching review comment; branch-audit fix.
2. [fbe98e6b9](https://github.com/icosa-foundation/open-brush/commit/fbe98e6b9a1bd7b426fa972f582fb79860759e37) (feature/animation) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544029)

**Scope:** Add LayerId to existing GetTiltText, and in the consolidated text-layer fix bind the persistent target canvas before applying its saved local pose/size (avoid later world-preserving rebasing). Coordinate with overlapping animation serialization candidate; no TextTool or animation feature dependency. Assign existing TiltText.LayerId via authored-canvas helper. In FromTiltText, establish destination canvas before interpreting saved canvas-local transform/size; preserve grouping/pinning and material keywords. Omit FrameId and animation overloads.

**Dependencies:** Shared authored-canvas/pose helper from selected-widget-authored-layer-and-pose if selected text is included.

**Main evidence:**

1. [Assets/Scripts/Save/MetadataUtils.cs:163](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/MetadataUtils.cs#L163) — Initializer sets transform/style/group but omits LayerId.
2. [Assets/Scripts/Save/SketchSnapshot.cs:153](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchSnapshot.cs#L153) — Main snapshot writes TextWidgets = MetadataUtils.GetTiltText(m_GroupIdMapping).
3. [Assets/Scripts/Widgets/TextWidget.cs:162](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L162) — FromTiltText uses App.Scene.GetOrCreateLayer(tiltText.LayerId).
4. [Assets/Scripts/WidgetManager.cs:902](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/WidgetManager.cs#L902) — Main restore loop calls TextWidget.FromTiltText for saved text entries.
5. [Assets/Scripts/Widgets/Media2dWidget.cs:53](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/Media2dWidget.cs#L53) — SaveTransform stores TrTransform.FromLocalTransform(transform), with widget-local signed size.
6. [Assets/Scripts/Widgets/TextWidget.cs:145](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L145) — Loader initially parents to main, then applies local pose and finally SetCanvas.
7. [Assets/Scripts/Widgets/GrabWidget.cs:567](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GrabWidget.cs#L567) — SetCanvas reparents with transform.parent and rescales for canvas ratio at585–590, preserving world pose rather than interpreting saved coordinates in the target canvas.
8. [Assets/Scripts/Save/SketchMetadata.cs:712](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L712) — Existing TiltText includes LayerId; no schema/frame field is required.
9. [Assets/Scripts/Widgets/GrabWidget.cs:589](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GrabWidget.cs#L589) — SetCanvas preserves world pose and adjusts signed size; assigning it after applying wrong-parent local data is insufficient.

**Focused validation before inclusion:**

1. Planned validation: Compile current main and inspect serialized TiltText LayerId.
2. Planned validation: Save/reload text on default and transformed nondefault canvases; verify layer and placement, group and pinned state.
3. Planned validation: Avoid importing any SDF/animation or TextTool feature dependencies.
4. Future: text save/reopen on translated/rotated/scaled secondary layers, selected and unselected, verifying layer, world pose, size and old files with absent LayerId.
5. LayerId-only change is not accepted as a full transformed-layer fix.

### 5 Restore guide layer membership and local pose

Main Guides.State already has LayerId and restoration reads it, but GetSaveState never populates it. Non-main guide layer identity is lost; restoring its saved local transform before world-preserving SetCanvas also needs correction for transformed layers.

**Source and review provenance:**

1. [f2532ad1a](https://github.com/icosa-foundation/open-brush/commit/f2532ad1a85a117b1a5087d109c878e7738816f9) (feature/animation) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4083384768)

**Scope:** Populate existing guide LayerId using authored canvas; restore target canvas before interpreting guide-local translation/rotation/extents. Preserve shape/pin/group contracts and selected-guide coordinates. Exclude FrameId and animation GetOrCreateLayer overload.

**Dependencies:** Reuse authored-canvas/pose helper where applicable.

**Main evidence:**

1. [Assets/Scripts/Widgets/StencilWidget.cs:62](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/StencilWidget.cs#L62) — Returned state contains transform/extents/pin/group but omits LayerId.
2. [Assets/Scripts/Widgets/StencilWidget.cs:84](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/StencilWidget.cs#L84) — SaveState setter applies local pose under its original parent before reparenting.
3. [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Existing Guides.State includes LayerId; new FrameId is unnecessary on main.

**Focused validation before inclusion:**

1. Future: all guide types saved/reopened on translated/rotated/scaled secondary layers, selected/unselected, plus legacy default-layer metadata.

### 6 Preserve text content and appearance when duplicating

Reachable inherited main bug: create/customize text with existing text.add, then duplicate the widget. Main Clone instantiates the prefab and only copies transform/material state, losing text/font/fill/outline/mode. DuplicateSelectionCommand calls this method directly.

**Source and review provenance:**

1. [7f417378d](https://github.com/icosa-foundation/open-brush/commit/7f417378dae02247302e15b15be3b8900a743ed8) (feature/text-tool) — No matching review comment; branch-audit fix.

**Scope:** Apply the five source-state assignments after CloneInitialMaterials in existing TextWidget.Clone; do not import TextTool, keyboard, HTTP edit endpoints or exporter changes.

**Main evidence:**

1. [Assets/Scripts/Widgets/TextWidget.cs:119](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L119) — Main Clone lines119–138 never assigns Text, TMP font, TextColor, StrokeColor or Mode from the source.
2. [Assets/Scripts/Commands/DuplicateSelectionCommand.cs:76](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/DuplicateSelectionCommand.cs#L76) — Existing selection duplication calls widget.Clone(); the stamped duplication path also calls it at127.
3. [Assets/Scripts/API/ApiMethods.cs:1115](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.cs#L1115) — Main already exposes text.add, so a new VR TextTool is not required to create a text widget.

**Focused validation before inclusion:**

1. Planned validation: Compile current main after the isolated hunk.
2. Planned validation: Create text through existing text.add; customize label/fill/outline/font, duplicate via selection/stamp, and compare source/clone plus independence.
3. Planned validation: Exercise ordinary TMP text mode; do not claim completed geometric text extrusion.

### 7 Preserve audio and video playback state when duplicating

Animation-only drawing group/pin copying is excluded, but main audio/video Clone has the same general state loss: cloning a paused/seeking media widget does not copy paused/time state (nor video volume), despite load paths supporting it.

**Source and review provenance:**

1. [9a494e3da](https://github.com/icosa-foundation/open-brush/commit/9a494e3da065cb183224933547bbb36c7177f33f) (feature/animation) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754956)

**Scope:** Port only SoundClipWidget and VideoWidget hunks: capture source loaded-or-pending playback state before SetSoundClip/SetVideo and restore independent initial-state objects. Exclude AnimationUI_Manager group/pin drawing hunk.

**Main evidence:**

1. [Assets/Scripts/Widgets/SoundClipWidget.cs:301](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SoundClipWidget.cs#L301) — Clone copies audio settings but not playback time/paused state.
2. [Assets/Scripts/Widgets/SoundClipWidget.cs:69](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SoundClipWidget.cs#L69) — SetAudioProperties creates fresh state without copied Time/Paused.
3. [Assets/Scripts/Widgets/VideoWidget.cs:214](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/VideoWidget.cs#L214) — Clone calls SetVideo without copying loaded or pending VideoState.
4. [Assets/Scripts/Commands/DuplicateSelectionCommand.cs:76](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/DuplicateSelectionCommand.cs#L76) — Ordinary main duplication reaches these Clone methods.

**Focused validation before inclusion:**

1. Future: duplicate loaded and deferred audio/video with nonzero seek, paused/playing, nondefault volume; assert independent state and unchanged source.
2. Compile against main and test media initialization callbacks; no checks executed in this plan.

### 8 Decode POST form values exactly once

Main decodes the entire POST form before splitting on ampersands and decodes values again in EnqueueCommand; encoded delimiters/plus/nested escapes are already corrupted independently of polling.

**Source and review provenance:**

1. [646b88d3e](https://github.com/icosa-foundation/open-brush/commit/646b88d3ee2d9dd9ccefdee7b6915cca72318d2d) (feature/polling-stroke-listener) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1025#discussion_r4006159104)

**Scope:** Split encoded POST body into command fields before decoding. Preserve percent escapes and replace form + with %20 so EnqueueCommand decodes values exactly once. Adapt tests to existing main endpoints; do not add polling registry/endpoints.

**Main evidence:**

1. [Assets/Scripts/API/ApiManager.cs:669](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L669) — Global Uri.UnescapeDataString occurs before formdata.Replace(+)/Split(&).
2. [Assets/Scripts/API/ApiManager.cs:227](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L227) — EnqueueCommand calls UnityWebRequest.UnEscapeURL again on parameter value.

**Focused validation before inclusion:**

1. POST %26 stays within one parameter
2. %2B becomes literal plus, + becomes space, %2520 becomes literal %20
3. Multiple fields remain separate
4. Existing GET/WebSocket tests

### 9 Dispose the catalog refresh iterator when replacing it

Collection refresh/query behavior is feature-only, but main RequestForcedRefresh also incorrectly StopCoroutine-cancels a manually MoveNext-pumped iterator, bypassing disposal of its nested request enumerator.

**Source and review provenance:**

1. [972f6b97f](https://github.com/icosa-foundation/open-brush/commit/972f6b97f3d0c54969e4b865d1cb09f93380fcd9) (feature/icosa-collections) — No matching review comment; branch-audit fix.

**Scope:** Adapt cancellation lifetime only: dispose main existing m_FetchMetadataCoroutine in RequestForcedRefresh, then clear it and retain normal forced-refresh behavior. Do not add SelectCollection, force-query matching or collection tile state.

**Main evidence:**

1. [Assets/Scripts/Poly/IcosaAssetCatalog.cs:653](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L653) — RequestForcedRefresh calls StopCoroutine on metadata IEnumerator then discards it.
2. [Assets/Scripts/Poly/IcosaAssetCatalog.cs:1013](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L1013) — Update pumps the iterator with MoveNext; it is not a registered Unity coroutine.
3. [Assets/Scripts/Poly/IcosaAssetCatalog.cs:1474](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L1474) — RefreshAssetSet owns NextPage enumerator inside using; disposing the outer iterator unwinds its request lifetime.

**Focused validation before inclusion:**

1. Forced refresh during in-flight catalog page disposes old NextPage iterator
2. Replacement query populates normally
3. Ordinary completed refresh unaffected

### 10 Start empty timestamp playback without throwing

Main timestamp startup calls GetEarliestTimestamp on empty memory and that helper explicitly throws. Adapt only the timestamp guard, not branch RealTime fallback.

**Source and review provenance:**

1. [6f83010fb](https://github.com/icosa-foundation/open-brush/commit/6f83010fb274fa4d5cb3f642e402bb877c9cf33a) (feature/mp-stroke-streaming) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310368)

**Scope:** Use zero when m_MemoryList empty in existing timestamp-mode assignment; retain earliest timestamp otherwise. Do not add RealTime mode/session metadata.

**Main evidence:**

1. [Assets/Scripts/SketchMemoryScript.cs:1154](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L1154) — Timestamp startup unconditionally calls GetEarliestTimestamp.
2. [Assets/Scripts/SketchMemoryScript.cs:525](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L525) — GetEarliestTimestamp explicitly throws if m_MemoryList empty.

**Focused validation before inclusion:**

1. Start timestamp playback on empty/widget-only sketch
2. Nonempty sketch retains earliest start time

### 11 Finish quick-load with a finite playback clock

Main quick-load sets a float.MaxValue clock sentinel, while timestamp-mode completion skips restoration. Main also casts the sentinel to int milliseconds, so port needs bounded conversion as well as completion reset.

**Source and review provenance:**

1. [b6550cd78](https://github.com/icosa-foundation/open-brush/commit/b6550cd78d6e06b9af50fa0d121568988ea67f05) (feature/mp-stroke-streaming) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310361)

**Scope:** Adapt completion guard using main SketchControlsScript.SketchPlaybackMode property, resetting sentinel to latest timestamp or zero. Handle quick-load sentinel safely before main integer millisecond conversion so playback can complete; coordinate other timestamp conversion work. No streaming sessions or RealTime mode.

**Dependencies:** Coordinate with any other planned timestamp conversion fix

**Main evidence:**

1. [Assets/Scripts/Playback/ScenePlaybackByTime.cs:219](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Playback/ScenePlaybackByTime.cs#L219) — QuickLoadRemaining sets CurrentSketchTime=float.MaxValue.
2. [Assets/Scripts/App.cs:2321](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/App.cs#L2321) — Timestamp-mode OnPlaybackComplete skips clock restoration.
3. [Assets/Scripts/Playback/ScenePlaybackByTime.cs:104](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Playback/ScenePlaybackByTime.cs#L104) — Update directly converts CurrentSketchTime*1000 to int, requiring explicit saturation/sentinel handling.
4. [Assets/Scripts/SketchMemoryScript.cs:1443](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L1443) — New stroke timestamps cast CurrentSketchTime*1000 to uint.

**Focused validation before inclusion:**

1. Timestamp quick-load finishes and returns finite usable clock
2. Draw after quick-load yields sensible uint timestamps
3. Ordinary timestamp rewind/playback, empty and distance modes remain correct

### 12 Preserve path colors and pressure during symmetry expansion

Multipath preview storage is feature-only, but existing main commit-time symmetry expansion passes unexpanded path colors and transformed scales as source pressure. Adapt those proven attributes without importing multipath preview state.

**Source and review provenance:**

1. [cfde2400d](https://github.com/icosa-foundation/open-brush/commit/cfde2400dd7db45b6382112c558396f496ca9310) (feature/multipath-toolscript-previews) — No matching review comment; branch-audit fix.

**Scope:** Adapt commit-time expansion only: duplicate each source color alongside its symmetry copies, preserve original per-point pressure after reflected orientation correction, and apply intended base brush scale once. Keep main single-path preview result/API unchanged. Multipath preview symmetry copies require separate feature storage and are not part of this port.

**Main evidence:**

1. [Assets/Scripts/API/Lua/LuaManager.cs:1532](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L1532) — Main expands each source path across symmetries, but passes original color list to expanded paths at1552.
2. [Assets/Scripts/API/DrawStrokes.cs:47](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/DrawStrokes.cs#L47) — DrawNestedTrList indexes colors by expanded path index; two copies of first path incorrectly consume second path color.
3. [Assets/Scripts/API/Lua/LuaManager.cs:1538](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L1538) — sym * baseTransform * point carries transform scale into point.scale, which DrawStrokes line62 interprets as control-point pressure.
4. [Assets/Scripts/API/DrawStrokes.cs:108](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/DrawStrokes.cs#L108) — Brush scale is a distinct field; main no-symmetry path applies base transform via Stroke.Recreate.

**Focused validation before inclusion:**

1. Two differently colored paths with two symmetries retain source colors on all copies
2. Nonunit base transform retains source pressure and intended brush size
3. Mirrored orientation matches existing convention
4. No-symmetry and canvas-space output unchanged

### 13 Apply the multiplayer passthrough policy to direct backdrop changes

Main already blocks preset changes away from passthrough with remote participants, but direct gradient/custom-skybox entry points bypass that admission check and change RenderSettings. This general policy bypass exists independently of dynamic backdrop modes.

**Source and review provenance:**

1. [da4cdf0ec](https://github.com/icosa-foundation/open-brush/commit/da4cdf0ecb7025a875865fed43ddef53c0919771) (feature/backdrop-passthrough-settings) — [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391712)

**Scope:** Adapt only admission checks to existing main preset.isPassthrough plus HasRemotePlayersInRoom. Guard external gradient/custom-skybox routes (including cache/async completion as appropriate) without introducing PassthroughEnabled, BackdropMode or new environment fields. Preserve allowed environment-restore/internal transition sequencing.

**Main evidence:**

1. [Assets/Scripts/SceneSettings.cs:994](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L994) — Existing main policy checks desired preset isPassthrough and remote room participants.
2. [Assets/Scripts/SceneSettings.cs:197](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L197) — Setter can call TransitionToGradient without multiplayer policy check.
3. [Assets/Scripts/SceneSettings.cs:220](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L220) — Direct API/background loading has no equivalent check.
4. [Assets/Scripts/SceneSettings.cs:647](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L647) — Gradient directly replaces the backdrop even while the preset remains passthrough.

**Focused validation before inclusion:**

1. Future: multiplayer passthrough room, attempt gradient/background/API changes; confirm denied; repeat alone and in normal preset to confirm allowed. Check saved-environment restoration and HDR completion after room membership changes.
2. Logical bypass is source-proven; exact headset rendering remains unvalidated.

### 14 Remove the duplicate shared localization entry

Pinned main contains two identical VIEW_MODE_CONTROLS entries with the same key ID. Remove the redundant block as an asset-integrity fix: avoid duplicate editor rows and ambiguous per-entry rename/remove behavior. Do not claim a current runtime lookup exception: installed SharedTableData lookup uses dictionary assignment and shadows the duplicate.

**Source and review provenance:**

1. [464368483](https://github.com/icosa-foundation/open-brush/commit/464368483419b04e20117af8f658a91e1d88acfb) (feature/text-tool) — [review 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558407)

**Scope:** Apply only removal of one exact duplicate VIEW_MODE_CONTROLS shared-data block. Preserve the remaining ID/key and all language tables/metadata. No text-tool code or enum changes.

**Main evidence:**

1. [Assets/Settings/Localization/Strings/Strings Shared Data.asset:3482](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Settings/Localization/Strings/Strings%20Shared%20Data.asset#L3482) — Duplicate tuple (317029250049335296, VIEW_MODE_CONTROLS) occurs at3482–3489 with identical empty metadata.

**Focused validation before inclusion:**

1. Planned validation: Check exactly one occurrence of the targeted ID/key tuple remains and no unrelated YAML entries changed.
2. Planned validation: Load the localization shared table/editor rows when Unity validation becomes available; do not claim a runtime crash fix.


## Validation and PR preparation

1. Recheck upstream main before implementation; refresh these decisions if the affected paths changed. Respect the existing session's ownership of this checkout. Starting an implementation checkout or branch is a separate step after the user supplies the branch name.
2. Implement only each candidate's stated scope. Compile affected C# through the project's available narrow checks. Use focused cases listed with each candidate; avoid adding a general test framework for this backport.
3. Persistence checks must cover selected and unselected widgets, default and transformed nondefault layers, normal save and autosave, and subsequent reload. Verify layer, local/world placement, scale, pinned state and group where relevant. A successful compile alone does not prove this behavior.
4. Exercise the API only while Open Brush is running or the Editor is in Play mode. Check decoding boundaries without adding polling or transport features.
5. Runtime checks for text, playback, audio/video and symmetry must be reported separately from static inspection. This audit performed patch and source/call-path review; it did not run Unity, device tests or builds.
6. Review the final diff against the refreshed main baseline for feature imports and unrelated changes. Keep this plan and other temporary audit files untracked. Have the user supply the PR heading before any later authorized PR creation.

## Deferred fixes

1. [c39db389a](https://github.com/icosa-foundation/open-brush/commit/c39db389ad5930b48340b6ea51d7e7365030c093) — The bounds utility exists on main, but this reported failure relies on hidden VOX instances introduced by the editable-document pipeline. Main's VoxImporter creates every imported model object active and does not apply VOX hidden flags. Including inactive geometry in every non-glTF importer may alter intended bounds semantics; a shared reproducer is not established.

2. [b52459117](https://github.com/icosa-foundation/open-brush/commit/b5245911725b0a35d561fc8f51f7a3f7d3eeb5d1) — Removes deformation only for the branch OpenBrush profile. Main has legacy ProcessMesh calls but no matching profile/import restoration contract; shared baking calls do not prove that globally removing baking is safe.

3. [270259a79](https://github.com/icosa-foundation/open-brush/commit/270259a79132d5bd3f5ab681f625ffb0fef25d51) — Main has a potentially faulty nonlegacy single-clip playable loop, but the current importer explicitly forces AnimationMethod.Legacy. No production nonlegacy main trigger was established; branch seek/restart APIs are also absent.

4. [db0ab9428](https://github.com/icosa-foundation/open-brush/commit/db0ab942875716e5045d3309f6b9dc912b5d4be0) — The shared docs generator does enumerate all reflected parameters on main, but the current registered main wrapper APIs do not expose injected MoonSharp Script/ScriptExecutionContext/CallbackArguments parameters. The motivating ForceGuideSnappingOff(Script, bool) exists only on the SDF branch. No current main documentation failure is proven; retain as hardening for a future injected-parameter API.

5. [27c9e785e](https://github.com/icosa-foundation/open-brush/commit/27c9e785e3e21172cc1312d29cc217eff6d44516) — Main contains the same wrong-image-count text-index helper, but it is private and its only main reference is inside commented-out text.extrude. Existing text.add does not call it. There is no reachable negative-index main endpoint trigger; fix alongside future text operations, not as a proven currently active bug.

6. [115e75af9](https://github.com/icosa-foundation/open-brush/commit/115e75af9ce256b023ec0ff5efd64938c5aed072) — Main also captures metadata after timeslice yields, but the proven original trigger is animation playback changing unrecorded frame locations. Main lacks animation, and recorded edits call SketchChanged which aborts an in-progress autosave. A concrete non-aborted main corruption case is still needed before moving capture timing.

7. [0cbc16fa6](https://github.com/icosa-foundation/open-brush/commit/0cbc16fa667eef033e92807973913d24deec6d02) — The two main memorize methods do retain the incoming color list, but all main callers are PointerScript, which relinquishes that list by setting it null after detach, and StrokeData copy already deep-copies colors. No reachable main alias/mutation corruption was proved; do not port copying merely because assignments match.

8. [b1e3285b3](https://github.com/icosa-foundation/open-brush/commit/b1e3285b32e7b84d49e517c8eb5f4c2e250d29bd) — Main has the same bare StartsWith root matcher, but only fixed built-in Models/Blocks roots and catalog navigation/scanning constrained to those roots were verified. No reachable sibling-prefix input was proved. The exact patch also fails to accept equality with a root directory, so cannot be imported as-is.

## Complete audit by branch

Every entry links its source commit, available matching review comments, and pinned-main evidence. “No matching review comment” identifies a fix from the broader branch audit; it is not a missing attribution to fabricate.

### 1 feature/3d-shapes

1. [9dc74ec56](https://github.com/icosa-foundation/open-brush/commit/9dc74ec567ed20fc578e4b182af7c300160c9752) — Keep editable shape clone renderer maps on the clone. **feature-only**. Fixes renderer ownership in feature EditableModelWidget.Clone; ordinary main ModelWidget.Clone already binds clone maps correctly.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Widgets/ModelWidget.cs:214](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L214) — Ordinary main ModelWidget.Clone already initializes renderer/material maps on the clone; EditableModelWidget, Polyhydra preview/presets and EditableModelManager are absent.

2. [78aa8ec01](https://github.com/icosa-foundation/open-brush/commit/78aa8ec013315c028adbe49420c9516adf8bc4ac) — Fit editable shape interaction bounds to offset and rebuilt meshes. **feature-only**. Fits colliders and refreshes bounds after procedural editable-shape regeneration. Main has no EditableModelManager/PolyRecipe regeneration path.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Widgets/ModelWidget.cs:214](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L214) — Ordinary main ModelWidget.Clone already initializes renderer/material maps on the clone; EditableModelWidget, Polyhydra preview/presets and EditableModelManager are absent.

3. [285b54461](https://github.com/icosa-foundation/open-brush/commit/285b544612f5d1c70781459595d1903fa0983e30) — Release generated shape preview meshes on rebuild and teardown. **feature-only**. Disposes generated meshes owned by feature Polyhydra preview; main does not instantiate that preview or generate its meshes.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Widgets/ModelWidget.cs:214](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L214) — Ordinary main ModelWidget.Clone already initializes renderer/material maps on the clone; EditableModelWidget, Polyhydra preview/presets and EditableModelManager are absent.

4. [f85b190fa](https://github.com/icosa-foundation/open-brush/commit/f85b190fae5aee3d69c9d5faeac0240dfe2cdabc) — Remove only the final JSON extension from shape preset paths. **feature-only**. Corrects final-extension handling in feature Polyhydra preset authoring; main has no shape recipe/preset panel or CurrentPresetPath setter.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Widgets/ModelWidget.cs:214](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L214) — Ordinary main ModelWidget.Clone already initializes renderer/material maps on the clone; EditableModelWidget, Polyhydra preview/presets and EditableModelManager are absent.

### 2 feature/animation

1. [9f4ef1257](https://github.com/icosa-foundation/open-brush/commit/9f4ef1257bf0abaf8cbc88ae168b5679d8229877) — Keep transform scratch canvases outside the animation timeline. **adapt; candidate 2**. The animation-specific ActiveCanvas failure is absent, but a separate main bug is proven: temporary transform canvases are registered as layers and synchronously cached by layer UI; destruction removes the layer without notifying that UI. Transforming selection can leave a phantom destroyed scratch layer in the panel.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129359), [review 2](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531980)

   Main: [Assets/Scripts/Commands/TransformItems.cs:16](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/TransformItems.cs#L16) — Transform/TransformSelected/TransformList all allocate registered scratch layers.; [Assets/Scripts/SceneScript.cs:255](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L255) — AddLayerNow registers the temporary layer and immediately emits LayerCanvasesUpdate.; [Assets/Scripts/Layers/LayerUI_Manager.cs:54](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Layers/LayerUI_Manager.cs#L54) — ResetUI caches the scratch layer synchronously through OnLayerCanvasesUpdate.; [Assets/Scripts/SceneScript.cs:262](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L262) — DestroyLayer removes it without a LayerCanvasesUpdate; cached UI still retains it.

2. [efb94d5ab](https://github.com/icosa-foundation/open-brush/commit/efb94d5abaaa0a55a7ed9560927ec5f0efe337f8) — Preserve model widget canvas when deferred loading completes. **already-fixed**. Main LoadModel already instantiates content under the widget without reparenting the widget to the current active canvas. The removed deferred-load reparent was introduced only in the feature branch.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3914129368), [review 2](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3992531985)

   Main: [Assets/Scripts/Widgets/ModelWidget.cs:299](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L299) — Main lines immediately before/after this assignment contain no SetParent(App.Scene.ActiveCanvas) on the widget.

3. [91c91f4fd](https://github.com/icosa-foundation/open-brush/commit/91c91f4fd768acb678aca3fdaa4aeed3bf1c255e) — Ignore parentless widgets during timeline registration. **feature-only**. Guards only animation drawing-registration notifications for parentless widgets; main has no timeline notification entry points.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4171082976)

   Main: [Assets/Scripts/SceneScript.cs:243](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L243) — Main creates one canvas per ordinary layer; AnimationUI_Manager, sparse tracks, frame drawings and animation path APIs are absent from this base.

4. [4accf0855](https://github.com/icosa-foundation/open-brush/commit/4accf085565ee884754f1db5ab54944c502536fc) — Avoid subtracting hidden stroke cost when destroying canvases. **feature-only**. Changes complexity subtraction in animation DestroyCanvas, not main DestroyLayer. Main has no per-drawing destruction/accounting loop or hidden-frame rendering lifetime.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3980000181)

   Main: [Assets/Scripts/SceneScript.cs:262](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L262) — Main only removes the layer, destroys its batches and GameObject; it has no changed stroke-cost subtraction to guard.

5. [8da8fec90](https://github.com/icosa-foundation/open-brush/commit/8da8fec906ec08c4348805e958110c484f05ccd7) — Synchronize explicit layer visibility with animation tracks. **feature-only**. Synchronizes layer visibility with a new animation-track model; ordinary main layer visibility is already authoritative without a second track flag.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3959825460)

   Main: [Assets/Scripts/SceneScript.cs:311](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L311) — Main ShowLayer/HideLayer toggle the one canvas and emit layer UI updates, with no timeline visibility state.

6. [58b584c0e](https://github.com/icosa-foundation/open-brush/commit/58b584c0e4e7039f3ed9eed2cbd578275ede3b72) — Clamp the timeline playhead when reducing the first frame. **feature-only**. Clamps a frame-reduction playhead in AnimationUI_Manager; main has no timeline/playhead or ReduceKeyFrame call path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4121872889)

   Main: [Assets/Scripts/SceneScript.cs:243](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L243) — Main creates one canvas per ordinary layer; AnimationUI_Manager, sparse tracks, frame drawings and animation path APIs are absent from this base.

7. [a8b0c3111](https://github.com/icosa-foundation/open-brush/commit/a8b0c311163b398ba190aab2d0bde654f7db98fb) — Keep camera capture active and target paths in the current tool mode. **feature-only**. Removes feature-added capture-rig shutdown and separates new animation paths from camera paths. Main already targets CameraPathWidgets and has no animation-position/rotation tool modes.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3953112640), [review 2](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4121872880)

   Main: [Assets/Scripts/Tools/CameraPathTool.cs:482](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/CameraPathTool.cs#L482) — Main path hit testing is limited to camera paths; the problematic AllPathWidgets/captureRig animation branch is absent.

8. [115e75af9](https://github.com/icosa-foundation/open-brush/commit/115e75af9ce256b023ec0ff5efd64938c5aed072) — Capture animation metadata before timesliced stroke snapshots yield. **defer**. Main also captures metadata after timeslice yields, but the proven original trigger is animation playback changing unrecorded frame locations. Main lacks animation, and recorded edits call SketchChanged which aborts an in-progress autosave. A concrete non-aborted main corruption case is still needed before moving capture timing.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3983404338)

   Main: [Assets/Scripts/Save/SketchSnapshot.cs:118](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchSnapshot.cs#L118) — Stroke capture can yield before GetSketchMetadata at the end.; [Assets/Scripts/Save/SketchSnapshot.cs:91](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchSnapshot.cs#L91) — TimeslicedConstructor captures metadata after stroke snapshots.; [Assets/Scripts/Save/SaveLoadScript.cs:941](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L941) — SketchChanged aborts the active autosave; ordinary edits do not automatically reproduce the animation race.

9. [c4f0cce4b](https://github.com/icosa-foundation/open-brush/commit/c4f0cce4b56fb4363c764618185578b807a83e66) — Serialize selected widgets using their authored animation canvas. **adapt; candidate 3**. Main reproduces authored-layer loss when selected widgets are saved: selection reparents widgets to SelectionCanvas, GetIndexOfCanvas maps that unlisted canvas to 0, and serializers write that index. A layer-only port is incomplete because saved local transforms still belong to SelectionCanvas.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935463)

   Main: [Assets/Scripts/Save/MetadataUtils.cs:120](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/MetadataUtils.cs#L120) — Selected model widgets currently serialize SelectionCanvas as layer 0, rather than their remembered authored layer.; [Assets/Scripts/SelectionManager.cs:914](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SelectionManager.cs#L914) — SelectWidget remembers the authored canvas and then reparents to SelectionCanvas.; [Assets/Scripts/SceneScript.cs:280](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L280) — GetIndexOfCanvas returns 0 for SelectionCanvas because it is absent from m_LayerCanvases.; [Assets/Scripts/Widgets/ModelWidget.cs:832](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#L832) — Model save pose is current-canvas local, requiring coordinate/size conversion when selecting authored layer.

10. [fbe98e6b9](https://github.com/icosa-foundation/open-brush/commit/fbe98e6b9a1bd7b426fa972f582fb79860759e37) — Persist text widget animation layer and frame locations. **adapt; candidate 4**. Main TiltText already has LayerId and its loader reads it, but GetTiltText never assigns it. Text on non-main layers reloads on layer 0. Main loader also assigns saved local pose under main before world-preserving SetCanvas, so transformed-layer round trips require loader-order repair.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544029)

   Main: [Assets/Scripts/Save/MetadataUtils.cs:163](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/MetadataUtils.cs#L163) — Initializer sets transform/style/group but omits LayerId.; [Assets/Scripts/Save/SketchMetadata.cs:712](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L712) — Existing TiltText includes LayerId; no schema/frame field is required.; [Assets/Scripts/Widgets/TextWidget.cs:145](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L145) — Loader initially parents to main, then applies local pose and finally SetCanvas.; [Assets/Scripts/Widgets/GrabWidget.cs:589](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GrabWidget.cs#L589) — SetCanvas preserves world pose and adjusts signed size; assigning it after applying wrong-parent local data is insufficient.

11. [f2532ad1a](https://github.com/icosa-foundation/open-brush/commit/f2532ad1a85a117b1a5087d109c878e7738816f9) — Save and restore guide widgets on their animation frames. **adapt; candidate 5**. Main Guides.State already has LayerId and restoration reads it, but GetSaveState never populates it. Non-main guide layer identity is lost; restoring its saved local transform before world-preserving SetCanvas also needs correction for transformed layers.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4083384768)

   Main: [Assets/Scripts/Widgets/StencilWidget.cs:62](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/StencilWidget.cs#L62) — Returned state contains transform/extents/pin/group but omits LayerId.; [Assets/Scripts/Widgets/StencilWidget.cs:84](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/StencilWidget.cs#L84) — SaveState setter applies local pose under its original parent before reparenting.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Existing Guides.State includes LayerId; new FrameId is unnecessary on main.

12. [545af4389](https://github.com/icosa-foundation/open-brush/commit/545af43893b823d2c84b645bb675dacafce7984c) — Initialize deferred media canvases with current frame visibility. **feature-only**. Fixes visibility of lazily materialized empty animation spans. Main creates an ordinary layer canvas immediately and has no deferred per-frame content canvas.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544016)

   Main: [Assets/Scripts/SceneScript.cs:243](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L243) — Main creates one canvas per ordinary layer; AnimationUI_Manager, sparse tracks, frame drawings and animation path APIs are absent from this base.

13. [afc326b29](https://github.com/icosa-foundation/open-brush/commit/afc326b298326acc449731891aa6928097c34915) — Rename every drawing canvas in an animation layer. **feature-only**. Renames all drawing canvases in one animation track. Main owns a single canvas per layer and already renames it directly.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3959825466)

   Main: [Assets/Scripts/SceneScript.cs:355](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L355) — Main renames its single layer canvas; there are no held-frame canvases or track-wide frame names.

14. [de39ba92c](https://github.com/icosa-foundation/open-brush/commit/de39ba92c3208903803ec964a04010e62ecee062) — Save only live nonempty animation paths at their current timeline location. **feature-only**. Filters and relocates layer-animation paths, not ordinary camera paths. Main has neither AnimationPathWidgets nor animation timeline locations.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916977552), [review 2](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916977562), [review 3](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3983404326)

   Main: [Assets/Scripts/Save/MetadataUtils.cs:81](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/MetadataUtils.cs#L81) — Main serializes ordinary CameraPathWidgets; branch-only GetAnimationPaths and path track/frame locations are absent.

15. [71f66c64c](https://github.com/icosa-foundation/open-brush/commit/71f66c64c0cae577d03b3c527e4b17cd1fd93482) — Detach and restore animation paths with widget creation undo. **feature-only**. Undo detaches/reconnects a path to sparse animation spans and adds drawing-index notification. Main CreateWidgetCommand has no animation attachment or drawing notification to maintain.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3953112636)

   Main: [Assets/Scripts/Commands/CreateWidgetCommand.cs:67](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/CreateWidgetCommand.cs#L67) — Main ordinary widget undo hides the widget; it owns no timeline span path reference.

16. [571d51a4b](https://github.com/icosa-foundation/open-brush/commit/571d51a4bfac70ff1a3125701186f1f6d1620437) — Unregister and account for widgets before destroying drawing canvases. **feature-only**. Adds widget cost/unregister bookkeeping specifically to new drawing-canvas garbage collection. Main has no DestroyCanvas/frame repository. Its destructive layer callers are reset, scratch transforms and LoadQuill cleanup; LoadQuill explicitly unregisters imported widgets.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3916935482)

   Main: [Assets/Scripts/SceneScript.cs:262](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L262) — No frame-drawing lifetime or animation GC exists in main.; [Assets/Scripts/Commands/LoadQuillCommand.cs:128](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/LoadQuillCommand.cs#L128) — Main import teardown already has explicit widget unregister, independent of the feature drawing destructor.

17. [9a494e3da](https://github.com/icosa-foundation/open-brush/commit/9a494e3da065cb183224933547bbb36c7177f33f) — Preserve pin group and media playback state in copied animation drawings. **adapt; candidate 7**. Animation-only drawing group/pin copying is excluded, but main audio/video Clone has the same general state loss: cloning a paused/seeking media widget does not copy paused/time state (nor video volume), despite load paths supporting it.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754956)

   Main: [Assets/Scripts/Widgets/SoundClipWidget.cs:301](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SoundClipWidget.cs#L301) — Clone copies audio settings but not playback time/paused state.; [Assets/Scripts/Widgets/SoundClipWidget.cs:69](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SoundClipWidget.cs#L69) — SetAudioProperties creates fresh state without copied Time/Paused.; [Assets/Scripts/Widgets/VideoWidget.cs:214](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/VideoWidget.cs#L214) — Clone calls SetVideo without copying loaded or pending VideoState.; [Assets/Scripts/Commands/DuplicateSelectionCommand.cs:76](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/DuplicateSelectionCommand.cs#L76) — Ordinary main duplication reaches these Clone methods.

18. [27e94b85c](https://github.com/icosa-foundation/open-brush/commit/27e94b85cbcc9bc1a36b9c7deb102a60e82f7916) — Keep temporary animation planning documents untracked. **already-fixed**. This is only deletion/untracking of temporary animation plans; neither plan is tracked at the verified main base.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r4005754948)

   Main: [AGENTS.md:3](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/AGENTS.md#L3) — Main already requires temporary plans to remain local; git tree confirms ANIMATION-SCALABILITY-PLAN.md and ANIMATION-TASKS.md absent.

19. [c4e53ecb2](https://github.com/icosa-foundation/open-brush/commit/c4e53ecb26b42e775cb778bc761c1ee14f32c585) — Compose animation path motion with the drawing base pose and save that pose. **feature-only**. Composes animated path motion with a retained drawing base pose and serializes that authored pose. Main has no path-driven layer animation; its layer transform is already its authored pose.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3980000162)

   Main: [Assets/Scripts/SceneScript.cs:457](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L457) — Main layer metadata saves LocalPose directly and no animation update overwrites it.

20. [0cbabef5d](https://github.com/icosa-foundation/open-brush/commit/0cbabef5df80e7d9c9d28efc6e570ab1ac8b622e) — Keep the canonical main canvas stable while projecting track zero. **feature-only**. Separates canonical startup canvas from projected track-zero drawing, with animation-aware delete/rename protections. Main already keeps m_MainCanvas stable and has no timeline projection.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3952875458)

   Main: [Assets/Scripts/SceneScript.cs:149](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L149) — MainCanvas is the stable serialized m_MainCanvas; main does not project a timeline frame into it.

21. [c8750f25c](https://github.com/icosa-foundation/open-brush/commit/c8750f25cb462e2090defbf7e4076589179f7843) — Squash complete animation tracks across source and destination span boundaries. **feature-only**. Replaces layer squash with complete timeline span-boundary combination and animation undo retention. Main SquashLayerCommand works on its sole source/destination canvases, with no track/frame states to lose.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3968544002)

   Main: [Assets/Scripts/Commands/SquashLayerCommand.cs:52](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/SquashLayerCommand.cs#L52) — Main squash moves source strokes/widgets to the destination and has ordinary undo; no timeline traversal or spans exist.

22. [0cbc16fa6](https://github.com/icosa-foundation/open-brush/commit/0cbc16fa667eef033e92807973913d24deec6d02) — Copy per-point colors into each memorized stroke. **defer**. The two main memorize methods do retain the incoming color list, but all main callers are PointerScript, which relinquishes that list by setting it null after detach, and StrokeData copy already deep-copies colors. No reachable main alias/mutation corruption was proved; do not port copying merely because assignments match.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1147#discussion_r3915196421)

   Main: [Assets/Scripts/SketchMemoryScript.cs:613](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L613) — Same assignment exists but caller ownership must be considered.; [Assets/Scripts/PointerScript.cs:1271](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/PointerScript.cs#L1271) — After detach the pointer relinquishes its list rather than clearing/reusing it.; [Assets/Scripts/StrokeData.cs:82](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/StrokeData.cs#L82) — Main duplicate StrokeData already owns a color-list copy.

23. [7e7416890](https://github.com/icosa-foundation/open-brush/commit/7e741689049cbd8a15035e1ee51f99f9f80c7837) — Guard the animation editor namespace import in player builds. **feature-only**. Guards UnityEditor import in new animation runtime manager. That runtime source does not exist on main; no player import to guard there.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/SceneScript.cs:243](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L243) — Main creates one canvas per ordinary layer; AnimationUI_Manager, sparse tracks, frame drawings and animation path APIs are absent from this base.

24. [0d98b791a](https://github.com/icosa-foundation/open-brush/commit/0d98b791af29eb53c8a74843d7134ca213543220) — Avoid false stroke count warnings when duplicating animation frames. **feature-only**. Removes false warnings in new animation frame replication. Main has no ReplicateDrawingToNewCanvas or frame copy loop.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/SceneScript.cs:243](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L243) — Main creates one canvas per ordinary layer; AnimationUI_Manager, sparse tracks, frame drawings and animation path APIs are absent from this base.

### 3 feature/backdrop-passthrough-settings

1. [201fe4984](https://github.com/icosa-foundation/open-brush/commit/201fe49849fe7ff26a681b2e4236553f23318f85) — Reference the packaged passthrough hull material. **already-fixed**. Main PassthroughHull descriptor and renderer already reference packaged ce8b2be5 material. The new panel preview field and orphan fe9ceb meta were branch additions.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391656)

   Main: [Assets/Resources/X/Brushes/PassthroughHull/PassthroughHull.asset:51](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Resources/X/Brushes/PassthroughHull/PassthroughHull.asset#L51) — Verified main m_Material GUID is ce8b2be5f7b63f344ae603ba6f4b4aec.; [Assets/Resources/X/Brushes/PassthroughHull/HullPrefabPassthrough.prefab:79](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Resources/X/Brushes/PassthroughHull/HullPrefabPassthrough.prefab#L79) — Main renderer already references the correct packaged material.

2. [317e73442](https://github.com/icosa-foundation/open-brush/commit/317e7344203ca5d6134bbb569c06e877d6b18161) — Scope persistent button highlights to backdrop controls. **already-fixed**. Shared TextActionButton already uses selected-state visibility on main. The custom always-visible mode subclass/prefab replacement belongs to the new backdrop mode UI, which main lacks.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391721)

   Main: [Assets/Scripts/GUI/TextActionButton.cs:44](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/TextActionButton.cs#L44) — Main common button behavior already matches the safe fixed hunk.

3. [cf98d0d5d](https://github.com/icosa-foundation/open-brush/commit/cf98d0d5d9227162a5b1328d53bf6eb437ddde46) — Hide unfinished passthrough controls and remove missing callbacks. **feature-only**. Removes TogglePassthrough callbacks and hides unfinished controls introduced in the branch backdrop panel. The main prefab has no TiltBrush.BackdropPanel/TogglePassthrough persistent call targets.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391686)

   Main: [Assets/Prefabs/Panels/BackdropPanel.prefab:307](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Prefabs/Panels/BackdropPanel.prefab#L307) — Main prefab was inspected: the branch-only passthrough control calls and new mode component are absent.

4. [da4cdf0ec](https://github.com/icosa-foundation/open-brush/commit/da4cdf0ecb7025a875865fed43ddef53c0919771) — Apply multiplayer passthrough guard to direct backdrop changes. **adapt; candidate 13**. Main already blocks preset changes away from passthrough with remote participants, but direct gradient/custom-skybox entry points bypass that admission check and change RenderSettings. This general policy bypass exists independently of dynamic backdrop modes.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391712)

   Main: [Assets/Scripts/SceneSettings.cs:994](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L994) — Existing main policy checks desired preset isPassthrough and remote room participants.; [Assets/Scripts/SceneSettings.cs:197](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L197) — Setter can call TransitionToGradient without multiplayer policy check.; [Assets/Scripts/SceneSettings.cs:220](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L220) — Direct API/background loading has no equivalent check.; [Assets/Scripts/SceneSettings.cs:647](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L647) — Gradient directly replaces the backdrop even while the preset remains passthrough.

5. [21f73353b](https://github.com/icosa-foundation/open-brush/commit/21f73353b99e485bddbe37bc371c85ce0e874961) — Notify backdrop UI from authoritative mode setters. **feature-only**. Emits branch-only BackdropModeChanged from dynamic passthrough/gradient setters. Main has no this event/mode panel, and existing gradient listeners already receive GradientActiveChanged.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391731)

   Main: [Assets/Scripts/SceneSettings.cs:215](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L215) — Main notifies its existing gradient observers; new mode observer does not exist.

6. [864e253a8](https://github.com/icosa-foundation/open-brush/commit/864e253a8336cd41d1506b1a97c0161df184826c) — Restore fog and skybox rendering after passthrough. **feature-only**. Restores fog/camera clears for newly introduced mutable passthrough state. Main passthrough is an environment prefab/preset, with no PassthroughEnabled setter or dynamic flag to restore.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391735)

   Main: [Assets/Scripts/SceneSettings.cs:629](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L629) — Main changes passthrough through environment creation/transition, not the branch dynamic setter.; [Assets/Scripts/SceneSettings.cs:794](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L794) — Main applies preset fog state; no dynamic flag exists to gate here.

7. [4ef2f1da7](https://github.com/icosa-foundation/open-brush/commit/4ef2f1da7dd4637347fa690952c961e74c88fb74) — Apply saved passthrough after creating environment geometry. **feature-only**. Defers a saved CustomEnvironment.PassthroughEnabled flag until new room geometry exists; that metadata field and dynamic setter are absent from main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744)

   Main: [Assets/Scripts/SceneSettings.cs:507](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L507) — Main restores gradient/fog/reflection customization, without a saved dynamic passthrough boolean.

8. [c4d2c2f12](https://github.com/icosa-foundation/open-brush/commit/c4d2c2f12b4e48e9c0015d6885352cca9ffa01ce) — Handle passthrough in environments without room geometry. **feature-only**. Makes the newly added dynamic passthrough setter tolerate geometry-less presets and create a holder. Main has no such setter, so cannot dereference room geometry on that path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744)

   Main: [Assets/Scripts/SceneSettings.cs:869](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L869) — Main checks Resources.Load result before instantiating geometry; dynamic OVR holder creation is absent.

9. [3d26cf1d7](https://github.com/icosa-foundation/open-brush/commit/3d26cf1d789fb9bc497b5a5bcfaf60a63b1dd03a) — Parent empty passthrough holders with environment geometry. **feature-only**. Corrects parent of the feature dynamic empty-room holder. Main real environment geometry already uses the environment transform.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1118#discussion_r3996391744)

   Main: [Assets/Scripts/SceneSettings.cs:882](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L882) — Main environment geometry already has the intended parent; no empty dynamic holder exists.

10. [e2771d721](https://github.com/icosa-foundation/open-brush/commit/e2771d721ecbfdb1c2f7bb149dd7374edc9c71a2) — Restore passthrough render state before loading custom skyboxes. **feature-only**. Routes custom skybox loading through branch-added dynamic PassthroughEnabled setter. Main does not set the backing flag or possess that setter, so this restoration path cannot be ported directly; the independent multiplayer admission issue is separately adapted from da4cdf0ec.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/SceneSettings.cs:220](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L220) — Main starts by storing filename/version; it has no direct m_PassthroughEnabled assignment to replace.

### 4 feature/editable-platform-settings

1. [10876b9dd](https://github.com/icosa-foundation/open-brush/commit/10876b9dd681d25fcba381629533461f39e4311d) — Reject non-finite positive performance overrides. **feature-only**. Both corrected validation overloads belong to PerformanceConfig, introduced by the platform-overrides branch; that struct, its nullable overrides and TestPerformanceConfig do not exist on main.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/UserConfig.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UserConfig.cs#Lnull) — Main has no PerformanceConfig, PerformanceOverrides or ValidatePositive overloads from this patch.; [Assets/Editor/Tests/TestPerformanceConfig.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Editor/Tests/TestPerformanceConfig.cs#Lnull) — Absent from main tree.

2. [a12356f73](https://github.com/icosa-foundation/open-brush/commit/a12356f73bb574b6a8c7b79c5a5810060ca99d94) — Restore multicam API preview using performance overrides. **feature-only**. The correction restores the feature branch's effective user override; main has no PerformanceOverrides and consistently uses PlatformConfig.EnableMulticamPreview.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Tools/MultiCamTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/MultiCamTool.cs#Lnull) — Current main has no effective-performance-override accessor; importing this replacement would not compile without the new feature.

### 5 feature/fix-media-import-paths

1. [be7a2c081](https://github.com/icosa-foundation/open-brush/commit/be7a2c0816d6abaffcf16fb9ded242f19a553397) — Constrain relative media resolution to its configured root. **feature-only**. Constrains branch-added multi-root ResolveMediaPath. Main has no that resolver; existing skybox/API relative imports already call GetSafeRelativePathInDirectory.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571368)

   Main: [Assets/Scripts/SceneSettings.cs:226](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneSettings.cs#L226) — Main custom skybox loading already uses its contained relative-path helper; no multi-root resolver is present.

2. [b1e3285b3](https://github.com/icosa-foundation/open-brush/commit/b1e3285b32e7b84d49e517c8eb5f4c2e250d29bd) — Require a directory boundary when matching model roots. **defer**. Main has the same bare StartsWith root matcher, but only fixed built-in Models/Blocks roots and catalog navigation/scanning constrained to those roots were verified. No reachable sibling-prefix input was proved. The exact patch also fails to accept equality with a root directory, so cannot be imported as-is.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571409)

   Main: [Assets/Scripts/ModelCatalog.cs:110](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelCatalog.cs#L110) — Main matcher uses StartsWith; GetModelDirectories returns only built-in Models/Blocks.; [Assets/Scripts/ModelCatalog.cs:338](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelCatalog.cs#L338) — Called during directory enumeration; root equality must remain valid for Blocks root flattening.

3. [5b2a87ccb](https://github.com/icosa-foundation/open-brush/commit/5b2a87ccb50976fc08fe65c861ca9be27186fed7) — Keep temporary media import review notes local. **already-fixed**. Only removes a temporary branch review document; MEDIA_IMPORT_PATHS_BRANCH_REVIEW.md is absent from the verified main tree.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1121#discussion_r3673571429)

   Main: [AGENTS.md:3](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/AGENTS.md#L3) — Main policy and tree already keep this branch planning artifact out of version control.

4. [599adf41e](https://github.com/icosa-foundation/open-brush/commit/599adf41e2eba490ed294036f6b0e81730dfc14b) — Save image paths relative to their configured media root. **feature-only**. Makes image save metadata relative to configured secondary image roots. Main imports/catalogs ordinary images under one fixed ReferenceImagePath and has no configured extra roots; its same substring is not sufficient evidence of the secondary-root failure.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/ReferenceImage.cs:145](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ReferenceImage.cs#L145) — Main uses default-image-root relative serialization.; [Assets/Scripts/App.cs:2227](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/App.cs#L2227) — Main returns the single Media Library/Images path; no GetAllImageRoots/MediaPathComparison contract exists.

5. [ad2036316](https://github.com/icosa-foundation/open-brush/commit/ad203631697e11fdcc79c0343ecaa650ddf56a90) — Retain model root ownership when duplicate paths are skipped. **feature-only**. Although matching dictionary lines exist, main resolves local model paths Blocks-first, unlike the configurable branch default-first policy. A retained Model stores only relative identity, not first physical root. Last Blocks ownership can match main resolution; keeping first default ownership would instead mislabel a Blocks-resolved model.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/ModelCatalog.cs:386](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelCatalog.cs#L386) — Shared overwrite exists but cannot be judged without model resolver priority.; [Assets/Scripts/Model.cs:93](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Model.cs#L93) — Main deliberately checks existing Blocks path before default Models; the proposed first-owner patch does not match that priority.

### 6 feature/gltf-export-profiles

1. [cfb56c949](https://github.com/icosa-foundation/open-brush/commit/cfb56c949b475982049293bc709e07c8cb2df609) — Isolate static GLB export failures like other formats. **feature-only**. Wraps the branch-only static-glb export. Main already wraps its existing newglb path in RunExport; no static export invocation exists.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3992531425)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

2. [b52459117](https://github.com/icosa-foundation/open-brush/commit/b5245911725b0a35d561fc8f51f7a3f7d3eeb5d1) — Preserve undeformed meshes in OpenBrush GLB exports. **defer**. Removes deformation only for the branch OpenBrush profile. Main has legacy ProcessMesh calls but no matching profile/import restoration contract; shared baking calls do not prove that globally removing baking is safe.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3980017909)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

3. [2e16d57ed](https://github.com/icosa-foundation/open-brush/commit/2e16d57ed909d42e2223521d06c5edd1fdc2f346) — Retain batch material overrides during static GLB export. **feature-only**. Fixes a static ProcessBrushMesh material argument and static-only renderer material replacement, neither present on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3914877048)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

4. [b13ad9325](https://github.com/icosa-foundation/open-brush/commit/b13ad932543fadeb9a27fc0833aa363428fec189) — Export linear additive stroke colors and sub-unit emission gains. **feature-only**. Corrects linear additive color and subunit gains in branch-created emissive clones/atlases; main has no such clone pipeline.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3822139203), [review 2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860868)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

5. [54cd125ea](https://github.com/icosa-foundation/open-brush/commit/54cd125ea62e82c4175f1160a49d8fc8c549c8e5) — Remap batch timestamps through static mesh vertex expansion. **feature-only**. Remaps timestamps through branch static vertex expansion. Main legacy ProcessMesh keeps vertex count and has no source-index expansion mapping.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860875)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.; [Assets/Scripts/Export/BrushBaker.cs:160](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/BrushBaker.cs#L160) — Main legacy ProcessMesh writes the same-count vertex buffer, not expanded static topology.

6. [9100836b9](https://github.com/icosa-foundation/open-brush/commit/9100836b93b4ea6f4e7e889d20618a4b359dc5ce) — Retain stored particle billboard rotation in static bakes. **feature-only**. Rotates four branch-only Static compute kernels. Main legacy kernels differ; the Static directory and these patched kernels do not exist.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3914877055)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

7. [6a532997d](https://github.com/icosa-foundation/open-brush/commit/6a532997d186972ff14cfc22306a655acd549a4a) — Freeze shader time and neutralize audio inputs during texture bakes. **feature-only**. Freezes shader time/audio around a branch-added render-texture UV bake absent from main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112368)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

8. [1889a761d](https://github.com/icosa-foundation/open-brush/commit/1889a761d0af2e628f1c651183b687f2d3384079) — Preserve repeated UV continuity and filtering gutters in emissive atlases. **feature-only**. Fixes repeated UV interpolation and gutters in branch-only tinted emissive atlases, not a main texture export operation.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112364), [review 2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201345)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

9. [27c1725fb](https://github.com/icosa-foundation/open-brush/commit/27c1725fb363273f12c857bfe0f7d24117c1251e) — Bake procedural additive shader output into emissive textures. **feature-only**. Bakes procedural additive shaders and updates clone texture sources only in the absent static profile pipeline.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3711377578), [review 2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794441), [review 3](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794448)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

10. [3a1dda65f](https://github.com/icosa-foundation/open-brush/commit/3a1dda65fb3c084b27e0d6b31ea9cd46d8829d88) — Consume baked particle colors and bubble highlights in emissive atlases. **feature-only**. Consumes baked lifetime/highlight colors in the static atlas; main has neither the atlas nor static particle color output.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112365), [review 2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201350), [review 3](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201351)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

11. [7a2478b1e](https://github.com/icosa-foundation/open-brush/commit/7a2478b1e69f2a0598fcbacceff578cc6d4729e8) — Bake Snow lifetime opacity and Embers sparkle colors at export time. **feature-only**. Edits Static Snow/Embers kernels and staticComputeShaders prefab entries. Main prefab contains only legacy computeShaders, so shared prefab path is not the same bug.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653112365), [review 2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201351)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.; [Assets/Prefabs/BrushBaker.prefab:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Prefabs/BrushBaker.prefab#Lnull) — Main prefab has no staticComputeShaders collection; edited branch entries belong to that added collection.

12. [7457be370](https://github.com/icosa-foundation/open-brush/commit/7457be370895bdff44e4509105514952560be199) — Provide core alpha coverage approximation for static additive materials. **feature-only**. Implements core-glTF coverage approximation in the new static atlas pipeline. Main uses its existing additive extension contract instead.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3653201350), [review 2](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3711377578), [review 3](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3822139203), [review 4](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3876496156), [review 5](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3982860868)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

13. [62ee6e60b](https://github.com/icosa-foundation/open-brush/commit/62ee6e60b9de0f4dc1b10c387477b3ef5695724b) — Preserve atlas alpha coverage with UnityGLTF texture export settings. **feature-only**. Preserves alpha for the feature coverage atlas via BaseColor export; main has no coverage atlas to lose alpha.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3876496156)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

14. [5a57a5253](https://github.com/icosa-foundation/open-brush/commit/5a57a5253cfc8c393b3e9c89ac136af9ad9c5fd6) — Retain stroke-dependent ChromaticWave line phases in static atlases. **feature-only**. Adds ChromaticWave stroke-dependent phases to a branch static atlas, not a main runtime shader correction.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1117#discussion_r3959794448)

   Main: [Assets/Scripts/Export/Export.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/Export.cs#L324) — Existing newglb export is wrapped by RunExport; no static-glb profile call.; [Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs:249](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/UnityGLTF%20Plugins/OpenBrushExportPlugin.cs#L249) — Legacy ProcessMesh is called directly here and at285; no static ProcessBrushMesh selector or tinted emissive-atlas pipeline.

### 7 feature/icosa-collections

1. [972f6b97f](https://github.com/icosa-foundation/open-brush/commit/972f6b97f3d0c54969e4b865d1cb09f93380fcd9) — Refresh selected collections when forced or their query changes. **adapt; candidate 9**. Collection refresh/query behavior is feature-only, but main RequestForcedRefresh also incorrectly StopCoroutine-cancels a manually MoveNext-pumped iterator, bypassing disposal of its nested request enumerator.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Poly/IcosaAssetCatalog.cs:653](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L653) — RequestForcedRefresh calls StopCoroutine on metadata IEnumerator then discards it.; [Assets/Scripts/Poly/IcosaAssetCatalog.cs:1013](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L1013) — Update pumps the iterator with MoveNext; it is not a registered Unity coroutine.; [Assets/Scripts/Poly/IcosaAssetCatalog.cs:1474](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L1474) — RefreshAssetSet owns NextPage enumerator inside using; disposing the outer iterator unwinds its request lifetime.

2. [f6f38b57c](https://github.com/icosa-foundation/open-brush/commit/f6f38b57cf1c45b792959efb2ed3e391e7378fb1) — Clear recycled asset models when showing collection tiles. **feature-only**. Clears retained model state when buttons become collection tiles. Main has no collection tile mode or SetPresetCollection caller.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/IcosaModelButton.cs:21](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/IcosaModelButton.cs#L21) — Exact main file exists but contains no SetPresetCollection or collection tile mode.; [Assets/Scripts/Poly/IcosaAssetCatalog.cs:647](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Poly/IcosaAssetCatalog.cs#L647) — RequestForcedRefresh exists, but SelectCollection and selected collection AssetSet do not.

### 8 feature/inspector

1. [041df13a2](https://github.com/icosa-foundation/open-brush/commit/041df13a2493a47a73c8a0a6e061d93bb8aefb38) — Initialize inspector tabs safely before inactive selection notifications. **feature-only**. Fixes inactive inspector tab initialization/selection notifications; main has no inspector panel or tab classes.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/InspectorPanel.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/InspectorPanel.cs#Lnull) — Inspector panel/tabs absent from exact main tree.

2. [7e3ecc069](https://github.com/icosa-foundation/open-brush/commit/7e3ecc0697997e05ae4f5ddb353517573dba07bc) — Convert absolute inspector edits to relative selection transforms. **feature-only**. Converts inspector absolute fields to TransformSelectionCommand deltas. Main has the shared command but no faulty inspector caller.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/InspectorPanel.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/InspectorPanel.cs#Lnull) — Inspector panel/tabs absent from exact main tree.

3. [d3f916bcc](https://github.com/icosa-foundation/open-brush/commit/d3f916bccad84d102e4fd450db5f3703d0d9afb1) — Interpolate inspector counts for multi-widget selections. **feature-only**. Interpolates inspector-only selection summary labels absent from main.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/InspectorPanel.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/InspectorPanel.cs#Lnull) — Inspector panel/tabs absent from exact main tree.

4. [bda3296e4](https://github.com/icosa-foundation/open-brush/commit/bda3296e4a5a513739387c16a91b2abdfdecc8d3) — Generate finite model UVs and release compute buffers on failure. **feature-only**. Fixes finite UV mapping and compute-buffer cleanup in branch-only GenerateModelUV. Exact main has neither this standalone component nor its compute shader.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GenerateModelUV.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GenerateModelUV.cs#Lnull) — Absent from exact main tree.; [Assets/Shaders/Compute/GenerateModelUV.compute:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Shaders/Compute/GenerateModelUV.compute#Lnull) — Absent from exact main tree.

5. [aaf39f8cc](https://github.com/icosa-foundation/open-brush/commit/aaf39f8ccabc963dbabb2316c6b190236962f1d4) — Remove whitespace from UV generation buffer cleanup. **feature-only**. Whitespace-only follow-up in the absent GenerateModelUV component; no behavioral main patch.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GenerateModelUV.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GenerateModelUV.cs#Lnull) — Absent from exact main tree.; [Assets/Shaders/Compute/GenerateModelUV.compute:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Shaders/Compute/GenerateModelUV.compute#Lnull) — Absent from exact main tree.

### 9 feature/jitter-plugins

1. [51b62f33e](https://github.com/icosa-foundation/open-brush/commit/51b62f33eed29708f3f431fa9dcf9a1dc32039d8) — Reject unknown jitter script names before changing activation. **feature-only**. Validates the JitterScript activation endpoint. Main has no JitterScript category, enable state or endpoint; generic script selection is not the same bug.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/API/Lua/LuaManager.cs:42](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L42) — LuaApiCategory contains PointerScript, ToolScript, SymmetryScript, BackgroundScript. JitterScript only appears in a future-work comment at48.

### 10 feature/mp-stroke-streaming

1. [b6550cd78](https://github.com/icosa-foundation/open-brush/commit/b6550cd78d6e06b9af50fa0d121568988ea67f05) — Restore a finite sketch clock after timestamp quick-load. **adapt; candidate 11**. Main quick-load sets a float.MaxValue clock sentinel, while timestamp-mode completion skips restoration. Main also casts the sentinel to int milliseconds, so port needs bounded conversion as well as completion reset.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310361)

   Main: [Assets/Scripts/Playback/ScenePlaybackByTime.cs:219](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Playback/ScenePlaybackByTime.cs#L219) — QuickLoadRemaining sets CurrentSketchTime=float.MaxValue.; [Assets/Scripts/App.cs:2321](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/App.cs#L2321) — Timestamp-mode OnPlaybackComplete skips clock restoration.; [Assets/Scripts/Playback/ScenePlaybackByTime.cs:104](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Playback/ScenePlaybackByTime.cs#L104) — Update directly converts CurrentSketchTime*1000 to int, requiring explicit saturation/sentinel handling.; [Assets/Scripts/SketchMemoryScript.cs:1443](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L1443) — New stroke timestamps cast CurrentSketchTime*1000 to uint.

2. [6f83010fb](https://github.com/icosa-foundation/open-brush/commit/6f83010fb274fa4d5cb3f642e402bb877c9cf33a) — Start timestamp and real-time playback safely for empty sketches. **adapt; candidate 10**. Main timestamp startup calls GetEarliestTimestamp on empty memory and that helper explicitly throws. Adapt only the timestamp guard, not branch RealTime fallback.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4177310368)

   Main: [Assets/Scripts/SketchMemoryScript.cs:1154](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L1154) — Timestamp startup unconditionally calls GetEarliestTimestamp.; [Assets/Scripts/SketchMemoryScript.cs:525](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L525) — GetEarliestTimestamp explicitly throws if m_MemoryList empty.

3. [8cbd6c071](https://github.com/icosa-foundation/open-brush/commit/8cbd6c071b08fac362d88d6cbb7ec3c6f81d79da) — Resolve real-time timeline sessions for strokes arriving during playback. **feature-only**. Resolves branch real-time wall-clock session dictionary misses for arriving strokes. Main has only ordinary sketch timestamp playback and no session dictionary.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4184371124)

   Main: [Assets/Scripts/Multiplayer/MultiplayerManager.cs:593](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/MultiplayerManager.cs#L593) — Forwards completed commands to backend; no live stroke capability-discovery, streaming sessions or negotiated voice-policy state.

4. [c711c1571](https://github.com/icosa-foundation/open-brush/commit/c711c15714b6124ba1bf79123a00d85d8c866d38) — Keep multiplayer streaming planning documents untracked. **already-fixed**. Deletes two temporary branch plans that are already absent from exact main; no application fix to port.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4006215350)

   Main: [LIVE-MULTIPLAYER-STROKE-STREAMING-PLAN.md:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/LIVE-MULTIPLAYER-STROKE-STREAMING-PLAN.md#Lnull) — Already absent from exact main.; [MULTIPLAYER-STROKE-CLOCK-COMPATIBILITY-PLAN.md:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/MULTIPLAYER-STROKE-CLOCK-COMPATIBILITY-PLAN.md#Lnull) — Already absent from exact main.

5. [9b329fecd](https://github.com/icosa-foundation/open-brush/commit/9b329fecde383561d1b94e556a1a629403d29d74) — Snapshot scene stroke membership before late-join capability discovery. **feature-only**. Snapshots before branch capability-discovery delay. Main immediately starts sync and already creates strokeList.ToList before its serialization await; absent delay/live-delta protocol is the source of the branch race.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r3996228080)

   Main: [Assets/Scripts/Multiplayer/MultiplayerManager.cs:593](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/MultiplayerManager.cs#L593) — Forwards completed commands to backend; no live stroke capability-discovery, streaming sessions or negotiated voice-policy state.; [Assets/Scripts/Multiplayer/MultiplayerSceneSync.cs:90](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/MultiplayerSceneSync.cs#L90) — Main already copies strokes.ToList before first serialization await at99; no capability discovery wait precedes it.

6. [e819f2e96](https://github.com/icosa-foundation/open-brush/commit/e819f2e96295f8e232a75c3350cd5d0107ee2c82) — Complete outgoing live strokes after synchronous simplification finishes. **already-fixed**. Main backend PhotonManager.PerformCommand already awaits Task.Yield before ProcessCommand, so synchronous simplification finishes before serialization. The new outer delay serves branch live-stream completion logic.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4057218855)

   Main: [Assets/Scripts/Multiplayer/MultiplayerManager.cs:597](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/MultiplayerManager.cs#L597) — Main awaits backend PerformCommand.; [Assets/Scripts/Multiplayer/Photon/PhotonManager.cs:316](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/Photon/PhotonManager.cs#L316) — Backend already yields before ProcessCommand serialization.

7. [159eba378](https://github.com/icosa-foundation/open-brush/commit/159eba37801e56cadb46a2a3e92cce4238929458) — Recompute owner voice policy after the authoritative data-room join. **feature-only**. Recomputes branch local/room voice policy after data-room ownership discovery. Main has none of that policy state; unrelated join error handling is a different bug.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r3996228079)

   Main: [Assets/Scripts/Multiplayer/MultiplayerManager.cs:593](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/MultiplayerManager.cs#L593) — Forwards completed commands to backend; no live stroke capability-discovery, streaming sessions or negotiated voice-policy state.

8. [6b4c605f4](https://github.com/icosa-foundation/open-brush/commit/6b4c605f4dfb8634d8917e89a680d8449efbdf0b) — Restore legacy-owner voice after capability timeout without overriding modern policy. **feature-only**. Restores compatibility between branch negotiated room voice policy and legacy owners; main does not negotiate this policy or wait fail-closed for it.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1137#discussion_r4006215363)

   Main: [Assets/Scripts/Multiplayer/MultiplayerManager.cs:593](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Multiplayer/MultiplayerManager.cs#L593) — Forwards completed commands to backend; no live stroke capability-discovery, streaming sessions or negotiated voice-policy state.

### 11 feature/multipath-toolscript-previews

1. [cfde2400d](https://github.com/icosa-foundation/open-brush/commit/cfde2400dd7db45b6382112c558396f496ca9310) — Preview tool script symmetry copies and preserve their source colors. **adapt; candidate 12**. Multipath preview storage is feature-only, but existing main commit-time symmetry expansion passes unexpanded path colors and transformed scales as source pressure. Adapt those proven attributes without importing multipath preview state.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/API/Lua/LuaManager.cs:1532](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L1532) — Main expands each source path across symmetries, but passes original color list to expanded paths at1552.; [Assets/Scripts/API/DrawStrokes.cs:47](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/DrawStrokes.cs#L47) — DrawNestedTrList indexes colors by expanded path index; two copies of first path incorrectly consume second path color.; [Assets/Scripts/API/Lua/LuaManager.cs:1538](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L1538) — sym * baseTransform * point carries transform scale into point.scale, which DrawStrokes line62 interprets as control-point pressure.; [Assets/Scripts/API/DrawStrokes.cs:108](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/DrawStrokes.cs#L108) — Brush scale is a distinct field; main no-symmetry path applies base transform via Stroke.Recreate.

### 12 feature/native-imm

1. [ed78f17b1](https://github.com/icosa-foundation/open-brush/commit/ed78f17b16436ccea66aa9e09b6945fa15115179) — Show native Quill import only when an IMM source file exists. **feature-only**. Restricts the native IMM import button to IMM sources. Main Quill panel has no such native button/import operation.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4264820507)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

2. [8638b4eeb](https://github.com/icosa-foundation/open-brush/commit/8638b4eeb846fa33dd9f590e19df6137cfa8dbef) — Unload native IMM before restoring a replacement sketch environment. **feature-only**. Unloads native IMM documents before environment replacement; main has no NativeImmImport document state.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053283)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

3. [3790d6788](https://github.com/icosa-foundation/open-brush/commit/3790d6788954f786a4a4ceb269201a2b1d88c1bc) — Bound whole-sketch native IMM loading to a two-minute deadline. **feature-only**. Times out the branch native whole-sketch document wait; main has no corresponding native loading loop.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541402)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

4. [18ede7cd9](https://github.com/icosa-foundation/open-brush/commit/18ede7cd9bee7cf40359b290b2fa7883d98a2b97) — Track explicit native playback edits with snapshot-aware sketch save revisions. **adapt; candidate 1**. Native revision machinery is feature-only, but its independent save-success/full-save guard fixes main clearing dirty state after failed writes and SaveSelected.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4270053294), [review 2](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541416)

   Main: [Assets/Scripts/Save/SaveLoadScript.cs:549](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L549) — After writeFuture completes, error is stored; lines551-552 unconditionally mark sketch clean and reset initial pose.; [Assets/Scripts/Save/SaveLoadScript.cs:463](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L463) — SaveSelected calls SaveLow(... selectedOnly:true), reaching ThreadedSave.; [Assets/Scripts/SketchMemoryScript.cs:288](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L288) — SetLastOperationStackCount stores the count used by dirty comparison at332.

5. [57bd906bc](https://github.com/icosa-foundation/open-brush/commit/57bd906bc6f13ac6a090bcc1bf2dac90dcef0178) — Preserve saved native IMM stop timestamps during playback restoration. **feature-only**. Preserves saved native stopTime in RequestSetTime restore calls; both native request and saved playback-state representation are absent on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4306910297)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

6. [194c90c8f](https://github.com/icosa-foundation/open-brush/commit/194c90c8fe001bd01171e3e763caa73db611f24b) — Reject incomplete native viewpoint snapshot retries. **feature-only**. Checks native spawn-area snapshot retry success. Main has no native spawn-area query/retry.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4335695143)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

7. [fb1145d51](https://github.com/icosa-foundation/open-brush/commit/fb1145d51fafd4c111dd2eb67fad9f6334acac9b) — Undo model viewpoint resizing and scene movement as one operation. **feature-only**. Groups branch model viewpoint resizing and scene navigation undo; main has no such viewpoint operation despite shared generic widget transforms.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4342145641)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

8. [270259a79](https://github.com/icosa-foundation/open-brush/commit/270259a79132d5bd3f5ab681f625ffb0fef25d51) — Wrap playable animation sample times and rewind completed clips on Play. **defer**. Main has a potentially faulty nonlegacy single-clip playable loop, but the current importer explicitly forces AnimationMethod.Legacy. No production nonlegacy main trigger was established; branch seek/restart APIs are also absent.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4303764969), [review 2](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4335695127)

   Main: [Assets/Scripts/PlayGltfAnimationClip.cs:140](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/PlayGltfAnimationClip.cs#L140) — Local AnimationClipPlayable is assigned infinite duration at148 without explicit sample wrap; main lacks branch pause/seek/restart controls.; [Assets/Scripts/ImportGltfast.cs:98](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ImportGltfast.cs#L98) — Current importer forces AnimationMethod.Legacy, then calls PlayAnimation at139.

9. [5191ba18a](https://github.com/icosa-foundation/open-brush/commit/5191ba18a7c7d4ed7446c146f5c6146c8d9afac1) — Register native IMM rendering for screenshot and capture cameras. **feature-only**. Registers native IMM rendering on screenshot/capture cameras. Main has capture cameras but no NativeImmRenderer or ImmPlayerManager rendering to omit.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4276887936)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

10. [7a48bd71a](https://github.com/icosa-foundation/open-brush/commit/7a48bd71a3fce6ee5c9374f0497eaaafa40fc3b3) — Persist IMM gallery model format and restore through the IMM-only loader. **feature-only**. Persists explicit native IMM representation of mixed gallery models. Main has no native-model flag, playback states or IMM-only model loader.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541391)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

11. [7a2e48a4c](https://github.com/icosa-foundation/open-brush/commit/7a2e48a4c4a9f838c5e0f612f896ce75a73218aa) — Preserve pending native document unloads when a waiting import is canceled. **feature-only**. Merges pending native document unload IDs when canceling imports. Main has no native document ID/unload state.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1192#discussion_r4296541402)

   Main: [Assets/Scripts/NativeImmImport.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/NativeImmImport.cs#Lnull) — No native document manager in exact main.; [Assets/Scripts/ModelPlaybackController.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/ModelPlaybackController.cs#Lnull) — No native/unified model playback controller, snapshots, restore or viewpoint operations.

### 13 feature/polling-stroke-listener

1. [646b88d3e](https://github.com/icosa-foundation/open-brush/commit/646b88d3ee2d9dd9ccefdee7b6915cca72318d2d) — Decode polling POST command values once. **adapt; candidate 8**. Main decodes the entire POST form before splitting on ampersands and decodes values again in EnqueueCommand; encoded delimiters/plus/nested escapes are already corrupted independently of polling.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1025#discussion_r4006159104)

   Main: [Assets/Scripts/API/ApiManager.cs:669](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L669) — Global Uri.UnescapeDataString occurs before formdata.Replace(+)/Split(&).; [Assets/Scripts/API/ApiManager.cs:227](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L227) — EnqueueCommand calls UnityWebRequest.UnEscapeURL again on parameter value.

### 14 feature/sculpting

1. [5401a4979](https://github.com/icosa-foundation/open-brush/commit/5401a4979e5ea8199b99a95b3d9fb55dc08bba7c) — Validate sculpted vertex and normal payloads before saving. **feature-only**. Validates branch-only SculptedGeometryData vertex/normal arrays. Main serializes regenerated control-point strokes and has no such binary extension or payload.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552121), [review 2](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552567)

   Main: [Assets/Scripts/Save/SketchWriter.cs:89](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L89) — Main AdjustedMemoryBrushStroke contains layerIndex, strokeData and adjustedStrokeFlags only, no SculptedGeometryData; main StrokeExtension lines68–79 has no sculpt geometry payload.

2. [04699457d](https://github.com/icosa-foundation/open-brush/commit/04699457d293bca54735c7b129842b6519485e02) — Recover safely from missing or mismatched sculpt geometry. **feature-only**. Guards a branch-only saved-sculpt queue and InsertSculptedGeometry restoration. Neither field nor method exists on main; no malformed persisted sculpt record can reach this path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552194), [review 2](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552220)

   Main: [Assets/Scripts/Save/SketchWriter.cs:89](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L89) — Main AdjustedMemoryBrushStroke contains layerIndex, strokeData and adjustedStrokeFlags only, no SculptedGeometryData; main StrokeExtension lines68–79 has no sculpt geometry payload.; [Assets/Scripts/SketchMemoryScript.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#Lnull) — Exact-main git show contains no m_SavedSculptedGeometry, ReapplySculptedGeometry or InsertSculptedGeometry symbols.

3. [31da0901a](https://github.com/icosa-foundation/open-brush/commit/31da0901a655926bf71306f3130cfcbb40b70cbe) — Retain edited-mesh state only when sculpt geometry is reapplied. **feature-only**. Preserves branch-only m_MeshIsEdited state through recoloring/rebrushing and calls branch-only InsertSculptedGeometry. Main RepaintStrokeCommand simply recreates strokes from their control points without this bit or sculpt payload.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552308)

   Main: [Assets/Scripts/Commands/RepaintStrokeCommand.cs:61](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/RepaintStrokeCommand.cs#L61) — Main ApplyColorAndBrushToObject lines61–69 updates color/brush/size, invalidates copy, then Uncreate/Recreate; no edited-mesh snapshot/reapply path.; [Assets/Scripts/Save/SketchWriter.cs:89](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L89) — Main AdjustedMemoryBrushStroke contains layerIndex, strokeData and adjustedStrokeFlags only, no SculptedGeometryData; main StrokeExtension lines68–79 has no sculpt geometry payload.

4. [b9870bbd9](https://github.com/icosa-foundation/open-brush/commit/b9870bbd90b1f912ea228940ce935c8eee7f6271) — Encapsulate and guard legacy sculpt subtool selection. **feature-only**. Encapsulates and guards the separate LegacySculpting SculptTool introduced by this branch. Main has ReshapeTool and its own manager, not SculptTool or LegacySculpting. Its SetSubTool already activates the newly selected subtool according to hidden state; absent-reference hardening alone has no demonstrated main user trigger.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552347), [review 2](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552361), [review 3](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552388)

   Main: [Assets/Scripts/Tools/SculptTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/SculptTool.cs#Lnull) — Absent from the pinned main tree.; [Assets/Scripts/Tools/ReshapeTool.cs:120](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/ReshapeTool.cs#L120) — Main SetSubTool lines120–139 switches its own subtool and sets the new gameObject active according to m_ToolHidden; manager uses this ReshapeTool.

5. [ae5e4e1fb](https://github.com/icosa-foundation/open-brush/commit/ae5e4e1fbf527912f755c404ec9e22ed33306f21) — Emit sculpt feedback once per modified stroke. **feature-only**. Moves sound/haptics outside the branch legacy tool vertex loop. Main has no SculptTool.ManipulateVertex loop; ReshapeTool uses its own captured-contact/control-point deformation and feedback path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552416)

   Main: [Assets/Scripts/Tools/SculptTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/SculptTool.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Tools/ReshapeTool.cs:219](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/ReshapeTool.cs#L219) — Main haptics path exists in ReshapeTool, not the patched per-vertex LegacySculpting loop; actual deformation uses CalculateInfluence/CalculateDirection at269/309.

6. [671bd503a](https://github.com/icosa-foundation/open-brush/commit/671bd503a5efe1d68170fe8fd9609af66d02ca65) — Compute sculpt rotation axis without mutating the tool transform. **feature-only**. Avoids per-vertex transform mutation in branch-only LegacySculpting.RotateSubTool. Main has no RotateSubTool; its twist axis is already computed in canvas coordinates without mutating the tool transform.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552464)

   Main: [Assets/Scripts/Tools/Sculpting/RotateSubTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/Sculpting/RotateSubTool.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Tools/ReshapeTool.cs:168](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/ReshapeTool.cs#L168) — Main sets m_TwistAxis from Quaternion.Inverse(canvas.Pose.rotation) * m_ToolTransform.forward, rather than temporarily changing transform.rotation.

7. [4c93c04a1](https://github.com/icosa-foundation/open-brush/commit/4c93c04a19426a20796a29958150e7516cf1a7fd) — Use the Batch geometry property from sculpting code. **already-fixed**. Main already keeps Batch.m_Geometry private and exposes Geometry. This patch repairs the branch-induced public-field change and updates legacy sculpt-only callers; importing it adds no needed main behavior.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552513)

   Main: [Assets/Scripts/Batching/Batch.cs:32](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Batching/Batch.cs#L32) — Main m_Geometry is private; the public read-only Geometry getter is at50.; [Assets/Scripts/Batching/BatchSubset.cs:44](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Batching/BatchSubset.cs#L44) — Main BatchSubset ends without the branch-added RecalculateBounds helper; remaining patch callers use branch sculpt data.

8. [57b5fd7ba](https://github.com/icosa-foundation/open-brush/commit/57b5fd7baa2b9985a54282b517a5c4b72999c982) — Remove unused mesh saver collection imports. **feature-only**. Removes unused imports from the branch-added MeshSaver editor utility; that utility is absent on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552553)

   Main: [Assets/MeshSaver/Editor/MeshSaverEditor.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/MeshSaver/Editor/MeshSaverEditor.cs#Lnull) — Absent from pinned main git tree.

9. [910297acb](https://github.com/icosa-foundation/open-brush/commit/910297acb721dd23098e6ec795f208190c3c1a49) — Use the empty sculpt payload sentinel for zero-vertex geometry. **feature-only**. Corrects zero-length sentinel and restoration validation for branch-only sculpt binary payloads; main writes no such payload.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/857#discussion_r3397552121)

   Main: [Assets/Scripts/Save/SketchWriter.cs:89](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L89) — Main AdjustedMemoryBrushStroke contains layerIndex, strokeData and adjustedStrokeFlags only, no SculptedGeometryData; main StrokeExtension lines68–79 has no sculpt geometry payload.

10. [ef2ae5b16](https://github.com/icosa-foundation/open-brush/commit/ef2ae5b16e60abaf949557d146ecc14233cabf8e) — Resolve current stroke geometry when undoing sculpt edits. **feature-only**. Makes branch-only SculptCommand resolve live mesh subsets after rebatched legacy sculpt edits. Main has no SculptCommand or persisted mesh edit state, so no stale command subset exists to repair.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Commands/SculptCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/SculptCommand.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Save/SketchWriter.cs:89](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L89) — Main AdjustedMemoryBrushStroke contains layerIndex, strokeData and adjustedStrokeFlags only, no SculptedGeometryData; main StrokeExtension lines68–79 has no sculpt geometry payload.

### 15 feature/sdf-guides

1. [0d8356ab7](https://github.com/icosa-foundation/open-brush/commit/0d8356ab77fdc3d52131b02f43da2850989445df) — Invalidate guide distance cache when guides show or hide. **feature-only**. Invalidates the branch-added GlobalStencilSdfCache from shared StencilWidget show/hide callbacks. Main has no cache to become stale; shared callback filename alone does not establish a main bug.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3952875893), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363934)

   Main: [Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GlobalStencilSdfCache.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no cached combined guide signed-distance/union projection implementation.; [Assets/Scripts/Widgets/StencilWidget.cs:185](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/StencilWidget.cs#L185) — Main OnShow refreshes visibility directly; there is no cached combined SDF registry to invalidate.

2. [480fffaea](https://github.com/icosa-foundation/open-brush/commit/480fffaeaf4f1ca30dccbcedc9a49aa2279b8195) — Measure guide distances using the owning canvas and finite plane surface. **feature-only**. Corrects canvas scaling and finite-plane semantics in branch-only combined guide distance evaluator, not the existing analytic guide surface implementations.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069366), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530520)

   Main: [Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GlobalStencilSdfCache.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no cached combined guide signed-distance/union projection implementation.; [Assets/Scripts/WidgetManager.cs:1179](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/WidgetManager.cs#L1179) — Main surface dispatch calls each widget FindClosestPointOnSurface directly, not GlobalStencilSdfCache.DistanceToStencil.

3. [15bd80797](https://github.com/icosa-foundation/open-brush/commit/15bd8079718df3a94c394927412ccf919b2faaf2) — Check projected guide points against the combined volume boundary. **feature-only**. Rechecks a projected point against a branch-only union of guides. Main has no combined volume surface-step endpoint or union evaluator.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804669)

   Main: [Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GlobalStencilSdfCache.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no cached combined guide signed-distance/union projection implementation.

4. [ab1a2223a](https://github.com/icosa-foundation/open-brush/commit/ab1a2223ad012d9ea7f6aded22eb506eef1ebdc1) — Transform HTTP guide queries and explicit SDF poses through scene space. **feature-only**. Corrects coordinates for newly added SDF/guide closest-point, signed-distance and next-surface HTTP endpoints. These endpoint methods and ApiMethods.SdfGuides.cs are absent from main, so no existing main endpoint uses the faulty coordinate path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3959800195), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3959800203)

   Main: [Assets/Scripts/API/ApiMethods.SdfGuides.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.SdfGuides.cs#Lnull) — Absent from pinned main; git grep of main ApiMethods finds no GetGuideClosestPoint/GetGuideSignedDistance/GetActiveGuidesSignedDistance/GetActiveGuidesNextSurfacePoint methods.; [Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GlobalStencilSdfCache.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no cached combined guide signed-distance/union projection implementation.

5. [7da4580f1](https://github.com/icosa-foundation/open-brush/commit/7da4580f1d8a76713dcbd4ae159bb0ac517d5277) — Preserve complete world scales when converting models and SDF guides. **feature-only**. Corrects world scaling only inside newly added model-to-guide and editable-SDF conversion code. Main has neither conversion command nor SdfGuideConversion.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4177451766), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363957)

   Main: [Assets/Scripts/Commands/CreateModelGuideCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/CreateModelGuideCommand.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Widgets/SdfGuideConversion.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfGuideConversion.cs#Lnull) — Absent from pinned main.

6. [7782c8531](https://github.com/icosa-foundation/open-brush/commit/7782c85316a4af44b1042270150d98171c522ced) — Clear empty SDF previews and reject stale conversion meshes. **feature-only**. Clears stale branch SDF generated previews and checks visual revisions before branch conversion. No such generator/revision state exists on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3982852267), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363944)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.

7. [150c7d7b2](https://github.com/icosa-foundation/open-brush/commit/150c7d7b240f1eb4dd45cdd3eae96b6f6e93ece8) — Unregister malformed SDF guides before destroying rejected instances. **feature-only**. Unregisters rejected SdfStencil instances during loading. Main cannot construct SdfStencil and its guide metadata has no Sdf definition. The shared loader is not sufficient evidence for porting the branch-specific rejection branch.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804681)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Main Guides.State has Transform/Extents/Pinned/GroupId/LayerId only (lines465–474), no Sdf, ModelGuide or resize-operand metadata.; [Assets/Scripts/Widgets/StencilWidget.cs:256](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/StencilWidget.cs#L256) — Main instantiates GetStencilPrefab(stencilType), with no editable-SDF prefab selection or malformed-SDF special case.

8. [d2a4a8543](https://github.com/icosa-foundation/open-brush/commit/d2a4a854371667952a5eb4ef7671ee18df760114) — Record API model-to-guide conversions on the undo stack. **feature-only**. Records branch-only model-to-guide conversion as an undo command. Main ModelWidget has no ConvertToStencil method or ModelStencil prefab; importing the new command would introduce the feature.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980434)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/Commands/CreateModelGuideCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/CreateModelGuideCommand.cs#Lnull) — Absent from pinned main; git grep main ModelWidget has no ConvertToStencil.

9. [de0eaf3d9](https://github.com/icosa-foundation/open-brush/commit/de0eaf3d9f989d85f0e96fb5fed33e6c45952372) — Build model guides from selected subtrees and baked skinned geometry. **feature-only**. Builds branch model-guide source snapshots from selected subtrees and baked skinning; no ModelStencil/create-model-guide lifecycle exists on main. Existing model import/subtree functionality is not the modified caller.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3968491517), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3982852278)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/Commands/CreateModelGuideCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/CreateModelGuideCommand.cs#Lnull) — Absent from pinned main.

10. [3444ac134](https://github.com/icosa-foundation/open-brush/commit/3444ac134da5745bb37d971d55ebe415ca81b81a) — Query model guide surfaces and selection bounds from actual geometry. **feature-only**. Replaces branch ModelStencil SDF/collider nearest-point, activation and selection-bound code. Main has no ModelStencil counterpart; do not infer a general ModelWidget collider bug from the shared model concept.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069373), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3955597741), [review 3](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980421), [review 4](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980431), [review 5](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530514)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.

11. [a277aacf7](https://github.com/icosa-foundation/open-brush/commit/a277aacf7212d5b5c326d2a1dfb3beffd10da789) — Replace model-guide placeholders and register actual guide renderers. **feature-only**. Replaces placeholder renderers and registers generated preview renderer tint/highlight state only in branch ModelStencil.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3979980413), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530529)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.

12. [9f8719191](https://github.com/icosa-foundation/open-brush/commit/9f87191915fae83e7b87795f1b75cad2e0cc799b) — Release model-guide runtime SDF assets and retained source meshes. **feature-only**. Releases branch model-guide runtime SDF/source assets and clones samples during SDF conversion; neither owner nor conversion exists on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4005804686)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/Widgets/SdfGuideConversion.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfGuideConversion.cs#Lnull) — Absent from pinned main.

13. [a6e2793a1](https://github.com/icosa-foundation/open-brush/commit/a6e2793a122c2b4e7a871a52106fa25349bc77c7) — Persist and clone model guides with self-contained geometry snapshots. **feature-only**. Adds self-contained model-guide geometry metadata, restoration and cloning. Main guide metadata has no ModelGuide field and no ModelStencil; this is feature persistence, not an existing main save fix.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3914069346), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4057121719)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Main Guides.State has Transform/Extents/Pinned/GroupId/LayerId only (lines465–474), no Sdf, ModelGuide or resize-operand metadata.

14. [2cb07c5fb](https://github.com/icosa-foundation/open-brush/commit/2cb07c5fbf489d0de5d95da3543ff46f7d0dfbe3) — Track forced guide-snapping ownership across all Lua script categories. **feature-only**. Adds ownership cleanup for branch Brush:ForceGuideSnappingOff API. Main BrushApiWrapper and LuaManager have no ForceGuideSnappingOff/IsGuideSnappingForcedOff symbols, so ending a main Lua script cannot strand that nonexistent override.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530541)

   Main: [Assets/Scripts/API/Lua/Wrappers/BrushApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/BrushApiWrapper.cs#Lnull) — Pinned main git show contains no ForceGuideSnappingOff method.; [Assets/Scripts/API/Lua/LuaManager.cs:768](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L768) — Main EndScript exists but has no forced-guide-snapping ownership state to clear.

15. [626a86919](https://github.com/icosa-foundation/open-brush/commit/626a869195849df06ab2799ea69e2d35dc7c98ed) — Record Lua SDF primitive mutations as undoable guide edit commands. **feature-only**. Makes newly added editable SDF primitive Lua operations undoable. Main GuideApiWrapper has no SDF component editing API, SdfPrimitiveApiWrapper and EditSdfGuideCommand are absent.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4057121722)

   Main: [Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Commands/EditSdfGuideCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/EditSdfGuideCommand.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.

16. [be9bfa027](https://github.com/icosa-foundation/open-brush/commit/be9bfa0272c47dfad6c21942cb39f405cc5628c4) — Exclude empty SDF surfaces and require successful current conversion previews. **feature-only**. Checks branch-only empty SDF and successful preview revision state. Main has neither SdfStencil nor GlobalStencilSdfCache or SDF conversion previews.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530502), [review 2](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4199363944)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.; [Assets/Scripts/Widgets/GlobalStencilSdfCache.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GlobalStencilSdfCache.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no cached combined guide signed-distance/union projection implementation.

17. [9d256b8fc](https://github.com/icosa-foundation/open-brush/commit/9d256b8fc1362f90e3125796f0deb840971c59ff) — Reject model guide conversion when the requested subtree is missing. **feature-only**. Rejects missing subtrees inside newly added model-guide conversion. Existing ModelWidget subtree loading is not modified by this patch, and no ModelStencil caller exists on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3968491517)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.

18. [db0ab9428](https://github.com/icosa-foundation/open-brush/commit/db0ab942875716e5045d3309f6b9dc912b5d4be0) — Omit MoonSharp-injected parameters from generated Lua API documentation. **defer**. The shared docs generator does enumerate all reflected parameters on main, but the current registered main wrapper APIs do not expose injected MoonSharp Script/ScriptExecutionContext/CallbackArguments parameters. The motivating ForceGuideSnappingOff(Script, bool) exists only on the SDF branch. No current main documentation failure is proven; retain as hardening for a future injected-parameter API.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r3992530541)

   Main: [Assets/Scripts/API/Lua/LuaDocsRegistration.cs:155](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaDocsRegistration.cs#L155) — Main parameter loop has no injected-type filter, but a current registered signature is needed to demonstrate a user-visible bad entry.; [Assets/Scripts/API/Lua/LuaManager.cs:1014](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/LuaManager.cs#L1014) — RegisterApiClasses lines1014–1073 registers wrapper types; pinned git grep across current wrappers finds no MoonSharp injected parameter declarations.; [Assets/Scripts/API/Lua/Wrappers/BrushApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/BrushApiWrapper.cs#Lnull) — Main lacks ForceGuideSnappingOff(Script, bool), which motivated the extra filtering on the feature branch.

19. [4e7a45a9e](https://github.com/icosa-foundation/open-brush/commit/4e7a45a9e3112e491cf24256320b2a04e390bce7) — Keep HTTP primitive update transforms in guide-local space. **feature-only**. Removes a scene-space conversion from the branch-only per-component SDF update HTTP method; main has no ApiMethods.SdfGuides endpoint file.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106955)

   Main: [Assets/Scripts/API/ApiMethods.SdfGuides.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.SdfGuides.cs#Lnull) — Absent from pinned main.

20. [8f324f161](https://github.com/icosa-foundation/open-brush/commit/8f324f16144622a34d460eb6b19f6550487d9deb) — Allow skinned-only model selections to create guides. **feature-only**. Expands the branch SelectionManager.CanConvertModelToGuide predicate for skinned-only models. Main SelectionManager has no model-guide conversion predicate, command or ModelStencil destination.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106970)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/SelectionManager.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SelectionManager.cs#Lnull) — Pinned main version has no CanConvertModelToGuide model-guide conversion availability path.

21. [d8446dc19](https://github.com/icosa-foundation/open-brush/commit/d8446dc19191f56cad8c36eedb744d5ef5fd764d) — Keep Lua primitive handles bound to stable component identities across edits. **feature-only**. Adds stable component identity across branch SDF edit rebuilds and Lua handles. Main has no SDF component wrapper, edit command or component definition registry.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106961)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.; [Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs#Lnull) — Absent from pinned main.

22. [a4e8fd214](https://github.com/icosa-foundation/open-brush/commit/a4e8fd2146fc83b80189a34528064f9d6d486b27) — Release editable-guide runtime SDF assets after guides and undo snapshots expire. **feature-only**. Introduces reference ownership for runtime SDF mesh assets held by branch guides and undo snapshots. Main has no RuntimeSDFGenerator or SDF edit/conversion commands that allocate/retain these assets.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342106979)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.; [Assets/Scripts/Widgets/RuntimeSDFGenerator.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/RuntimeSDFGenerator.cs#Lnull) — Absent from pinned main.

23. [1ac441c85](https://github.com/icosa-foundation/open-brush/commit/1ac441c8547e9b173083bc6e60d0f2152afba237) — Project stationary SDF queries from bounded directional probes. **feature-only**. Adds bounded directional projection probes for zero-gradient queries in branch SdfStencil. Main analytic guide shapes do not use this IsoMesh mapper or fallback path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342224947)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.

24. [84e547f12](https://github.com/icosa-foundation/open-brush/commit/84e547f1206118bd2c127127b8622a19a6f63df0) — Generate runtime SDFs from restored model guide geometry. **feature-only**. Removes the imported Model object prerequisite when generating an SDF from a restored branch model-guide snapshot. Main supports neither snapshot kind nor ModelStencil.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342224955)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Main Guides.State has Transform/Extents/Pinned/GroupId/LayerId only (lines465–474), no Sdf, ModelGuide or resize-operand metadata.

25. [ae89c0d23](https://github.com/icosa-foundation/open-brush/commit/ae89c0d231a1b153a60a27798ff093ea0823fab4) — Fit SDF model guide broad-phase colliders to retained mesh bounds. **feature-only**. Fits a broad-phase bounds collider specifically for successful-SDF ModelStencil instances. Main ModelStencil is absent, so the placeholder bug has no current main counterpart.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342367798)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.

26. [da9a39fd6](https://github.com/icosa-foundation/open-brush/commit/da9a39fd62003c6d325f684e3202a9d886cf2a43) — Preserve edited SDF component definitions when cloning primitive guides. **feature-only**. Copies edited component definitions when cloning branch SdfPrimitiveStencil. Main Cylinder/other analytic stencils are not this editable component class.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342367800)

   Main: [Assets/Scripts/Widgets/SdfPrimitiveStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfPrimitiveStencil.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Main Guides.State has Transform/Extents/Pinned/GroupId/LayerId only (lines465–474), no Sdf, ModelGuide or resize-operand metadata.

27. [0ce9fa017](https://github.com/icosa-foundation/open-brush/commit/0ce9fa017f022669006e09b6496c3e444f0de22c) — Release restored SDF mesh assets when guide validation fails. **feature-only**. Cleans up assets allocated while restoring malformed saved editable-SDF components. Main has no SDF saved payload/asset constructor or component validation path.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342769319)

   Main: [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Main Guides.State has Transform/Extents/Pinned/GroupId/LayerId only (lines465–474), no Sdf, ModelGuide or resize-operand metadata.

28. [f1f4cd8da](https://github.com/icosa-foundation/open-brush/commit/f1f4cd8da16ba10f4771d97e407fcdfae3fab3ce) — Exclude guides without surface query backends from snapping. **feature-only**. Adds HasSurface availability for branch ModelStencil backends and empty editable SDFs, then filters them in shared snapping. Main has no unavailable model-SDF backend or empty SDF instance to exclude; adding the base property alone fixes no proven main bug.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.; [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.; [Assets/Scripts/WidgetManager.cs:1053](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/WidgetManager.cs#L1053) — Main magnetization queries existing stencil surfaces, without ModelStencil/SdfStencil types or runtime backend availability states.

29. [1c88ce592](https://github.com/icosa-foundation/open-brush/commit/1c88ce59236b298d688273c8a641c647d82883d0) — Revive retained Lua primitive wrappers when deletion is undone. **feature-only**. Allows undo revival of branch-only stable-identity Lua SDF primitive wrappers. Main has neither wrapper nor its deletion/edit command.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997712)

   Main: [Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/SdfPrimitiveApiWrapper.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Widgets/SdfStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no editable SDF components or IsoMesh component snapshots.

30. [43db2f490](https://github.com/icosa-foundation/open-brush/commit/43db2f4907fb661198a6bd77d6a63f9f7f617fc3) — Use model fallback mesh for grab and snapping broad phases. **feature-only**. Disables placeholder and sets GrabCollider only for new ModelStencil mesh fallback. There is no ModelStencil counterpart on main; shared grab collider concepts do not prove the same bug for main analytic stencils.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997696)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.

31. [e22ba8ea0](https://github.com/icosa-foundation/open-brush/commit/e22ba8ea0b24b87c409b3283e54734a9674d93d9) — Release model source when asynchronous preview generation completes. **feature-only**. Replaces a 36-frame poll with completion notification for new ModelStencil SDF previews. Main has no such coroutine/generator/source model replacement lifecycle.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342997705)

   Main: [Assets/Scripts/Widgets/ModelStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelStencil.cs#Lnull) — Absent from git ls-tree -r 5591ff1aad5288903e718752c522d54420ae6d1e; main has no model-guide SDF generation, placeholder replacement, mesh fallback or model-guide preview lifecycle.

32. [16f902779](https://github.com/icosa-foundation/open-brush/commit/16f902779775c8c0587920e9f41a85263dfe9bdc) — Resize only the original standalone guide primitive operand. **feature-only**. Preserves original editable primitive identity while resizing and adds saved operand-role metadata for new SdfPrimitiveStencil. Main has no shared SDF component editor or saved operand role.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1119#discussion_r4342769306)

   Main: [Assets/Scripts/Widgets/SdfPrimitiveStencil.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/SdfPrimitiveStencil.cs#Lnull) — Absent from pinned main.; [Assets/Scripts/Save/SketchMetadata.cs:465](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchMetadata.cs#L465) — Main Guides.State has Transform/Extents/Pinned/GroupId/LayerId only (lines465–474), no Sdf, ModelGuide or resize-operand metadata.

### 16 feature/sky-export

1. [9baf0a5bf](https://github.com/icosa-foundation/open-brush/commit/9baf0a5bfae373ff57ec327ffd035488a631b120) — Release generated sky gradient textures after export. **feature-only**. Releases Texture2D created by branch gradient generation; main simply assigns RenderSettings.skybox and never allocates this texture.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Export/GltfExportStandinManager.cs:34](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/GltfExportStandinManager.cs#L34) — CreateSkyStandin assigns RenderSettings.skybox at37 and leaves the sphere inactive at38; no generated gradient texture/orientation.

2. [ac6a8183d](https://github.com/icosa-foundation/open-brush/commit/ac6a8183d383a89d88b647ae6ffe40641aa161c5) — Orient exported sky gradients with the scene gradient. **feature-only**. Orients branch generated gradient sphere and restores its rotation; main has no baked gradient export/sphere rotation path.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Export/GltfExportStandinManager.cs:34](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/GltfExportStandinManager.cs#L34) — CreateSkyStandin assigns RenderSettings.skybox at37 and leaves the sphere inactive at38; no generated gradient texture/orientation.

### 17 feature/straight-edge-snapping

1. [9c03ea48b](https://github.com/icosa-foundation/open-brush/commit/9c03ea48b6505405e48aa9bdd18a43f03eb9f361) — Exclude hidden and uncreated strokes from endpoint hash registration. **already-fixed**. Main uses a bounded line history rather than this branch's spatial hash, and rejects disabled geometry, missing canvases, off-active-canvas strokes and invisible layers before endpoint evaluation.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/931#discussion_r3656716296)

   Main: [Assets/Scripts/StraightEdgeGuideScript.cs:304](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/StraightEdgeGuideScript.cs#L304) — TryGetEndpointSnap prunes disposed strokes and applies IsGeometryEnabled, active-canvas and IsLayerVisible eligibility checks at lines323-328.

2. [8afa17020](https://github.com/icosa-foundation/open-brush/commit/8afa17020be374f6188e623f12385e6dba025703) — Exclude hidden canvases from straight-edge endpoint snapping. **already-fixed**. Main already excludes hidden layers via IsLayerVisible(canvas) before evaluating recent straight-edge endpoints. QuerySingleCanvas/QueryAllCanvases are branch-specific hash paths.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/StraightEdgeGuideScript.cs:324](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/StraightEdgeGuideScript.cs#L324) — Visibility is explicitly checked before endpoint selection.; [Assets/Scripts/SceneScript.cs:343](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SceneScript.cs#L343) — IsLayerVisible(CanvasScript) returns the canvas GameObject activeSelf state.

### 18 feature/symmetry-stroke-linking

1. [2ee1063d5](https://github.com/icosa-foundation/open-brush/commit/2ee1063d5e877092555378c86ab44a86a421006e) — Keep snapshot deselection out of undo history. **feature-only**. The offending ClearActiveSelection call in snapshot capture was added by symmetry linking. Main's selected-only snapshot already uses direct DeselectStrokes, and ordinary snapshots do not clear/recreate selection through recorded commands.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367734)

   Main: [Assets/Scripts/Save/SketchSnapshot.cs:102](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchSnapshot.cs#L102) — Selected-only path takes SelectedStrokes then calls DeselectStrokes(strokes, App.ActiveCanvas) at lines105-106; no ClearActiveSelection call.

2. [19e3d3ed8](https://github.com/icosa-foundation/open-brush/commit/19e3d3ed8b0924ca5763dd5a8bbe362817d5d8ff) — Link real symmetry strokes after straight-edge proxy replacement. **feature-only**. BeginSymmetryStrokeGroup and the persistent active mirror group exist only on the linked-symmetry branch. Main has straight-edge replacement but no linked-copy ownership to preserve.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367749)

   Main: [Assets/Scripts/PointerManager.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/PointerManager.cs#Lnull) — Main has no BeginSymmetryStrokeGroup/m_ActiveStrokeMirror machinery.

3. [fd2d30bbd](https://github.com/icosa-foundation/open-brush/commit/fd2d30bbdab8ade2b02430d8eeb7c38f1beedbf6) — Keep the temporary symmetry editing plan local. **feature-only**. This commit only untracks a temporary plan on the symmetry branch. The file does not exist in main's Git tree.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4346367757)

   Main: [SYMMETRY-PEER-EDITING-PLAN.md:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/SYMMETRY-PEER-EDITING-PLAN.md#Lnull) — Absent from main.

### 19 feature/text-tool

1. [27c9e785e](https://github.com/icosa-foundation/open-brush/commit/27c9e785e3e21172cc1312d29cc217eff6d44516) — Index text widgets using the active text widget count. **defer**. Main contains the same wrong-image-count text-index helper, but it is private and its only main reference is inside commented-out text.extrude. Existing text.add does not call it. There is no reachable negative-index main endpoint trigger; fix alongside future text operations, not as a proven currently active bug.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558434)

   Main: [Assets/Scripts/API/ApiMethods.Utils.cs:184](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.Utils.cs#L184) — Main _GetActiveTextWidget lines184–187 uses ActiveImageWidgets for normalization.; [Assets/Scripts/API/ApiMethods.cs:1144](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.cs#L1144) — The only reference outside its definition is commented-out text.extrude; main text.add at1115–1135 creates directly.

2. [34c40526b](https://github.com/icosa-foundation/open-brush/commit/34c40526be933421bc3130f5cbe9d3e4402db58b) — Define the serialized text tool enum value. **feature-only**. Adds the serialized TextTool enum required by the new VR text tool. Main has TextWidget/API text.add but no TextTool implementation or prefab route needing the enum.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558454)

   Main: [Assets/Scripts/Tools/TextTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/TextTool.cs#Lnull) — Absent from pinned main; main BaseTool enum has no TextTool value.

3. [d7c60b676](https://github.com/icosa-foundation/open-brush/commit/d7c60b6760ea5e0f35ec4494d5e13d570f4bcfdd) — Close keyboard sessions on panel hide and ignore key release events. **feature-only**. Repairs session close/key-release handling in the newly added KeyboardPanel used by the TextTool. Main KeyboardPanel is absent; existing keyboard popup/UltimateXR UI are different callers.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558358), [review 2](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558379)

   Main: [Assets/Scripts/GUI/KeyboardPanel.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/KeyboardPanel.cs#Lnull) — Absent from pinned main; git tree instead contains KeyboardPopUpWindow/KeyboardUI and other keyboard components.

4. [8011bddae](https://github.com/icosa-foundation/open-brush/commit/8011bddae0b771660f049ec31631e458973dfbb3) — Remove exporter debug logging and name the output directory argument. **feature-only**. Removes branch-added debug logging and names the branch-added outputDirectory argument for text raster export. Main ExportGlTF has neither that log nor argument, so no cleanup is needed there.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558190)

   Main: [Assets/Scripts/Export/ExportGlTF.cs:97](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/ExportGlTF.cs#L97) — Main GetExportPayload call passes temporaryDirectory only at97–100, with no debug outputFile log or outputDirectory argument.

5. [464368483](https://github.com/icosa-foundation/open-brush/commit/464368483419b04e20117af8f658a91e1d88acfb) — Remove the duplicated shared localization entry. **include; candidate 14**. Pinned main contains two identical VIEW_MODE_CONTROLS entries with the same key ID. Remove the redundant block as an asset-integrity fix: avoid duplicate editor rows and ambiguous per-entry rename/remove behavior. Do not claim a current runtime lookup exception: installed SharedTableData lookup uses dictionary assignment and shadows the duplicate.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/512#discussion_r3397558407)

   Main: [Assets/Settings/Localization/Strings/Strings Shared Data.asset:3482](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Settings/Localization/Strings/Strings%20Shared%20Data.asset#L3482) — Duplicate tuple (317029250049335296, VIEW_MODE_CONTROLS) occurs at3482–3489 with identical empty metadata.

6. [456980291](https://github.com/icosa-foundation/open-brush/commit/456980291e3c88621593e595cc5a3f4cf847d7d4) — Record text API content and color edits for undo and saving. **feature-only**. Records edits from new text.setText/text.setColor endpoints using branch ModifyTextCommand. Main has existing text.add but neither endpoint nor ModifyTextCommand, so importing this would add operations rather than repair a current main mutation.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/API/ApiMethods.cs:1115](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.cs#L1115) — Main exposes text.add; pinned git grep has no text.setText or text.setColor endpoints.; [Assets/Scripts/Commands/ModifyTextCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/ModifyTextCommand.cs#Lnull) — Absent from pinned main.

7. [7f417378d](https://github.com/icosa-foundation/open-brush/commit/7f417378dae02247302e15b15be3b8900a743ed8) — Preserve text content and appearance when duplicating widgets. **include; candidate 6**. Reachable inherited main bug: create/customize text with existing text.add, then duplicate the widget. Main Clone instantiates the prefab and only copies transform/material state, losing text/font/fill/outline/mode. DuplicateSelectionCommand calls this method directly.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Widgets/TextWidget.cs:119](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L119) — Main Clone lines119–138 never assigns Text, TMP font, TextColor, StrokeColor or Mode from the source.; [Assets/Scripts/Commands/DuplicateSelectionCommand.cs:76](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/DuplicateSelectionCommand.cs#L76) — Existing selection duplication calls widget.Clone(); the stamped duplication path also calls it at127.; [Assets/Scripts/API/ApiMethods.cs:1115](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiMethods.cs#L1115) — Main already exposes text.add, so a new VR TextTool is not required to create a text widget.

8. [ca93a3813](https://github.com/icosa-foundation/open-brush/commit/ca93a381308b8d8000c7c592022b5e0d37136592) — Save text widget layer membership. **adapt; candidate 4**. Reachable inherited main save bug: text on a nondefault layer serializes no LayerId, then reload defaults to layer0. The source hunk restores metadata membership, but main also applies a canvas-local saved pose before world-preserving SetCanvas, so a complete transformed-layer round-trip needs a small feature-free restore-order adaptation rather than a pose-correctness claim from the one-line patch.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Save/MetadataUtils.cs:163](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/MetadataUtils.cs#L163) — GetTiltText initializes Transform/Text/FillColor/Pinned/GroupId/etc at163–174 but omits LayerId.; [Assets/Scripts/Save/SketchSnapshot.cs:153](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchSnapshot.cs#L153) — Main snapshot writes TextWidgets = MetadataUtils.GetTiltText(m_GroupIdMapping).; [Assets/Scripts/Widgets/TextWidget.cs:162](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L162) — FromTiltText uses App.Scene.GetOrCreateLayer(tiltText.LayerId).; [Assets/Scripts/WidgetManager.cs:902](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/WidgetManager.cs#L902) — Main restore loop calls TextWidget.FromTiltText for saved text entries.; [Assets/Scripts/Widgets/Media2dWidget.cs:53](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/Media2dWidget.cs#L53) — SaveTransform stores TrTransform.FromLocalTransform(transform), with widget-local signed size.; [Assets/Scripts/Widgets/TextWidget.cs:145](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/TextWidget.cs#L145) — FromTiltText first parents to App.Instance.m_CanvasTransform and applies saved local pose before calling SetCanvas at162.; [Assets/Scripts/Widgets/GrabWidget.cs:567](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/GrabWidget.cs#L567) — SetCanvas reparents with transform.parent and rescales for canvas ratio at585–590, preserving world pose rather than interpreting saved coordinates in the target canvas.

9. [00a4446c4](https://github.com/icosa-foundation/open-brush/commit/00a4446c489d1b20b93f99c58831cfd4433d853d) — Bound text export texture allocations while retaining aspect. **feature-only**. Caps raster allocations in new text export path. Main ExportCollector does not rasterize text, allocate text textures or call text-render cameras; it emits empty transforms for remaining nonimage widgets.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Export/ExportCollector.cs:126](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/ExportCollector.cs#L126) — Main BuildEmptyXforms(payload, notImages) handles text and other nonimage widgets; no Text Widgets/RenderTexture path exists at110–128.

10. [e6e76d79e](https://github.com/icosa-foundation/open-brush/commit/e6e76d79e4e7da3cd53f93fe5d93dedc9115257c) — Frame exported text using centered world-space glyph bounds. **feature-only**. Frames scaled/rotated glyphs only in new text raster export path. Main has no text export camera to misframe; porting would require adding the new export feature.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Export/ExportCollector.cs:126](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Export/ExportCollector.cs#L126) — Main emits empty transforms for nonimage widgets rather than a rasterized text quad.

### 20 feature/texture-painting

1. [a4871117a](https://github.com/icosa-foundation/open-brush/commit/a4871117aafc93d71afdf00e692ab3d3a630041c) — Give each paint canvas independent per-material state. **feature-only**. Gives new InkCanvas conversion per-renderer/material PaintSet ownership; main lacks the conversion command and manager.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Commands/EnableTexturePaintingCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/EnableTexturePaintingCommand.cs#Lnull) — Absent from exact main tree.; [Assets/Scripts/TexturePainterManager.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/TexturePainterManager.cs#Lnull) — Absent from exact main tree.

2. [aff99623b](https://github.com/icosa-foundation/open-brush/commit/aff99623b4c9bbe8b279fb2cd78fa49d44d56e27) — Initialize texture painting on guide and image mesh children. **feature-only**. Initializes child guide/image InkCanvas only through the absent paint conversion command.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Commands/EnableTexturePaintingCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/EnableTexturePaintingCommand.cs#Lnull) — Absent from exact main tree.; [Assets/Scripts/TexturePainterManager.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/TexturePainterManager.cs#Lnull) — Absent from exact main tree.

3. [fd19ad009](https://github.com/icosa-foundation/open-brush/commit/fd19ad009435b27e31d4855d5ae4704420fd0dbf) — Reject repeated conversion of existing paint canvases. **feature-only**. Rejects duplicate/inactive-child paint conversion in branch command/manager paths absent from main.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/Commands/EnableTexturePaintingCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/EnableTexturePaintingCommand.cs#Lnull) — Absent from exact main tree.; [Assets/Scripts/TexturePainterManager.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/TexturePainterManager.cs#Lnull) — Absent from exact main tree.

### 21 feature/voxel-editing

1. [3d90f5d4b](https://github.com/icosa-foundation/open-brush/commit/3d90f5d4b3a6d9e0775aa82bbb85901f34af04c1) — Allocate generated VOX shape IDs after all transform IDs. **feature-only**. Generated VOX transform/shape ID allocation exists only in the added VOX serializer; main's VoxImporter is read-only.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125979)

   Main: [Assets/Scripts/VoxSerialization/VoxWriter.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/VoxSerialization/VoxWriter.cs#Lnull) — Feature file/type is absent from the pinned main tree.

2. [de28d730e](https://github.com/icosa-foundation/open-brush/commit/de28d730ec2859f9b49f18fd647760e3cc6a4872) — Keep voxel editing active when choosing a brush color. **feature-only**. This exception keeps the new VoxelTool active during brush color selection. Main has no VoxelTool; its existing tool switch behavior is unaffected.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195699)

   Main: [Assets/Scripts/GUI/SketchSurfacePanel.cs:285](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/SketchSurfacePanel.cs#L285) — Current main paint-like tool guard excludes no existing voxel tool because it does not exist.

3. [56b1124f0](https://github.com/icosa-foundation/open-brush/commit/56b1124f00605a8d134cda43912a9dc8a6fb22e6) — Drop hidden voxel widgets as editing targets. **feature-only**. Target visibility validation applies only to the added voxel-editing tool.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195696)

   Main: [Assets/Scripts/Tools/VoxelTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/VoxelTool.cs#Lnull) — Feature file/type is absent from the pinned main tree.

4. [2de2a71a6](https://github.com/icosa-foundation/open-brush/commit/2de2a71a61554269761a767d04f6dd671c4ca232) — Batch voxel gesture visuals until editing completes. **feature-only**. Gesture autoVisuals/dirty suppression applies only to editable VOX gestures.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195698)

   Main: [Assets/Scripts/Tools/VoxelTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/VoxelTool.cs#Lnull) — Feature file/type is absent from the pinned main tree.

5. [6c1eb560d](https://github.com/icosa-foundation/open-brush/commit/6c1eb560d3d4ab385f5a3c94dfbab70238772a61) — Traverse intervening cells during voxel gestures. **feature-only**. Cell traversal interpolates edits produced only by the new VoxelTool.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082991)

   Main: [Assets/Scripts/Tools/VoxelTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/VoxelTool.cs#Lnull) — Feature file/type is absent from the pinned main tree.

6. [93af2b3e6](https://github.com/icosa-foundation/open-brush/commit/93af2b3e6542617f73c76b6c03c52f3e2193bf59) — Store changed voxel cells and palette state for gesture undo. **feature-only**. Cell/palette delta undo belongs to the new editable VOX command and tool.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082994)

   Main: [Assets/Scripts/Commands/ModifyVoxDocumentCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/ModifyVoxDocumentCommand.cs#Lnull) — Feature file/type is absent from the pinned main tree.

7. [27088ee58](https://github.com/icosa-foundation/open-brush/commit/27088ee58b424405cd573cbea9ee005d53c3dbf1) — Track unrecorded Lua VOX edits in the persistent sketch dirty state. **feature-only**. The dirty-state extension is called only by the added Lua VOX document wrapper. Main has neither that wrapper nor the editable-document mutation path; this is not a standalone fix for an existing main operation.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125975)

   Main: [Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs#Lnull) — Absent from main.; [Assets/Scripts/SketchMemoryScript.cs:288](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L288) — Main uses operation-stack clean state and has no MarkUnrecordedChange caller.

8. [5d06f5739](https://github.com/icosa-foundation/open-brush/commit/5d06f5739bff707093a88f0808a59713628eb31c) — Enable automatic visuals for new Lua VOX widgets. **feature-only**. Auto-visual initialization belongs to a Lua document wrapper absent on main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195692)

   Main: [Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs#Lnull) — Feature file/type is absent from the pinned main tree.

9. [f378bf147](https://github.com/icosa-foundation/open-brush/commit/f378bf1473abecd21a55434bb39a5b0b800e8283) — Validate all embedded VOX documents before restoring widget groups. **feature-only**. This validates RuntimeVoxDocument payloads before restoring embedded editable VOX widgets. Main ModelWidget has no embedded editable-document restore routine or type.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4171082997)

   Main: [Assets/Scripts/Widgets/ModelWidget.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Widgets/ModelWidget.cs#Lnull) — Main has no RuntimeVoxDocument/EditableVoxDocument restore path.

10. [c39db389a](https://github.com/icosa-foundation/open-brush/commit/c39db389ad5930b48340b6ea51d7e7365030c093) — Include hidden model colliders in imported geometry bounds. **defer**. The bounds utility exists on main, but this reported failure relies on hidden VOX instances introduced by the editable-document pipeline. Main's VoxImporter creates every imported model object active and does not apply VOX hidden flags. Including inactive geometry in every non-glTF importer may alter intended bounds semantics; a shared reproducer is not established.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4335577425)

   Main: [Assets/Scripts/Model.cs:1182](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Model.cs#L1182) — CalcBoundsNonGltf enumerates active BoxColliders and is shared by OBJ/FBX/USD/PLY/VOX import paths.; [Assets/Scripts/VoxImporter.cs:82](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/VoxImporter.cs#L82) — Creates fresh active model GameObjects; collider construction at lines103-105. No SetActive/hidden-state handling exists in this importer.; [Assets/Scripts/Model.cs:1240](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Model.cs#L1240) — Imported root is deactivated only in EndCreatePrefab, after bounds calculation.

11. [a8d0a4f2d](https://github.com/icosa-foundation/open-brush/commit/a8d0a4f2d92b95958f670d2ca7ba62f5a31936eb) — Avoid reviving hidden voxel targets when ending a gesture. **feature-only**. Hidden editing-target/gesture completion state exists only in the new tool.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195696)

   Main: [Assets/Scripts/Tools/VoxelTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/VoxelTool.cs#Lnull) — Feature file/type is absent from the pinned main tree.

12. [f88a7c28f](https://github.com/icosa-foundation/open-brush/commit/f88a7c28f5a69036a7a2d013b6decd6d2c873ac6) — Keep voxel command refresh state consistent with document wrappers. **feature-only**. ViewState/VisualsDirty is specific to runtime editable VOX documents.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057195698)

   Main: [Assets/Scripts/Commands/ModifyVoxDocumentCommand.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Commands/ModifyVoxDocumentCommand.cs#Lnull) — Feature file/type is absent from the pinned main tree.

13. [135431b09](https://github.com/icosa-foundation/open-brush/commit/135431b09a8ed42e09c713232dc0f388693a1248) — Preserve unrecorded voxel edits made during background saves. **adapt; candidate 1**. The commit mixes a VOX-only unrecorded-change revision mechanism with a generic save-completion correction. Main unconditionally marks memory clean after WriteSnapshotToFile returns, including error and selected-only saves; those two triggers exist without VOX editing.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4057125975)

   Main: [Assets/Scripts/Save/SaveLoadScript.cs:463](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L463) — SaveSelected invokes the selectedOnly path.; [Assets/Scripts/Save/SaveLoadScript.cs:540](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SaveLoadScript.cs#L540) — ThreadedSave obtains the snapshot write result, then lines549-552 unconditionally call SetLastOperationStackCount and update InitialSketchTransform before testing error.; [Assets/Scripts/SketchMemoryScript.cs:288](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/SketchMemoryScript.cs#L288) — Main's clean marker records the operation-stack count only; it has no unrecorded revision state.

14. [8c28668d6](https://github.com/icosa-foundation/open-brush/commit/8c28668d6877a67e6da616281c7c02f8e6cd5f49) — Track scripted VOX transforms without dirtying tool placement. **feature-only**. Scripted transform dirty tracking and tool-placement suppression require the new wrapper/tool.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342222952)

   Main: [Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs#Lnull) — Feature file/type is absent from the pinned main tree.

15. [5c16844cf](https://github.com/icosa-foundation/open-brush/commit/5c16844cf533a869086d24300586cb381d2ed9de) — Track scripted VOX widget visibility changes in sketch dirty state. **feature-only**. Scripted Show/Hide dirty tracking is specific to new runtime VOX document widgets.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342353828)

   Main: [Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs#Lnull) — Feature file/type is absent from the pinned main tree.

16. [9f48c36ba](https://github.com/icosa-foundation/open-brush/commit/9f48c36ba56cd6c709690af1bb00ac028773a8d9) — Deactivate voxel editing when leaving Advanced Mode. **feature-only**. Main has no VoxelTool enum/tool/button; adding its advanced-mode guard would require the feature.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1024#discussion_r4342771295)

   Main: [Assets/Scripts/GUI/SketchSurfacePanel.cs:295](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/SketchSurfacePanel.cs#L295) — Main's advanced-tool fallback has no voxel editing tool to deactivate.; [Assets/Scripts/Tools/VoxelTool.cs:null](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Tools/VoxelTool.cs#Lnull) — Absent from main.

### 22 feature/websocket-client

1. [5b2294864](https://github.com/icosa-foundation/open-brush/commit/5b2294864390c6e00cbdc8eb25a68001618862d6) — Disambiguate the Unity update awaiter from NativeWebSocket. **feature-only**. Disambiguates a type collision introduced by the new NativeWebSocket dependency. Main has only UnityAsyncAwaitUtil.WaitForUpdate in this awaiter context, without the added colliding dependency.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381365)

   Main: [Assets/Scripts/API/ApiManager.cs:49](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L49) — Outgoing listeners are HTTP List<Uri>, not outgoing WebSocket dictionaries/connection queues.

2. [1d044e946](https://github.com/icosa-foundation/open-brush/commit/1d044e9464bb179946b693195b45c0fe597cd63f) — Include listener URL in the outgoing GET size limit. **already-fixed**. Main already measures complete getUri.Length including listener URL and query at line791; this branch refactor regression is absent.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381370)

   Main: [Assets/Scripts/API/ApiManager.cs:790](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L790) — getUri includes listener URL and query; line791 checks getUri.Length <512.

3. [0183171b6](https://github.com/icosa-foundation/open-brush/commit/0183171b6dc3c40639505febe0bc53fdfd90822f) — Bound WebSocket connection backlog and remove closed listeners. **feature-only**. Bounds connecting outgoing WebSocket queues and removes closed sockets; main outgoing listeners use HTTP and own no outgoing socket queues.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/1113#discussion_r3996381341)

   Main: [Assets/Scripts/API/ApiManager.cs:49](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L49) — Outgoing listeners are HTTP List<Uri>, not outgoing WebSocket dictionaries/connection queues.

4. [84bb2d099](https://github.com/icosa-foundation/open-brush/commit/84bb2d099fd08e75eddf70fcdc4c6679deb721db) — Drain incoming frames for outgoing WebSocket listeners. **feature-only**. Drains receive queues on outgoing NativeWebSocket clients; main outgoing HTTP listeners have no such client queue.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/API/ApiManager.cs:49](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L49) — Outgoing listeners are HTTP List<Uri>, not outgoing WebSocket dictionaries/connection queues.

5. [87be94c7f](https://github.com/icosa-foundation/open-brush/commit/87be94c7ffb4d70c39a38c681084161a39fac923) — Stop outgoing WebSocket connections when the API manager ends. **feature-only**. Cancels feature outgoing WebSocket connections on destroy/quit. Main owns no such outgoing connections to stop.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/API/ApiManager.cs:49](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/API/ApiManager.cs#L49) — Outgoing listeners are HTTP List<Uri>, not outgoing WebSocket dictionaries/connection queues.

### 23 features/brush-editing

1. [a209c62e3](https://github.com/icosa-foundation/open-brush/commit/a209c62e38af8f550f5a10608e45113a19b3dd69) — Compare and hash serializable GUIDs by value. **feature-only**. Fixes branch-added SerializableGuid Equals/GetHashCode/operators that compare incompatible boxed/string values. Main has no those overrides/operators; do not add value-equality machinery without a demonstrated main caller needing it.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530086), [review 2](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530203)

   Main: [Assets/Scripts/Util/SerializableGuid.cs:65](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Util/SerializableGuid.cs#L65) — Main contains serialization/conversion/formatting only; the defective Equals/GetHashCode implementation being fixed is absent.

2. [8a9a9a75b](https://github.com/icosa-foundation/open-brush/commit/8a9a9a75b519afbb2bf5453085aee8d4102a31be) — Clamp brush page selection in the correct argument order. **already-fixed**. The malformed Clamp belongs to branch-added GotoPageForBrush. Main has no that method and its ordinary GotoPage already clamps in correct order.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530278)

   Main: [Assets/Scripts/GUI/BrushGrid.cs:527](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/BrushGrid.cs#L527) — Main has ordinary brush paging; GotoPageForBrush, UserVariantBrush, EditBrushPanel and runtime variant editing/embedding are absent.; [Assets/Scripts/GUI/BrushGrid.cs:529](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/BrushGrid.cs#L529) — Existing main paging already uses Clamp(value,min,max).

3. [2ec211dd8](https://github.com/icosa-foundation/open-brush/commit/2ec211dd8a2c63212225e77a2d1f7b11aa36ee72) — Preserve recursive file watching by default. **already-fixed**. Main default path-only watcher already enables recursive watching. The regression came from the branch adding optional includeSubdirectories=false; changing the filtered overload globally is not an equivalent main fix.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530299)

   Main: [Assets/Scripts/FileWatcher.cs:34](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/FileWatcher.cs#L34) — Main one-argument watcher constructor already preserves recursive behavior; no optional false default exists.

4. [1edd39ced](https://github.com/icosa-foundation/open-brush/commit/1edd39ced7968c1a704ceeda2e442b62a9944dd2) — List immediate brush directories consistently in folders and archives. **feature-only**. Implements directory listing parity for branch folder/archive user-brush reader; main has no FolderOrZipReader or user-brush archive listing.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176)

   Main: [Assets/Scripts/Save/SketchWriter.cs:209](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L209) — Main sketch writer uses ordinary brush GUID metadata; FolderOrZipReader/runtime user-brush packaging is absent.

5. [977f02ccf](https://github.com/icosa-foundation/open-brush/commit/977f02ccf557947a2299d8500c024a2e8c514778) — Preserve missing brush GUIDs when writing fallback metadata. **feature-only**. Guards a new fallbackBrushList/BaseGuid write for embedded user brushes. Main WriteMemory has no fallback brush list or BaseGuid dereference in this loop.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530324)

   Main: [Assets/Scripts/Save/SketchWriter.cs:223](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L223) — Main writes the original GUID and proceeds to stroke data without looking up a fallback descriptor.

6. [0a1c6f2e8](https://github.com/icosa-foundation/open-brush/commit/0a1c6f2e8f4cf3de44d8e310999a1821488670db) — Qualify archive path comparison enum. **feature-only**. Compilation correction qualifies System.StringComparison in the new folder/archive reader; class and added expression are absent from main.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176)

   Main: [Assets/Scripts/Save/SketchWriter.cs:209](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L209) — Main sketch writer uses ordinary brush GUID metadata; FolderOrZipReader/runtime user-brush packaging is absent.

7. [5e6fc58bd](https://github.com/icosa-foundation/open-brush/commit/5e6fc58bdf69180fae31ae47441a461fad4dfd0a) — Escape the archive path separator character. **feature-only**. Compilation correction escapes a character in the new archive reader; no corresponding main expression exists.

   Review: [review 1](https://github.com/icosa-foundation/open-brush/pull/860#discussion_r3397530176)

   Main: [Assets/Scripts/Save/SketchWriter.cs:209](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/Save/SketchWriter.cs#L209) — Main sketch writer uses ordinary brush GUID metadata; FolderOrZipReader/runtime user-brush packaging is absent.

8. [7815f928e](https://github.com/icosa-foundation/open-brush/commit/7815f928e73230d8ccfe73a09ddb75ccf4e3ba3f) — Keep brush preview drawings off sketch canvases. **feature-only**. Isolates and disposes synthetic stroke previews in new EditBrushPanel. Main has no material editor/preview drawing on its canvas.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/BrushGrid.cs:527](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/BrushGrid.cs#L527) — Main has ordinary brush paging; GotoPageForBrush, UserVariantBrush, EditBrushPanel and runtime variant editing/embedding are absent.

9. [1f6db0c25](https://github.com/icosa-foundation/open-brush/commit/1f6db0c255b81d44f362f59286c1a6c94a74b3c8) — Preserve unchanged user brush texture references on save. **feature-only**. Preserves texture filenames while saving runtime user-brush configs. Main has no UserVariantBrush.CopyMaterialToProperties or Brush.cfg serialization.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/BrushGrid.cs:527](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/BrushGrid.cs#L527) — Main has ordinary brush paging; GotoPageForBrush, UserVariantBrush, EditBrushPanel and runtime variant editing/embedding are absent.

10. [79810c5be](https://github.com/icosa-foundation/open-brush/commit/79810c5be893a1a5dd359b077c9b82608d0ed7d6) — Enable brush saving after every material edit. **feature-only**. Re-enables Save after material editing in feature panel. Main has no editable variant dirty/save button flow.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/BrushGrid.cs:527](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/BrushGrid.cs#L527) — Main has ordinary brush paging; GotoPageForBrush, UserVariantBrush, EditBrushPanel and runtime variant editing/embedding are absent.

11. [7552c6b8d](https://github.com/icosa-foundation/open-brush/commit/7552c6b8da0c236bd2f5ed2aad8e861f54c49770) — Select duplicated user brushes by their new library location. **feature-only**. Selects a newly duplicated user-brush folder deterministically. Main lacks runtime brush duplication/library/scene variant catalogs.

   Review: No matching review comment; branch-audit fix.

   Main: [Assets/Scripts/GUI/BrushGrid.cs:527](https://github.com/icosa-foundation/open-brush/blob/5591ff1aad5288903e718752c522d54420ae6d1e/Assets/Scripts/GUI/BrushGrid.cs#L527) — Main has ordinary brush paging; GotoPageForBrush, UserVariantBrush, EditBrushPanel and runtime variant editing/embedding are absent.


