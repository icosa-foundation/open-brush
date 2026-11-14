**Brush batching performance review**

Measured workload update: [default batch-mode application results](Support/Benchmarks/AppBatchWorkloads.md) now supersede the source-only priority estimates below. These desktop traces point first to per-stroke visual updates and batch appending; completed-batch uploads are a smaller loading cost. Existing visibility changes reduce older-batch cache memory but do not establish a drawing/loading/FPS gain. Mobile remains unmeasured.

Implementation follow-up: triangle-ID generation, partial visibility index uploads, and index-only reloads for evicted batches are now implemented and benchmarked. See [measured results](brush-batching-benchmark-results.md) and [reproducible visibility benchmarks](Support/Benchmarks/BatchVisibility.md). The review below records the original source-analysis baseline.

25 October 2026. Source baseline: `39b088aab` in `C:\Users\andyb\Documents\open-brush-fast`.

The strongest low-memory opportunity is to stop rebuilding or resubmitting unchanged geometry. Use Unity's advanced mesh buffer APIs for incremental updates, preserve the existing eviction of managed geometry from older batches, and fix the triangle-ID allocation hotspot first. For large sketches that are merely being viewed, investigate SRP Batcher compatibility separately: faster mesh construction does not automatically improve steady-state rendering.

This is a source and API review, not a benchmark. No Unity Editor, player, device, build, or profiler session was run. Rankings describe expected benefit and confidence in the mechanism; they do not claim measured frame-time improvements. The project specifies Unity **6000.6.0f1**, URP **17.6.0**, and resolves Burst **2.0.0**, Collections **6.6.0**, and Mathematics **1.4.0**. The cited Unity 6 documentation establishes the APIs and baseline constraints; rendering feature eligibility should also be checked in the exact shipped Editor and graphics backend.

**1. Recommended priorities**

| Priority | Change | Where it helps most | Expected benefit | RAM assessment | Effort / confidence |
| --- | --- | --- | --- | --- | --- |
| 1 | Remove mesh-array getters from triangle-ID generation; fix subset indexing | Drawing/loading and undo clones on the non-geometry-shader path | Potentially very large allocation and CPU reduction in the subset path; useful repeated savings in full uploads | Reduces transient allocations; bounded scratch only | Small / high confidence in wasted work |
| 2 | Partial index updates for hide/show; preserve bounds | Selection, erase, undo/redo on populated batches | Work proportional to changed indices rather than the whole batch | Near-neutral with bounded conversion scratch; cold-batch reload needs separate attention | Medium / high |
| 3 | Persistent mesh capacity and partial vertex/index updates | Stroke completion, sketch playback/loading, long live strokes | Largest broad mesh-update opportunity; avoids repeated prefix copies | Modest bounded slack for mutable meshes; avoid permanent extra copies | Medium to large / high mechanism confidence |
| 4 | Incremental bounds and removal of redundant recalculation | All geometry updates | Removes full geometry scans accompanying uploads | A few bounds/count values per batch | Small to medium / high |
| 5 | Restore SRP Batcher eligibility where brush shaders support it | CPU-limited viewing of large sketches | Potentially important steady-state render CPU reduction | Little additional geometry memory required | Medium / benefit must be profiled |
| 6 | Read-only MeshData for cold-batch access; targeted writable MeshData + Burst jobs | Editing old batches, export, bulk load/rebuild | Avoids unnecessary materialization; can reduce main-thread stalls | Good only with bounded lifetimes and replacement of copies | Medium to large / workload dependent |
| 7 | Per-brush compact GPU vertex layouts | Large scenes limited by vertex bandwidth or memory | Smaller buffers and transfers; possible GPU benefit | Saves memory, but rehydration precision must be addressed | Medium to large / brush dependent |
| 8 | GPU Resident Drawer / custom BatchRendererGroup | Very large CPU-limited scenes | Conditional; unique stroke meshes limit instancing benefit | Extra renderer/culling buffers and implementation complexity | Large / defer |

Priority reflects benefit relative to implementation risk and mobile memory constraints. If profiling shows steady-state rendering dominates, priority 5 should move ahead of loading work. If drawing latency dominates, prioritize the active-stroke portion of priority 3.

**2. What the current code actually does**

The code is old, but already contains useful optimizations that should survive a modernization.

1. **Batches group strokes by brush, within a canvas.** `BatchManager.GetBatch` appends to the last batch with space. `Batch.HasSpaceFor` normally limits it to 15,999 vertices; `LargeMeshSupport` instead selects a very large limit and UInt32 indices. Each batch has a MeshRenderer and its own mesh; overlays and additional render passes can add draws. See [BatchManager.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Batching/BatchManager.cs:145>) and [Batch.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Batching/Batch.cs:236>).
2. **Updates are already deferred and coalesced.** Dirty flags are checked in `LateUpdate`, `OnWillRenderObject`, and explicit flushes. A clean batch does not upload geometry every frame. An inactive object's `DelayedUpdateMesh` flushes immediately, so callers that build inactive canvases may get less coalescing. `MarkDynamic()` is already used; adding it is not a new optimization.
3. **A vertex change rebuilds the whole destination mesh.** `Batch.UpdateMesh` calls `GeometryPool.CopyToMesh`, which calls `Clear(false)`, uploads all positions, indices, enabled normals/UVs/colors/tangents, and optionally regenerates picking IDs. It does this even when only one stroke has been appended. See [GeometryPool.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Brushes/GeometryPool.cs:843>).
4. **Visibility changes already avoid vertex uploads, but upload all indices.** `DisableSubset` saves that stroke's indices in a `ushort[]`, replaces them with zero, and marks topology dirty. `EnableSubset` restores them. `UpdateMesh` then calls `SetTriangles` for the entire batch. The zeroed triangles preserve index slots used by picking.
5. **Live drawing has a related, separate cost.** `GeometryBrush.ApplyChangesToVisuals` rebuilds its mesh through the same pool method and explicitly recalculates bounds afterward. `QuadStripBrush.ApplyChangesToVisuals` assigns its full backing attribute arrays. Improving completed batches alone will not remove this per-update drawing work. See [GeometryBrush.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Brushes/GeometryBrush.cs:413>) and [QuadStripBrush.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Brushes/QuadStripBrush.cs:710>).
6. **Old batches already shed a CPU copy.** With `m_EnableBatchMemoryOptimization`, enabled in Main, all but the newest batch in each pool become eligible to discard managed geometry after more than 60 batch-manager updates since their last mesh write. These are update ticks, not seconds. Unity's readable mesh then becomes the backing store; subsequent edits reload it. See [BatchPool.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Batching/BatchPool.cs:76>), `Batch.ClearCachedGeometry` at line 175, and `GeometryPool.ReloadFromMesh` at line 729.

Consequently, the repeated-upload problem scales with **changed batches and live strokes**, whereas rendering and per-batch callback overhead scale with the total visible/populated batch count. Loading many strokes in one frame is already partly protected by dirty-flag coalescing. A claim that every stored stroke is uploaded every frame would be incorrect.

**3. First fix: triangle-ID generation**

The fallback is selected by `Config.GeometryShaderSuppported`: it returns device geometry-shader support, except Zapbox forces false. This is capability-dependent, not simply desktop versus mobile. The fallback intersection shader consumes `TEXCOORD4.x` as the triangle identifier.

In `GeometryPool.GenerateTriangleIds(Mesh)`, lines 891â€“904, every full upload creates a `Vector2[vertexCount]` and reads `mesh.triangles` twice. Unity's array-valued mesh accessors return copies. For V vertices and I indices, these three array payloads alone account for approximately **8V + 8I bytes allocated per invocation**, excluding headers and Unity's own work. The CPU topology already exists in `m_Tris`. [Unity's guidance on array-valued API allocations](https://docs.unity3d.com/cn/2022.3/Manual/performance-garbage-collection-best-practices.html).

The subset overload, lines 907â€“921, is worse: it accesses `mesh.triangles` in the loop condition and repeatedly inside each iteration. Each getter copies the full index array. With T triangles, aggregate copied data grows quadratically with T. This path is reached by subset `CopyToMesh`, including `BatchManager.CloneAsUndoObject`. It is not necessarily invoked every drawing frame, but can cause severe operation-specific allocation spikes.

There is also a correctness concern to resolve before optimizing that overload: subset `CopyToMesh` first subtracts `iVert` from destination triangle indices, then the ID generator compares those already-local indices against the original source `iVert`. For nonzero source offsets this mixes index spaces. A rewrite should explicitly distinguish source offsets, destination-local vertices, and the triangle numbering expected by the consumer.

Recommended implementation:

1. Generate IDs from the existing pool index data, or from the already-rebased temporary index data for a subset. Do not read topology back from the Mesh.
2. Use a reusable, size-bounded scratch buffer for synchronous uploads. Avoid one permanently retained maximum-size scratch array per batch.
3. During incremental appends, calculate IDs only for new geometry, preserving the appropriate batch triangle offset.
4. Preserve the existing mapping for shared vertices until picking semantics are deliberately redesigned. A vertex shared by different triangles cannot store a distinct ID for each incident triangle. Do not replace IDs with `SV_VertexID / 3` unless the topology is proven to be unshared triangle soup.
5. Keep the channel present on platforms that require the fallback. Duplicating vertices to make every triangle independent can substantially increase RAM and vertex work and is not the preferred fix.

This improvement does not require a jobs rewrite or even a new mesh API. It should precede benchmarking a new uploader so it does not distort the comparison.

**4. Incremental index and vertex uploads**

Unity provides explicit destination offsets through [SetVertexBufferData](https://docs.unity3d.com/ja/current/ScriptReference/Mesh.SetVertexBufferData.html) and [SetIndexBufferData](https://docs.unity3d.com/ja/current/ScriptReference/Mesh.SetIndexBufferData.html). The design change is to retain an allocated mesh buffer, track which spans changed, and upload those spans. Merely renaming the existing full-array calls would leave most of the cost.

For visibility changes, `BatchSubset.m_iTriIndex` and `m_nTriIndex` already identify the exact dirty index interval. Merge adjacent intervals within a frame and update them once. For scattered modifications, fall back to one larger interval or the full index buffer when call overhead would dominate. Keep zeroed triangles and stable index locations initially. Creating a submesh/draw for every stroke would undermine batching; compacting visible triangles would require remapping picking IDs and subset offsets.

The raw index API requires the supplied element width to match the configured index format. The existing `List<int>` must not be copied as raw data into a UInt16 buffer. Use bounded `ushort` conversion scratch for UInt16 meshes, or redesign the authoritative index storage after measuring the value. Do not switch all meshes to UInt32 just to avoid conversion.

For appends, allocate vertex/index capacity separately from logical counts. Use `SetVertexBufferParams` and `SetIndexBufferParams` when creating or growing buffers, then upload only the newly appended range. Growing buffers may require a complete re-upload; it must be an occasional event rather than every append. Avoid `Clear(false)` during ordinary updates.

A sizing policy should allow limited slack only in actively growing meshes, cap that slack across many brush pools/canvases, and release or compact it when batches settle if the one-time cost is justified. Preallocating maximum-sized buffers for every small batch, especially with `OneStrokePerBatch`, is unsuitable for mobile.

Use a `SubMeshDescriptor` to set the drawn index count and valid vertex range, and explicitly maintain both submesh bounds and `mesh.bounds`. Allocated vertex capacity is not the logical vertex count. The current `SelfCheck`, nonresident `GeometryPool.NumVerts`, reload routines, exporters, and mesh consumers assume these counts agree. They must be adapted together or receive compact meshes. Otherwise spare capacity can leak into export, bounds, and readback. [SetSubMesh semantics](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetSubMesh.html).

For N equal stroke appends of s vertices, each followed by a flush, the current cumulative submitted vertex count is approximately:

`s Ã— N Ã— (N + 1) / 2`

An incremental path submits approximately `s Ã— N`, plus growth copies. For 100 separate flushes this is 50.5 times less submitted vertex data in the idealized case. This is **not a frame-rate estimate**; existing coalescing, buffer growth, API overhead, generation cost, and backend upload behavior determine the real gain. If all 100 appends coalesce into one flush, this particular amplification largely disappears.

The active brush uploader should share the underlying buffer machinery. `GeometryBrush` already tracks `m_FirstChangedControlPoint`, and knots contain vertex/triangle offsets. However, the dirty range must come from the actual generator: `FlatGeometryBrush.ControlPointsChanged` at line 115 backs up across a smoothing window and recalculates tangents, while UV styles can update different spans. Assuming only the newest knot changes would corrupt existing geometry.

A practical first implementation can keep managed GeometryPool ownership and use bounded packing buffers. There are only four Unity vertex streams, while GeometryPool can contain more than four separate attribute lists, so a one-list-per-stream mapping does not generally work. Pack compatible attributes into a few streams; separate streams only when measured update patterns justify them. Avoid allocating a second complete persistent native representation alongside all existing lists. [VertexAttributeDescriptor layout rules](https://docs.unity3d.com/ja/current/ScriptReference/Rendering.VertexAttributeDescriptor.html).

Finally, the `start` and `length` overloads of `SetVertices` select a slice of the **source** and replace the mesh's vertex collection; they are useful for clone construction, but are not destination-range updates. [SetVertices documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetVertices.html).

**5. Bounds and validation work**

`GeometryPool.CopyToMesh` uses `SetTriangles` with default bounds calculation. `GeometryBrush.ApplyChangesToVisuals` then explicitly calls `RecalculateBounds`. The batch topology-only path also uses default bounds calculation even though visibility changes do not move vertices.

Each appended subset already gets a bounds calculation in `Batch.AddSubset` at lines 415 and 480. Maintain the union of those bounds for completed batches. A visibility-only update can safely retain a conservative bound covering hidden geometry. Deletion/shrink may temporarily leave loose bounds; recompute from remaining subset bounds at a controlled point if that materially improves culling.

Preserve `Brush.m_BoundsPadding`, currently applied after a full batch upload, including its factor of `2 Ã— App.METERS_TO_UNITS`. Shader-expanded geometry must remain inside the bound. The topology-only recalculation currently does not reapply that padding, which is another reason to make bounds ownership explicit.

For live strokes, maintain bounds for the changed suffix and the retained prefix, or use periodic full recalculation when shrinkage makes incremental bounds too loose. Do not retain a tree or per-vertex bounds cache without evidence that simpler bounds are insufficient.

Use `DontRecalculateBounds` only where correct bounds and descriptor ranges are supplied. Introduce `DontValidateIndices` only after index generation and range invariants are established; keep explicit development checks. Do not broadly enable `DontNotifyMeshUsers`, because renderer bounds changes still need to propagate. These flags skip work rather than make invalid data safe. [MeshUpdateFlags](https://docs.unity3d.com/ja/current/ScriptReference/Rendering.MeshUpdateFlags.html).

**6. Cold geometry, MeshData, and Burst**

The existing residency policy is valuable. A hidden/reshown stroke in a cold batch currently triggers `EnsureGeometryResident`, which reconstructs all vertex attributes and indices even though the operation only needs topology. Partial GPU uploads alone will not remove that first-edit allocation spike.

An additional targeted improvement is to let visibility-only edits access just the required index range from a readable mesh, patch it, and leave unrelated attributes nonresident. Keep triangle backups and backing-mesh ownership consistent, with no outstanding snapshot when the mesh is mutated. General edits can still materialize the full GeometryPool.

`AcquireReadOnlyMeshData` is useful for export, copying subsets from cold batches, and read-only intersection/geometry operations that would otherwise allocate all mesh arrays. Access raw data directly where layouts are known; if the caller still demands complete managed lists, those allocations remain. Dispose the snapshot before modifying the source mesh, because retaining it across mutation forces Unity to preserve a copied snapshot. It also requires a readable mesh at runtime. [AcquireReadOnlyMeshData](https://docs.unity3d.com/ja/current/ScriptReference/Mesh.AcquireReadOnlyMeshData.html).

For bulk construction, use `AllocateWritableMeshData`, fill its buffers in jobs, then apply/dispose once ready. Allocate several destination meshes in one MeshDataArray where practical. Good first job candidates are transforms, index rebasing, bounds reductions, and packing already-generated geometry. Existing brush generation uses polymorphic brush objects and managed state, so it is not automatically Burst-compatible.

Parallelizing full stroke generation may eventually improve load time, but it has a larger correctness surface: brush-specific state, random sequencing, Unity object access, and ordering. First measure whether generation remains dominant after upload fixes. Schedule work in chunks rather than a tiny job per pointer sample, and cap the number of in-flight destination meshes to prevent a large load-time RAM peak. [Writable MeshData workflow](https://docs.unity3d.com/ja/current/ScriptReference/Mesh.MeshData.html).

Writable MeshData is most attractive for **new or substantially rebuilt meshes**. Creating fresh MeshData for every small edit is not the first choice for persistent dynamic batches. Native arrays reduce managed GC only when they replace storage or have bounded temporary lifetimes; they are not free memory.

**7. Steady-state rendering and SRP Batcher**

The configured URP asset has `m_UseSRPBatcher: 1` and `m_GPUResidentDrawerMode: 0`. Its GUID matches GraphicsSettings, and the checked quality levels do not override the render pipeline. See [the URP asset](<C:/Users/andyb/Documents/open-brush-fast/Assets/Settings/Open Brush Universal Render Pipeline Asset.asset:72>).

However, `Batch.Create` at lines 80â€“84 installs a renderer-wide MaterialPropertyBlock containing `_BatchID`. Unity documents that these blocks remove SRP Batcher compatibility. Enabling the pipeline setting alone therefore does not establish that brush batches use the fast path. The code also instantiates a material per batch; shader compatibility and material mutation patterns need their own audit. [Unity's MaterialPropertyBlock guidance](https://docs.unity3d.com/ja/current/ScriptReference/MaterialPropertyBlock.html).

A focused experiment should move picking IDs out of the normal color-pass renderer property block. The current GPU intersection code records `CommandBuffer.DrawRenderer` calls using an override intersection material; simply writing the ID to the brush material and clearing the block would not prove that the override pass still receives it. Supply IDs explicitly in the picking draw path, for example through an appropriate explicit draw/property mechanism, while preserving transforms, submeshes, XR, and widget handling. See [GpuIntersector.cs](<C:/Users/andyb/Documents/open-brush-fast/Assets/Scripts/Rendering/Intersection/GpuIntersector.cs:371>).

Then audit the normal brush shaders' constant-buffer layout and inspect actual SRP batching in Frame Debugger. This could lower submission cost with little geometry-memory change. It does not combine unique geometry into one draw and does not solve transparency overdraw or expensive fragment shaders.

Afterward, if thousands of batches still incur noticeable script callback cost, consider a manager-owned dirty queue instead of one `LateUpdate` and render callback per batch. Ensure dirty geometry is flushed before color, picking, highlight, export, and multi-camera rendering. This is a secondary architectural optimization, not a reason to rewrite the renderer first.

**8. Memory budget and compact formats**

For an illustrative vertex with Float32 position, normal and tangent, Color32, and one Float32 UV2, the attribute payload is **52 bytes/vertex**. The fallback picking channel adds another **8 bytes/vertex** to the Mesh. Actual brushes have different layouts.

Using V vertices and I index elements:

| Storage | Approximate payload for that layout |
| --- | --- |
| Resident managed GeometryPool | `52V + 4I` bytes, plus unused list capacity |
| Readable Unity Mesh CPU data, UInt16, fallback IDs | `60V + 2I` bytes |
| GPU mesh data, same layout | Approximately `60V + 2I` bytes, backend dependent |
| Saved visibility backups | `2 Ã— backed-up index count` bytes |
| Additional full native shadow copy | Another vertex/index payload unless it replaces existing storage |

For one million aggregate vertices and three million aggregate indices spread across UInt16 batches, these first three payloads are approximately **64 MB + 66 MB + 66 MB**, before capacity, objects, control points, textures, driver staging, and other scene data. This is an arithmetic illustration, not a measured sketch. Mobile unified memory does not justify ignoring GPU buffer allocations, and physical residency/accounting should be measured on device.

The existing eviction policy removes most of the managed-pool term for settled older batches. A permanent native shadow copy for the entire sketch could give that saving back. Keep additional working memory proportional to the active edit/load window rather than total sketch size.

Compact layouts can reduce both upload bandwidth and mesh memory. Keep positions Float32 initially. Color32 is already compact. Evaluate Float16 UV2, packed normals, and packed tangents only for brush layouts where their semantics and error tolerances allow it. GeometryPool explicitly supports UV channels carrying positions, vectors, distances, and timestamps; normals are not universally just ordinary shading normals.

Unity requires valid attribute sizes/alignment; Float16 with three components is not a valid six-byte attribute layout, and device format support must be checked. Packing may need padding or shader decoding. Test animated/deforming and export behavior as well as static appearance. [Vertex format constraints](https://docs.unity3d.com/ja/current/ScriptReference/Rendering.VertexAttributeDescriptor.html).

There is an important interaction with eviction: the readable mesh currently becomes the authoritative geometry backing store. Lossy GPU formats can therefore reduce editing/export precision when GeometryPool reloads it, and repeated edit/repack cycles can compound loss. Keeping a second full-precision copy for every batch undermines the RAM goal. Resolve that ownership/precision tradeoff before enabling compression broadly.

`UploadMeshData(true)` releases the readable CPU copy, but is incompatible with today's cold-batch reload and geometry consumers. It is suitable only for a separately defined immutable/view-only path with an acceptable reconstruction or alternate backing strategy. Do not enable it globally for completed strokes. [UploadMeshData](https://docs.unity3d.com/cn/6000.0/ScriptReference/Mesh.UploadMeshData.html).

**9. Features to defer or constrain**

| Option | Assessment for this application |
| --- | --- |
| Larger batches / UInt32 indices | Can reduce renderer/draw counts but coarsens culling, increases edit/upload scope, and doubles index-buffer bytes versus UInt16. Evaluate modest bounded sizes only after incremental updates. |
| GPU Resident Drawer | Potential CPU benefit, but strongest instancing gains require repeated meshes. Stroke batches mostly contain unique geometry. The documented Unity 6 path requires Forward+ and compute-capable APIs excluding OpenGL ES; shader and renderer eligibility also matter. |
| Custom BatchRendererGroup | Can improve large-scale draw submission/culling, but requires custom data ownership, shader support, visibility, selection and XR integration. Defer until draw submission is measured as the remaining bottleneck. |
| Ordinary GPU instancing | Appropriate for repeated identical meshes, not arbitrary unique stroke geometry. It is not a drop-in replacement for current mesh merging. |
| Additional static batching / CombineMeshes | The application already combines strokes. Another combination layer risks more construction copies and conflicts with editing and residency assumptions. |
| GPU-generated geometry / direct GraphicsBuffer writes | Can shift work off the CPU, but CPU editing/export/readback needs remain. A GPU-only design would be a larger architectural change with synchronization and platform costs. |
| Per-stroke shader visibility flags | Avoids some index writes but adds IDs/flags and shader logic, and may still process hidden vertices or fragments. Small index patches are a better first experiment. |
| Mesh index/vertex reordering | Potential vertex-cache benefit for finalized geometry, but triangle order and vertex offsets currently drive picking/subsets. Do not invoke an optimizer without updating those mappings. |

The `LargeMeshSupport` switch needs particular caution. `DisableSubset` casts absolute indices into `ushort` backups, so indices above 65,535 can be truncated. Separately, both intersection encoding and CPU decoding use a 16-bit triangle ID. Raising vertex capacity does not remove that triangle-count limit. These are source-visible constraints, not speculative GPU limitations.

Spatially grouping batches might improve culling without duplicating geometry, but changes renderer count, ordering, and potentially transparent appearance. Conversely, making batches arbitrarily large can worsen mobile GPU cost despite fewer draws. Treat batch sizing as a culling/submission tradeoff.

GPU Resident Drawer also documents restrictions on property blocks and per-instance rendering callbacks, both relevant to this design. Confirm exact-version eligibility before an experiment rather than just switching it on. [GPU Resident Drawer setup](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html), [object compatibility](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html).

**10. Suggested implementation sequence and focused measurement**

1. **Establish representative costs.** Use the same sketches on a target desktop and a representative mobile/standalone device. Separate viewing a settled sketch, drawing a long stroke, loading many short strokes, bulk hide/show/undo, and editing a cold batch. Include a device/backend using fallback picking. Record main-thread and render-thread time, GPU time, worst-operation/p95 frame time, managed allocations, peak process/native/graphics memory, and steady memory after eviction.
2. **Fix triangle-ID allocation and indexing first.** Check picking and undo-clone geometry with a subset that starts at a nonzero source vertex. Compare allocation volume and operation latency.
3. **Introduce partial index updates and explicit bounds.** Retain current index order and UInt16 storage. Measure visibility changes in both resident and cold batches so reload costs are not hidden.
4. **Build a shared incremental uploader for completed and active geometry.** Track actual dirty spans, logical counts, allocated capacity, and bytes submitted. Include append, tail shrink, remove, hide/show, and regeneration from earlier knots. Start with unchanged precision and the existing managed ownership model.
5. **Investigate SRP Batcher eligibility alongside large-sketch viewing.** Separate picking IDs from normal rendering, verify shader compatibility, and compare actual CPU submission time. If the sketch is GPU-bound, these CPU savings may not increase frame rate.
6. **Apply MeshData/jobs only to remaining measured hotspots.** Bound in-flight work and temporary buffers. Reject a change that wins average CPU time by retaining a sketch-sized additional copy or producing unacceptable peak RAM on mobile.
7. **Consider compact formats and larger rendering changes last.** Require acceptable edit/export precision and brush appearance before rollout.

Use distinct profiler markers such as `BrushBatchReview.UploadVertices`, `BrushBatchReview.UploadIndices`, `BrushBatchReview.Bounds`, `BrushBatchReview.TriangleIds`, and `BrushBatchReview.Rehydrate`. The useful comparison is the same operation and sketch before/after, including bytes submitted and memory peaks, rather than a synthetic mesh API microbenchmark alone.

The first four priorities offer a coherent modernization with bounded memory: retain batching and eviction, preserve logical stroke identity, and make mutation cost follow the geometry that actually changed.


