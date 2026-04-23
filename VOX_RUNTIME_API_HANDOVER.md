# VOX Runtime Generation & Editing Handover

## 1) Objective and scope

This document hands over a full implementation plan for adapting Open Brush's current VOX import pipeline so voxel data can be generated, edited, meshed, and serialized at runtime through API endpoints (no UI work in this phase).

In scope:
- Runtime ingestion from memory/streams, not just file paths.
- Reusable mesh generation for repeated rebuilds after edits.
- A mutable runtime voxel document for API-level editing.
- Serialization back to `.vox` bytes for persistence or API responses.
- Import options configurable per call (mesh mode/material/collider behavior).

Out of scope (for this phase):
- Editor UX and in-app UI controls.
- Multiplayer synchronization protocol.
- Advanced VOX chunk coverage beyond the subset currently needed.

---

## 2) Current-state baseline (what exists today)

The current implementation is centered around `Assets/Scripts/VoxImporter.cs` and has these key characteristics:

1. **File-path-coupled load path**
   - `VoxImporter` is constructed with a path and currently uses `VoxReader.VoxReader.Read(m_path)` from `Import()`. That makes filesystem access mandatory for load and blocks direct API payload ingestion.

2. **Importer owns both data ingestion and mesh construction**
   - `Import()` reads VOX data, creates Unity `GameObject` hierarchy, and invokes internal mesh generation helpers.
   - Greedy-mesh and per-voxel-cube strategies are private importer internals (`GenerateOptimizedMesh`, `GenerateSeparateCubesMesh`, `GreedyMesh`, `GreedyMeshAxis`, `AddQuad`, `AddCube`, `VoxelGrid`, etc.), limiting reuse for runtime editing.

3. **Material and collider assignment are bound into import workflow**
   - Material defaults to `ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial`.
   - Box colliders are always added to generated model objects.

4. **Warnings are collected as strings and returned from importer**
   - Existing warning flow should remain backward-compatible for existing callers.

5. **Mesh mode is constructor state, not call-level options**
   - `MeshMode` exists, but selection is importer-instance-level and not tied to a request/options contract.

---

## 3) Architectural target state

Target is a layered pipeline where runtime API handlers can operate in-memory:

1. **Data IO layer**
   - Load from path, stream, or byte memory.
   - Parse to a VOX model abstraction (`IVoxFile` and/or runtime document).

2. **Runtime document layer**
   - Mutable structure supporting voxel CRUD, palette edits, metadata.
   - Can convert from and to parsed VOX structures.

3. **Mesh layer**
   - Stateless/reusable mesh-builder utility.
   - Build mesh from parsed model or runtime document without tying to file IO.

4. **Importer/presenter layer**
   - Optional Unity `GameObject` creation and component/material/collider wiring.
   - Driven by options object per call.

5. **Serialization layer**
   - Emit `.vox` bytes from runtime document.
   - Round-trip compatibility checks.

---

## 4) Recommended implementation sequence (minimizes merge conflicts)

Implement in this exact order. Each stage can be a stacked branch/PR.

### Stage 1: Add stream/byte-buffer ingestion while preserving existing API

**Goal**: Decouple importer from path-only reads with minimal refactor.

Deliverables:
- Add additional constructor(s) and/or static factory overloads on `VoxImporter` to accept:
  - `Stream`
  - `ReadOnlyMemory<byte>` or `byte[]`
- Create shared internal import path:
  - Parse `IVoxFile` once.
  - Reuse existing model iteration and mesh generation code.
- Keep current path constructor and behavior intact for backward compatibility.

Notes:
- Ensure `ImportMaterialCollector` still works for path-based flow. For stream flow, define deterministic behavior (e.g., null/empty dir context, or optional virtual source name).
- Keep warning semantics consistent with existing return tuple.

### Stage 2: Extract mesh building into a reusable service

**Goal**: Separate geometry generation from importer orchestration.

Deliverables:
- Create `VoxMeshBuilder` (suggested: `Assets/Scripts/VoxMeshBuilder.cs` or nearby existing conventions).
- Move/port these internals from `VoxImporter` to builder:
  - `GenerateOptimizedMesh`
  - `GenerateSeparateCubesMesh`
  - `GreedyMesh`, `GreedyMeshAxis`
  - `AddQuad`, `AddCube`
  - `VoxelGrid`, `MeshData` helper types
- `VoxImporter` should call builder methods instead of owning mesh internals.

Design guidance:
- Keep builder independent from Unity scene object creation.
- It can still return Unity `Mesh` (pragmatic), but should not instantiate `GameObject`, `Renderer`, or `Collider`.
- Prefer explicit options argument over hidden importer state.

### Stage 3: Introduce mutable runtime voxel document

**Goal**: Enable API-level edits without re-reading source files.

Deliverables:
- Implement `RuntimeVoxDocument` (name may vary), containing:
  - Model list or single-model abstraction
  - Voxel collection keyed by position
  - Palette table/index mapping
  - Optional metadata (model names, dimensions/pivots if needed)
- Mutation APIs:
  - Add voxel / remove voxel / move voxel
  - Set voxel color (index or RGBA)
  - Replace palette entries
- Conversion APIs:
  - `IVoxFile -> RuntimeVoxDocument`
  - `RuntimeVoxDocument -> mesh-builder input`

Design guidance:
- Use deterministic coordinate conventions and document them (especially Y/Z orientation and current importer rotation behavior).
- Ensure duplicate voxel handling is defined (overwrite, reject, or first-write-wins).

### Stage 4: Add VOX serialization support

**Goal**: Allow edited/generated runtime data to be exported as `.vox` bytes.

Deliverables:
- Determine whether VoxReader in repo supports writing.
- If not, implement minimal writer for required chunks:
  - Header/version
  - `MAIN`
  - `SIZE`
  - `XYZI`
  - `RGBA` (if palette customization is needed)
- Implement `RuntimeVoxDocument.ToVoxBytes()`.

Design guidance:
- Start with subset this app needs; explicitly document unsupported VOX features.
- Keep writer deterministic for easier regression tests.

### Stage 5: Add per-call import/build options object

**Goal**: Make behavior request-driven for API endpoints.

Deliverables:
- Introduce options type (e.g., `VoxImportOptions`) with at least:
  - Mesh mode (optimized vs separate cubes)
  - Material override
  - Collider generation toggle
  - Optional object naming/source label
- Add overloads so callers can pass options per import/build call.
- Preserve default behavior equivalent to current importer behavior when options are omitted.

---

## 4.5) Active task tracker (source of truth)

Last updated: 2026-02-12

Status legend:
- `[ ]` Not started
- `[-]` In progress
- `[x]` Completed
- `[!]` Blocked

Stage checklist:
- `[x]` Stage 1: Add stream/byte-buffer ingestion while preserving existing API
- `[x]` Stage 2: Extract mesh building into a reusable service
- `[x]` Stage 3: Introduce mutable runtime voxel document
- `[x]` Stage 4: Add VOX serialization support
- `[x]` Stage 5: Add per-call import/build options object

Current focus:
- End-to-end validation and cleanup notes (including async command timing on load/save flows).

Work log:
- `2026-02-12`: Stage 1 implementation started and completed in `Assets/Scripts/VoxImporter.cs`.
  - Added overloads to ingest VOX from `byte[]`, `ReadOnlyMemory<byte>`, and `Stream`.
  - Added shared parse path (`LoadVoxFile`) so path/memory imports flow through one code path.
  - Preserved existing path constructor and default behavior for backward compatibility.
  - Added deterministic in-memory source naming behavior (`sourceName`, default `in-memory.vox`) for root object naming and material collector seeding.
  - Follow-up: add Stage 1 parity tests (path vs bytes) when test harness location is finalized.
- `2026-02-12`: Stage 2 implementation started in `Assets/Scripts/VoxMeshBuilder.cs`.
  - Extracted mesh generation internals from `VoxImporter` into new reusable `VoxMeshBuilder`.
  - `VoxImporter` now delegates optimized/cube mesh generation to `VoxMeshBuilder`.
- `2026-02-12`: Stage 2 completed with regression checks.
  - Added editor regression tests in `Assets/Editor/Tests/TestVoxMeshBuilder.cs` using in-memory `.vox` fixtures.
  - Tests validate mesh parity characteristics (vertex/index counts and bounds) for optimized vs separate-cube modes.
- `2026-02-12`: Stage 3 started with mutable runtime document primitives.
  - Added `Assets/Scripts/RuntimeVoxDocument.cs` with model/palette state plus voxel CRUD/move/color mutation methods.
  - Added conversion entry points `FromVoxFile` and `FromBytes`.
  - Extended `Assets/Scripts/VoxMeshBuilder.cs` with runtime-model mesh generation overloads.
  - Added editor tests in `Assets/Editor/Tests/TestRuntimeVoxDocument.cs` for mutation correctness and runtime mesh building.
- `2026-02-12`: Stage 4 started with minimal VOX serialization support.
  - Added `Assets/Scripts/VoxSerialization/VoxWriter.cs` implementing deterministic `VOX ` v150 output with `MAIN`, `SIZE`, `XYZI`, and `RGBA` chunks.
  - Added `RuntimeVoxDocument.ToVoxBytes()` and round-trip test coverage in `Assets/Editor/Tests/TestRuntimeVoxDocument.cs`.
  - Current writer limitation: single-model documents only (explicit `NotSupportedException` for multi-model docs).
- `2026-02-12`: Stage 5 started with per-call importer options.
  - Added `Assets/Scripts/VoxImportOptions.cs`.
  - Added `VoxImporter.Import(VoxImportOptions options)` overload while preserving legacy `Import()` behavior.
  - Added per-call overrides for mesh mode, material, collider toggle, and root object naming.
- `2026-02-12`: Validation switched to Unity headless batchmode (project-native compile path).
  - Ran Unity `2022.3.62f2` headless compile using project path and captured log at `Logs/unity-headless.log`.
  - Fixed compile blockers introduced in VOX work (`Vector3` ambiguity in `VoxMeshBuilder` and `TestVoxMeshBuilder`).
  - Current status: headless Unity compile exits successfully.
- `2026-02-12`: Stage 3 completed.
  - Added runtime converters `RuntimeVoxDocument.FromStream(Stream)` and `RuntimeVoxDocument.FromBytes(ReadOnlyMemory<byte>)`.
  - Added mutation/runtime coverage tests in `Assets/Editor/Tests/TestRuntimeVoxDocument.cs`.
- `2026-02-12`: Stage 4 completed.
  - Extended `VoxWriter` to serialize multi-model documents (adds `PACK` and minimal scene graph chunks `nTRN`/`nGRP`/`nSHP`).
  - Added multi-model round-trip coverage in `Assets/Editor/Tests/TestRuntimeVoxDocument.cs`.
  - Maintained deterministic chunk ordering and deterministic voxel ordering.
- `2026-02-12`: Stage 5 completed.
  - Added option-default and override tests in `Assets/Editor/Tests/TestVoxImportOptions.cs`.
  - `VoxImporter.Import(VoxImportOptions)` now drives per-call behavior while legacy `Import()` remains backward-compatible.
- `2026-02-12`: Unity validation status.
  - Headless compile command succeeds (`-batchmode -nographics -quit`).
  - Unity `-runTests` invocation in this environment exited without generating the expected `testResults` XML artifact, so compile validation is confirmed but test-runner artifact capture remains unresolved in this workspace.
- `2026-02-12`: Step 7 aligned to actual HTTP command architecture.
  - Added command-style VOX API actions in `Assets/Scripts/API/ApiMethods.Vox.cs`.
  - Replaced REST-style placeholder guidance with concrete `/api/v1?command=params` command contracts.
- `2026-02-12`: Lua API style alignment pass (house-style wrappers).
  - Refactored `Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs` to follow existing Lua wrapper conventions:
    - Removed JSON-string return payloads for `Info`/`MeshStats`.
    - Added typed wrapper objects/list wrappers (`doc.models`, `doc.models[index]`, `doc:FindModel(name)`).
    - Removed custom negative-index handling from VOX Lua wrapper flow.
  - Goal: keep Lua API object/wrapper-centric and independent from HTTP command-index semantics.
- `2026-02-12`: Added usage examples for VOX runtime API surfaces.
  - Added HTTP command example page: `Assets/Resources/ScriptExamples/vox_runtime_api.html`.
  - Added Lua plugin example: `Assets/Resources/LuaScriptExamples/BackgroundScript.VoxRuntimeDemo.lua`.
  - Both examples demonstrate create/edit/palette/mesh/export flows with the current house-style APIs.
- `2026-02-12`: Fixed `vox.mesh.stats` JSON serialization for HTTP flow.
  - Replaced direct `UnityEngine.Vector3` serialization with plain `{x,y,z}` payload objects for `boundsCenter` and `boundsSize`.
  - Prevents Newtonsoft self-referencing loop errors in API responses.
- `2026-02-12`: Added scene instantiation commands for runtime VOX docs.
  - Added `vox.spawn` and `vox.spawn.clear` in `Assets/Scripts/API/ApiMethods.Vox.cs`.
  - Updated VOX HTTP and Lua examples to call spawn APIs so users see visible voxels in-scene after edits.
- `2026-02-12`: Lua API shifted toward interactive scene-first workflow.
  - Updated `Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs` with auto-visual rebuild support (`doc:SetAutoVisuals(...)`).
  - Added scene-first constructors (`Vox:NewScene(...)`, `Vox:ImportSceneBase64(...)`).
  - Added model edit aliases (`Set`, `Remove`, `Move`) for quick scripting ergonomics.
  - Updated Lua example to use `Vox:NewScene(...)` so edits are visible immediately.
- `2026-02-12`: HTTP API shifted toward user-friendly interactive defaults.
  - Added automatic visual refresh for active VOX document edits.
  - Added active-model aliases (`vox.model.select`, `vox.set`, `vox.remove`, `vox.move`) to avoid model-index-heavy calls.
  - Added `vox.autovisuals` toggle for advanced/headless-style workflows.
  - Updated HTTP example to use the user-friendly active-model command flow.
- `2026-02-12`: HTTP example upgraded to interactive playground style.
  - Reworked `Assets/Resources/ScriptExamples/vox_runtime_api.html` to include form fields, buttons, and JS helpers.
  - Added interactive controls for document/model creation, palette editing, voxel set/remove/move, and procedural pattern generation.
- `2026-02-12`: Hooked VOX runtime cleanup into global sketch reset.
  - Added `ApiMethods.VoxResetRuntimeState()` in `Assets/Scripts/API/ApiMethods.Vox.cs`.
  - Called from `SketchControlsScript.NewSketch(bool fade)` so both API `new` and GUI new-sketch flows clear spawned VOX runtime objects/doc state.
- `2026-02-12`: Added `.tilt` persistence for runtime VOX documents using embedded `.vox` subfiles.
  - Save path now writes runtime VOX docs as binary entries (eg `vox/0.vox`) alongside sketch data.
  - Metadata stores lightweight runtime VOX index records (`RuntimeVoxIndex`) with file path + scene transform + mesh flags.
  - Load path restores runtime VOX docs and rebuilds their scene representations from embedded `.vox` entries.
- `2026-02-12`: Verified runtime VOX save/load roundtrip via HTTP in normal Unity runtime.
  - Sequence validated: `new` -> runtime VOX edits -> `save.as` -> `new` -> `load.named` -> `save.as`.
  - Verified both source and post-load-resave `.tilt` archives contain embedded `vox/0.vox` and matching `RuntimeVoxIndex` records in `metadata.json`.
  - Validation note: load is asynchronous; saving immediately after `load.named` can race and miss restored runtime VOX. A short wait/poll is required before follow-up save/assert steps.
  - Environment note: Unity batch/headless mode in this workspace did not expose the HTTP API listener on `localhost:40074`, so HTTP verification was performed against a normal interactive Unity run.

Open limitations:
- VOX serialization currently enforces voxel coordinate range `[0,255]` (matching `XYZI` byte encoding).

---

## 5) Branching and merge strategy

To avoid heavy merge conflicts:

- Use **stacked branches** instead of independent long-lived branches:
  - `vox-runtime-stage1` (based on `main`)
  - `vox-runtime-stage2` (based on stage1)
  - `vox-runtime-stage3` (based on stage2)
  - etc.
- Keep each PR narrowly scoped to one stage.
- Merge quickly once approved.
- Rebase only the top of the stack as needed.

Why this works:
- Stage 2 refactors the hotspot file (`VoxImporter`) and will conflict with almost everything else.
- Later stages can target new files and stable abstractions instead of repeatedly modifying importer internals.

---

## 6) Suggested file-level change map

Implemented core files:
- `Assets/Scripts/VoxImporter.cs`
- `Assets/Scripts/VoxMeshBuilder.cs`
- `Assets/Scripts/RuntimeVoxDocument.cs`
- `Assets/Scripts/VoxSerialization/VoxWriter.cs`
- `Assets/Scripts/VoxImportOptions.cs`

Implemented API integration files:
- `Assets/Scripts/API/ApiMethods.Vox.cs`
- `Assets/Scripts/SketchControlsScript.cs`

Implemented test files:
- `Assets/Editor/Tests/TestVoxMeshBuilder.cs`
- `Assets/Editor/Tests/TestRuntimeVoxDocument.cs`
- `Assets/Editor/Tests/TestVoxImportOptions.cs`

Status:
- `[x]` Step 6 change map is now complete for this VOX runtime/API phase (core, API integration, persistence wiring, and examples are all represented in tracked files).

---

## 7) API endpoint alignment (non-UI integration guidance)

Status: `[x] Updated for current HTTP command model`

Important note:
- This project uses command-style HTTP calls via `/api/v1` query/form parameters, not resource-style REST routes.
- Commands are discovered through `ApiEndpoint` attributes and dispatched by `ApiManager`.
- Commands execute asynchronously via the command queue; callers can poll completion via `query.command=<handle>`.

VOX command contract (implemented):
1. `vox.new=sizeX,sizeY,sizeZ`
2. `vox.select=index`
3. `vox.delete=index`
4. `vox.info[=index]` (default: active doc)
5. `vox.autovisuals=true|false`
6. `vox.model.add=sizeX,sizeY,sizeZ[,name]`
7. `vox.model.select=index`
8. `vox.set=x,y,z,paletteIndex` (active model)
9. `vox.remove=x,y,z` (active model)
10. `vox.move=fromX,fromY,fromZ,toX,toY,toZ[,overwrite]` (active model)
11. `vox.voxel.set=modelIndex,x,y,z,paletteIndex` (advanced/indexed)
12. `vox.voxel.remove=modelIndex,x,y,z` (advanced/indexed)
13. `vox.voxel.move=modelIndex,fromX,fromY,fromZ,toX,toY,toZ[,overwrite]` (advanced/indexed)
14. `vox.palette.set=paletteIndex,r,g,b[,a]`
15. `vox.mesh.stats=modelIndex[,optimized|cubes]`
16. `vox.spawn[=optimized|cubes[,generateCollider]]`
17. `vox.spawn.clear`
18. `vox.export.base64`
19. `vox.import.base64=base64`

Lua plugin API alignment (implemented):
- Added `Vox` Lua wrapper class: `Assets/Scripts/API/Lua/Wrappers/VoxApiWrapper.cs`
- Registered in Lua runtime via `LuaManager.RegisterApiClasses` as `Vox`
- Lua API is intentionally object/wrapper-oriented (house style), not command/index-oriented:
  - `local doc = Vox:New(16,16,16)`
  - `local sceneDoc = Vox:NewScene(16,16,16,true,true)`
  - `local model = doc.models[0]`
  - `local byName = doc:FindModel("model_0")`
  - `model:SetVoxel(1,2,3,5)`
  - `model:Set(2,2,2,6)` (alias)
  - `doc:SetAutoVisuals(true, true, true)`
  - `local stats = model:MeshStats(true)`
  - `doc:Spawn(true, true)`
  - `local b64 = doc:ExportBase64()`

Example request style:
- `GET /api/v1?vox.new=16,16,16`
- `GET /api/v1?vox.voxel.set=0,1,2,3,5`
- `GET /api/v1?vox.export.base64`

Behavior notes:
- Document state is held in-process by API static storage (`ApiMethods.Vox`).
- Multi-command batching still follows existing `&` command chaining behavior.
- For long-running flows, keep existing handle polling model (`query.command=<handle>`).

---

## 8) Testing strategy per stage

### Stage 1 tests
- Load known `.vox` from path and from equivalent in-memory bytes.
- Assert same model count and warnings shape.
- Assert importer does not throw for empty model lists.

### Stage 2 tests
- For fixed test model data, compare mesh outputs before/after refactor:
  - Vertex/triangle counts
  - Bounds
  - Optional checksum of generated index buffers
- Validate both mesh modes.

### Stage 3 tests
- Mutation correctness:
  - Add then remove returns original state.
  - Move updates occupancy correctly.
  - Palette changes propagate to generated voxel colors.
- Duplicate position behavior is deterministic.

### Stage 4 tests
- Round trip:
  - Load fixture `.vox` -> runtime doc -> serialize -> reload -> compare voxel/palette counts and key sample values.
- Generated-from-scratch doc serializes and reloads correctly.

### Stage 5 tests
- Option defaults match legacy behavior.
- Explicit options override defaults.
- Collider toggle and material override are honored.

Performance checks (recommended):
- Large voxel dataset mesh rebuild time before/after Stage 2.
- Memory profile for repeated edit + rebuild loops.

---

## 9) Key risks and mitigations

1. **Coordinate system drift (import rotation vs runtime edits)**
   - Risk: runtime document coordinates don't match current display orientation.
   - Mitigation: define one canonical coordinate space and centralize transforms.

2. **Behavior drift during mesh extraction**
   - Risk: visual output changes subtly after moving code from importer to builder.
   - Mitigation: golden-mesh regression checks (counts/bounds/checksums).

3. **Serialization subset mismatch**
   - Risk: exported files omit chunks needed by downstream tools.
   - Mitigation: document supported subset and validate with sample MagicaVoxel files.

4. **Long-lived branch conflicts**
   - Risk: multiple branches changing `VoxImporter` collide heavily.
   - Mitigation: stacked PR sequence and fast merges.

5. **Repeated allocations during runtime edits**
   - Risk: rebuild loops become expensive.
   - Mitigation: separate mutable data from scene graph and consider mesh/object reuse later.

---

## 10) Definition of done (overall)

The full initiative is complete when:
- VOX can be loaded from bytes/stream without filesystem dependency.
- Runtime voxel document supports CRUD-style edits and palette manipulation.
- Mesh can be rebuilt after edits without re-importing from path.
- Edited/generated content can be serialized to valid `.vox` bytes.
- Import/build behavior is configurable per call through options.
- Backward compatibility for existing path-based import remains intact.
- Automated tests cover parse/edit/build/serialize and round-trip behavior.

---

## 11) Practical next action list for whoever picks this up

1. Create Stage 1 branch and implement stream/bytes constructor overloads in `VoxImporter`.
2. Add minimal tests proving path vs bytes parity.
3. Open Stage 1 PR and merge.
4. Create Stage 2 stacked branch and extract `VoxMeshBuilder` from importer internals.
5. Add mesh regression checks and merge.
6. Implement `RuntimeVoxDocument` + converters.
7. Implement VOX writer and round-trip tests.
8. Add options object and endpoint-oriented call signatures.
9. Run full test suite and sanity-check performance with medium/large VOX fixtures.

---

## 12) Notes for future cleanup (optional after MVP)

- Consider splitting Unity-facing importer assembly from pure data/serialization assembly for easier server-side reuse.
- Add binary compatibility tests against representative MagicaVoxel sample files.
- Add diff-friendly debug export (JSON snapshot of runtime document) for API troubleshooting.
