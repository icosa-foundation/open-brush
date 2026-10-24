MAQUETTE AND SHAPESXR DOCUMENT IMPORT FEATURE MATRIX
2026-10-24. Requirements and candidate mappings; not a claim of completed support.

BROAD FEATURE SUMMARY

This is the high-level scope. The numbered detail table below expands these areas.
Similar entries share an import concern; they are not necessarily equivalent features.

| ID | Broad feature | Maquette | ShapesXR | Common Open Brush requirement |
| --- | --- | --- | --- | --- |
| 1 | Native document loading | Project folders / .mqp archives and JSON sections. | Native MessagePack spaces, usually obtained with a share code. | Separate readers, common import model. |
| 2 | Objects and groups | Models, object instances and nested state trees/groups. | Objects, instances, groups and parent relationships. | Preserve identity, placement, hierarchy, visibility and locks. |
| 3 | Procedural shapes | Rounded rectangles, frames, cylinders/cones, conics and capsules. | Editable panels/boxes with rounding, chamfer and bends; presets include spheres, tubes and tori. | Shared geometry recipes and Polyhydra generation; similar preset names do not imply equivalent constructions. |
| 4 | Imported models | External models, built-in assets and mesh-backed drawings. | External models, library assets and resizable source meshes. | Resolve assets, retain mesh/material data and preserve editable resizing where supported. |
| 5 | Drawing and strokes | Flat ribbons and mesh-backed hull/volume/tube drawings. | Brush strokes, ribbons/tubes and hull drawings. | Map matching strokes; retain source geometry where a brush conversion would lose appearance. |
| 6 | Images | Image objects. | Image objects and imported Figma visuals. | Reuse image support while preserving dimensions, aspect, appearance and source references. |
| 7 | Text | Editable text with font and formatting state. | Editable text with font/style/alignment/fitting state. | Reuse text support; preserve formatting and report font/layout differences. |
| 8 | Materials and appearance | Colour, transparency, brush/model materials and textures. | Colour, PBR materials, textures and additional visual effects. | Map supported appearance; preserve unsupported shader/effect intent. |
| 9 | Lighting and environment | Light objects, sun, gradient, fog, floor and environment settings. | Environment/skybox, sun/shadows and baked-lighting/reflection settings. | Map related scene settings; do not assume identical lighting systems. |
| 10 | Scenes and animation | Multiple scenes with saved states; switching between scenes is instant, with no tweening in the tested project. Internal data distinguishes slides and stages. | Scenes/frames, snapshots and animated transitions. | Resolve a chosen state and retain the rest; support discrete Maquette scene switching and ShapesXR animated transitions separately. |
| 11 | Navigation and links | Hotspots / viewing destinations. | Viewpoints, frame navigation and space links. | Map destinations and supported links, potentially through bookmarks/portals. |
| 12 | Interactive prototypes | UI controls and scripting facilities. | Trigger/action interactions, Figma prototypes and play-mode physics. | Preserve behaviour descriptions; implementing playback is separate from displaying the document. |
| 13 | Audio, video and recordings | Native saved user audio/video objects were not confirmed. | Audio, YouTube content and Holonote/avatar recordings. | Import supported media and retain unsupported references; do not infer Maquette media support from application playback. |
| 14 | Mixed reality and attachments | Anchors and AR simulation settings. | Spatial anchors, passthrough and body/viewpoint attachments. | Retain reference frames and intent; hardware/runtime behaviour may need separate support. |
| 15 | Saving back to .tilt | Imported geometry, scene state and unsupported source data. | The same, including retained states, media and behaviour data. | One persistent representation; no silent loss on save/reload or routine duplicate mesh fallback. |

1. HOW TO READ THE TABLE

Each numbered row aligns matching or related requirements. Shared means both sources
need that capability, not identical serialization or proven visual equivalence.
Related means similar results with materially different source semantics.
Specific means confirmed for only one source. Not confirmed does not mean impossible.

The scope is loading native documents, preserving appearance and data, and progressively
retaining editability/behaviour through .tilt save/reload. A faithful import does not
require reproducing either application's authoring UI or collaboration service.

Evidence: Maquette installed-application/native-library audit; ShapesXR Unity plugin
V1.31, two older public spaces, the user's version-5 space, and the official documentation
listed below. The user loaded the generated Maquette scene-switch probe and confirmed
instant switching between scenes 1 and 2, with no tweening (2026-10-23).
Presence in a saved-property model is distinguished from tested execution.
ShapesXR frames/states and Maquette scenes/slides/stages are analogous, not interchangeable.

The Open Brush column describes work required or reusable foundations. It is NOT a
completed/not-completed checklist. Polyhydra's new recipe, Panel and Bend support is
implemented locally. The checkpoints below distinguish verified compilation/tests
from still-unverified source mesh/shading parity. Native readers exist in `Assets/Scripts/Import`: Maquette directory/ZIP
sections, entity lookup and scene-state trees; ShapesXR MessagePack edit-log replay
and explicit snapshot-property resolution. Standalone C# checks read all 174 installed
Maquette projects (262 scenes, 120,580 states) and all three local ShapesXR fixtures;
the version-5 fixture resolves 10 procedural snapshots. Ten focused reader tests pass
in the standalone harness. This is not Unity compilation or rendered import validation.
The 3D Shapes generated-model/widget/definition-index foundation has now been ported
and adapted to the shared recipe encoding, together with the text branch's cloning
and layer-save fixes. The editable-instance DTO round-trip passes in a narrow standalone
check using production JSON converters. Checkpoint 17 now covers live Maquette widget
creation and .tilt save/reload; equivalent ShapesXR runtime validation remains pending.
Unversioned ShapesXR state migration is not yet implemented; version 3 initial
views and version 4/5 snapshots are supported by the reader. ShapesXR runtime conversion
now has initial-state panel, text, referenced-model, image, Tape-stroke and hull paths; other presets
and full material mapping remain outstanding. Maquette runtime conversion has procedural, text, local model, image, ribbon-stroke and point/spot light paths;
the remaining entity types are reported as unsupported. Animation and scene/frame
switching are deferred until the other supported
content is complete. The full 3D Shapes authoring UI is not included.

Implementation checkpoint, 2026-10-24:

1. Maquette resource resolution now covers bundled files, archive streams, installed
   content, user libraries, relative dependencies and legacy author-machine paths.
   Version normalization resolves older shape names using the user's installed
   primitive-path map. All 67,751 checked local model/image references resolve in the
   installed corpus. This excludes gallery/network references and proves file
   resolution, not model loading, material fidelity or rendered appearance.
2. The frame adapter distinguishes pre-21 outward borders and their opposite
   scale-border flag meaning. Builder scales multiply ancestor local scale components;
   they are separate from matrix-derived scale. Placement retains hierarchy shear
   through ordered shared recipe transforms and a rigid widget pose. All 120,580
   checked structural parent links match their saved parent-state references.
3. Existing text widgets now save optional authored layout: font fitting, style,
   alignment, wrapping, rich text, rectangle size/pivot and an intrinsic glyph affine
   transform. Legacy image-style text stretch is captured when saving or cloning.
   The existing text widget and text index remain the persistence representation.
   No vendor font assets are included.
4. Native text conversion uses selected-state text before the TextContents fallback.
   The shipped Maquette prefab has a 10,000-unit initial autosizing ratio, font size
   limits 1 to 10,000, a centered text rectangle inside a text field offset by
   (0.5, -0.5), and a text-area Z offset of -0.001. These measured prefab settings
   differ from the ratio of 100 in the script's field initializer. The adapter maps
   the box and glyph placement; all 910 checked native text states convert without
   missing fallback files. Font substitution and actual TMP rendering remain unverified.
5. Twenty-four focused reader/resource/adapter tests pass in the standalone harness.
   Text layout round-trip and validation pass using production sketch JSON converters.
   Separate compiler invocations for the current data assembly and main application
   sources pass against the existing dependency assemblies, without Unity analyzers.
   This does not prove a fresh Unity compilation, Play-mode execution, widget creation,
   rendering, save/reload or end-to-end native import behavior. The old USD namespace
   error is stale relative to the current cached source, which already has the corrected
   namespace. The agent-owned headless Editor is alive but the CLI currently reports
   it as unreachable; runtime validation has not been recovered.
6. `import.maquette` now connects procedural, text, local model and image conversion to existing widget
   creation paths. It imports the document's selected state at authored placement,
   maps immediate parent groups and locks, and places hidden objects on a hidden layer.
   Text can use the user's installed Segoe UI Semibold font; unavailable fonts are
   reported as substitutions. The returned report counts unsupported object kinds,
   unmapped procedural materials and individual failures. The API command has not
   been exercised in Play mode. Flat drawing strokes and other supported Maquette
   content remain to be wired up; subsequent checkpoints cover ShapesXR conversion. This is a partial
   implementation, not a finished or visually verified importer.
7. Model import now runs asynchronously; `import.status` returns its running/final report.
   Archives are staged once with relative sidecars intact, while existing local files
   are read in place. Persistent glTF model variants share source buffers and textures
   and add intrinsic transform nodes. Ordinary local model widgets and the existing
   model index provide persistence. glTF/GLB source metadata, including unknown fields
   and material extensions, is retained by the cache; all 1,402 installed glTF/GLB files
   pass its standalone copy check. HTTP/data URIs remain references rather than being
   downloaded by this cache step. Other model formats use the existing model loaders
   and a one-time glTF conversion; that Unity-dependent conversion remains untested.
8. Imported media can explicitly skip the model intro animation and use authored poses.
   Model imports track the current loader's units per metre without changing existing
   loading behavior: UnityGLTF retains metre units while the FBX/legacy loaders convert
   to canvas units. Native placement compensates for this difference. The pre-v9 model
   origin migration moves the widget handle while preserving visible placement. These
   runtime scale/origin paths still need verification in Unity.
9. Image import now stores texture files in the ordinary reference-image library and
   uses the existing image index. An optional per-instance layout retains intrinsic
   affine geometry, RGB tint and blend mode; alpha remains in the existing opacity
   array. Aspect is taken from Maquette's saved scale instead of applying the texture
   aspect a second time. Source height remains the ordinary widget size. All 1,373
   checked image states convert through the adapter. Of these, 689 have RGB tint,
   239 select Additive, and 709 require the pre-v20 default-material migration to
   TranslucentUnlit. Existing default cutout image rendering remains the default.
   Alpha-blended, additive and opaque unlit modes are implemented; Lit, TranslucentLit
   and EdgesLight images remain flagged as unmapped appearance (13 checked states).
   Native redundant front/back image mesh layers are represented as an ordinary
   two-sided plane, not copied vendor geometry.
10. Image layout/opacity index alignment and validation pass standalone production-JSON
    checks. Image duplication carries layout/two-sided state, and the export paths
    account for tint, opacity and additive blending. Media save coordinates now use
    the authored layer when selected; image/model restoration selects the destination
    layer before assigning saved local coordinates. These widget, shader, export and
    layer-restoration paths compile but still require Unity rendering and save/reload
    validation. No such runtime result is claimed by the adapter or JSON checks.

11. `import.shapesxr` now imports the first state of the first ordered source scene.
    Procedural panels use the shared recipe and existing editable-model widgets,
    with hierarchy transforms, visibility, grouping and locks. Color and opacity
    are read from saved material entities; the import reports partial appearance
    support while source shading, textures and secondary slots remain unmapped.
    The supplied version-5 space resolves 10 panel recipes with no data-conversion
    failures. The standalone data suite passes 27 tests, and both importer and
    main C# sources compile against cached Unity references. Live widget creation,
    rendering and save/reload are unverified. No playback or state-switching
    feature is included; the text and 3D Shapes branches supply the shared widgets,
    while animation-branch integration is deferred.

12. ShapesXR text now uses the existing text widget, including TMP styles/alignment,
    fixed-size versus auto-fit anchoring, the source box size and full hierarchy
    transforms. Its font GUID resolves to Liberation Sans already included in Open
    Brush; no ShapesXR font asset is copied. Saved resource-path font references
    can now reload through the existing widget API.
13. Referenced ShapesXR assets are fetched from the public resource endpoint, staged
    once per resource, and copied into ordinary persistent image/model libraries.
    Images retain the separate source aspect ratio and hierarchy scale. Models
    retain their authored origin and source glTF appearance, with only the highest
    available named LOD connected for each converted LOD group. Model overrides
    and full image/panel material appearance are reported as incomplete.
    A real image manifest and a converted 5.2 MB GLB were fetched from an older
    public space. The GLB cache check disconnects 12 alternate LOD nodes and keeps
    two renderable mesh nodes without duplicating its binary payload.
    Standalone checks pass 34 data tests; current importer and main sources compile
    against cached Unity references. These checks do not exercise Unity networking,
    widget creation, visual parity or .tilt save/reload. The agent-owned Editor is
    alive but default CLI routing failed at this checkpoint; checkpoint 17 records
    the later live validation. Animation and scene/frame switching remain deferred.

14. Maquette point and spot lights now use the existing light-widget path. Conversion
    preserves color, intensity, world-space radius, spot angle, switched-off state
    and hard-shadow enablement. All 273 installed light states convert, including
    144 spotlights, 12 disabled lights and 58 shadowless lights. Light widgets now
    initialize missing light/gizmo components and preserve their values when cloned
    or restored. Optional `TiltLights.Enabled` and `TiltLights.Shadows` fields keep
    the enabled/shadow states independently of intensity; absent fields retain
    legacy defaults. Point lights no longer require absent spot-cone fields, and
    light save/load uses the destination layer's local coordinates consistently.
    The production-JSON DTO check passes; live light rendering, interaction and
    complete sketch save/reload remain unverified.
15. ShapesXR materials now resolve selected-state snapshots as well as their base
    records. Eight of the ten panel materials in the supplied space have different
    color/opacity in their snapshots. Initial-view composition includes background
    objects using their own state. Version 3 property names and per-object scene
    ownership are normalized without changing the original records or introducing
    frame navigation. The older public sample resolves two panels, two referenced
    assets and one background object; the current sample still resolves ten panels.
    Standalone data checks now pass 37 tests and cached-reference source compilation
    passes. Subsequent checkpoints cover Maquette master-stage composition and live
    validation of the implemented Maquette paths.
16. Static Maquette slide views now include master-stage content. The slide's state
    wins for shared entity identities, and parent references resolve through the
    composed hierarchy without changing the original source records. Environment
    inheritance is independent and remains outstanding. The installed Disco slide
    resolves 153 distinct entity states rather than 89 slide-only states, retaining
    all eight point/spot lights. A regression test also exercises a selected group
    transform overriding the parent of a stage-only entity.
17. A graphical Unity 6000.6 Editor compiled the current import implementation and
    passed 65 import-focused EditMode tests. An authored archive fixture then created
    five procedural families, one text widget, two models, two image placements and
    three lights, including a hidden image and a disabled pinned spotlight. All 13
    widgets imported with zero object failures and no font substitution. Its report
    correctly remains partial for five unmapped procedural `Unlit` materials.
    Live testing found and fixed Unity missing-component null checks, absolute image
    cache paths, glTF importer stream disposal and text outline colors being reset
    by font assignment. The saved .tilt reloaded all 13 widgets; comparison of geometry
    definitions/instances, models, images, text, lights and layers found zero differences
    at a numeric tolerance of 1e-6. Two model variants share one binary buffer file.
    A manually framed thumbnail renders the generated geometry; this is not a native
    Maquette screenshot comparison or proof of complete material/lighting parity.
    The owned test Editor and generated library/temp files were cleaned up; ignored
    validation artifacts remain under `Support/DocumentImportAnalysis/RuntimeFixture`.
    Brush strokes, remaining material/appearance mappings, environment/navigation and
    other outstanding content still require work. Animation and scene/frame switching
    remain deferred.

18. ShapesXR image materials now select existing opaque, alpha-blended and additive
    image modes from texture-alpha mode, blend mode, opacity and object visibility.
    Opaque materials ignore stored color alpha; text retains its separate opacity
    semantics. Missing or empty material IDs use source defaults. Multiply blending,
    lit images, passthrough, differing alpha-mask thresholds and remaining material
    properties are reported as incomplete. Forty-two standalone data checks and
    cached-assembly source compilation pass; this material update has not yet had
    fresh Unity compilation or live import validation.

19. Maquette environment loading now resolves the selected slide's lighting or the
    master stage's complete lighting record when the v17 inheritance switch is on.
    Sun color, intensity and canvas-relative orientation map to the existing Open
    Brush environment-light controls and HDR light metadata. The other directional
    slot is cleared; the shadow toggle selects the shadowed or unshadowed slot.
    Pre-v12 slide intensity and pre-v13 shadow defaults follow source migrations.
    Background, fog, floor, ambient/reflection lighting and soft-shadow differences
    remain explicit entries in `UnmappedEnvironment`; malformed or unavailable sun
    conversion is reported without preventing object import. No new .tilt fields
    were added. The reader accepts 262 installed scene environment records; 45
    standalone data checks and cached-assembly compilation pass. This environment
    change still needs fresh Unity compilation and live import/save/reload checks.

20. Live graphical Unity validation now covers the environment sun and ShapesXR
    panels, images and text. The Maquette sun matched the expected HDR radiance and
    direction on a rotated canvas; its saved/reloaded `Lights` metadata was identical.
    The supplied ShapesXR space imported ten panels without object failures and
    reloaded identical geometry definitions/instances, layers and lights. A second
    local space imported two referenced images and two panels without failures.
    An independently authored native-space fixture added fixed-size and auto-fit
    text without font substitution, preserving separate opacity despite zero saved
    color alpha. Saving/reloading that combined scene retained both images, both
    texts and both panels with identical geometry, image, text, layer and light
    metadata. These image cases exercised alpha blending; standalone adapter tests
    cover the other mapped modes. No rendered source comparison or ShapesXR model
    import is claimed. Material properties remain partial. The previously reported
    unsupported object in the second space is the native Group preset; it is now
    recognized as a container while its children retain placement and grouping.
    A fresh Unity recompile including that fix passed 73 import-focused EditMode
    tests. The actual sample group was also resolved live as a container with
    three children. Standalone data checks now total 46, with zero failures.

21. Maquette straight and curved ribbon import now uses ordinary Open Brush strokes
    and the existing Quill ribbon brush, with no new brush or .tilt fields. Straight
    drawing retains its first width and final edge orientation. Curved drawing samples
    both cubic edges independently at the native five subdivisions per span. Mesh
    recentering, construction rotation, nested transforms, varying width/orientation,
    groups and hidden layers are retained. A live authored fixture matched both
    ribbons' boundaries within 0.0000031 metres and all 22 nondegenerate source
    triangles, including the diagonals of nonplanar quads. This is geometry validation,
    not a source-rendered material comparison. Lit/other ribbon materials, differing
    alpha blending and per-stroke locking remain reported gaps. Fresh Unity compilation
    and 76 focused tests pass. The installed corpus currently decodes 13,342 stroke
    states; 2,162 pre-v4 straight-stroke states still need their rotation migration.
    The largest decoded ribbon has 2,511 samples, within the existing brush capacity.
    Live .tilt stroke save/reload validation is still outstanding at this checkpoint.

22. The pre-v4 straight-ribbon frame migration is now handled without modifying
    source state records. Native migration applies a construction correction to
    each state and its inverse to the mesh; retaining the old centered points and
    saved state is their equivalent composition. Regression inputs exercise v2,
    v3 and current frames under a nonidentity saved rotation. All 15,504 installed
    stroke states now decode without errors, including the 2,162 previously rejected
    legacy states. This corpus check does not execute or compare Maquette rendering.
    The live authored two-ribbon fixture also saved/reloaded unchanged control-point
    positions, rotations, pressure, timestamps, brush identity/size/scale, colors,
    grouping and visibility. Rebuilt geometry still matched the same 22 nondegenerate
    triangles and boundary tolerance after reload. Source shading parity and the
    reported material/locking differences remain outstanding. Unity compiled the
    final legacy-frame change and passed all 76 focused EditMode tests; the standalone
    data suite passes 49 checks.

23. ShapesXR Tape brush import now consumes the native object's static position,
    rotation and width arrays and produces ordinary Open Brush ribbon strokes. The
    Tape half-width is in centimetres while positions are in metres. Per-point
    orientation/width, the selected state's placement, nested nonuniform transforms,
    grouping and hidden layers are retained. Snapshot fields do not replace the
    static brush sample stream. Other brush/hull preset identities are recognized
    and reported by name; Marker, Leaf, Oil, Square, Tube and hull generation are
    still outstanding rather than substituted with Tape. No preset asset payloads,
    new brush types or .tilt fields were added.
    A live authored fixture imported two tapes and one API-referenced ShapesXR model
    with zero object failures, reporting its deliberately unsupported Marker and
    incomplete appearance mappings. Both tapes matched all eight source triangles
    and boundary points within 0.0000004 metres, before and after .tilt reload. Their
    saved stroke fields were identical, including colors, pressure, orientation,
    grouping and visibility. The model reloaded all 4,284 selected-LOD vertices;
    `ModelIndex` and layer metadata were identical. This verifies loading/persistence,
    not source-rendered appearance. Fresh Unity compilation passed 82 focused
    EditMode tests; standalone data checks total 55 with zero failures.

24. ShapesXR hard and smooth hulls now use the existing Open Brush `HullBrush` with
    two hidden compatibility configurations. Point conversion consumes saved `p`
    coordinates directly; the usual rounded-volume inflation and simplification
    are disabled. No vendor mesh, new generator or .tilt field was added. The native
    hull importer does not use preset scale/profile values to expand this cloud.
    Source hierarchy transforms, hard/smooth intent, grouping and hidden layers
    are retained in ordinary strokes. The smooth normal calculation remains a
    fidelity gap: Open Brush uses angle-weighted normals, while the native importer
    invokes Unity normal recalculation in the original object coordinates. The
    runtime result reports `ShapesXR smooth hull normals` in addition to incomplete
    material appearance. An irregular tetrahedron under nested nonuniform transforms
    demonstrated a maximum normal difference of about 56 degrees; no shading match
    is claimed.
    Live import created both hulls with zero failures. Each retained its four extreme
    vertices without inflation or emitting the interior point, four triangles and
    the analytically expected volume. Hard mode emitted twelve face vertices and
    smooth mode four shared vertices. Saving/reloading retained identical stroke
    records, visibility and grouping, and reproduced the same vertices and volumes.
    Unity compiled the implementation and passed 83 focused EditMode tests; 56
    standalone data checks pass. Marker, Leaf, Oil, Square and Tube strokes remain
    outstanding.

25. Maquette ribbon material classification now follows the source loader's
    pre-v18 migration. Pre-v11 `OpaqueLit`/`OpaqueUnlit` names and later
    `Brush_DefaultLit`/`Brush_DefaultUnlit` names resolve through the stroke
    definition; modern files retain the state's material ID. This avoids false
    unknown-material reports for legacy Unlit ribbons and correctly reports legacy
    Lit ribbons as still needing shading support. Source JSON remains unchanged.
    Fifty-seven standalone checks and cached-assembly source compilation pass;
    this reporting/conversion update has not had fresh Unity runtime validation.

26. Maquette fog now maps to Open Brush's existing custom-environment fog fields.
    The shipped Maquette scene uses Unity exponential fog (mode 2); Open Brush's
    environment implementation fixes its fog to the same mode. Density conversion
    accounts for metres-to-canvas units and the destination layer's scale relative
    to the scene; Open Brush applies scene scale when setting the renderer density.
    The source's v13 toggle migration and master-stage environment inheritance are
    respected. Fog parsing is independent of sun parsing, so invalid fog does not
    discard valid sunlight. No .tilt field was added.
    Disabled fog maps to zero density. A nonzero source density retained behind its
    disabled toggle is reported as unrepresented, and color precision lost through
    existing Color32 metadata is also reported. An active source fog on a backdrop
    with fog disabled is reported without changing its density; the importer does
    not silently change the backdrop to enable it.
    All 262 installed scene environments parse with fog, including 31 with active
    fog. Fifty-nine standalone checks and 86 fresh Unity EditMode tests pass. A live
    fixture with scene scale 3 and canvas scale 6 matched the source's attenuation
    coefficient, color and exponential mode. Saving/reloading retained identical
    environment, layers and lights metadata and reproduced the same runtime fog
    values. This verifies state and attenuation math, not source-rendered pixels.

27. ShapesXR import now reports interactions attached to the selected state and its
    background state through `UnsupportedActions` in `import.status`. Sound playback,
    navigation, look-at, haptics and state transitions are named separately. Malformed
    interaction data produces a partial import report while object import continues.
    This reads metadata only: it does not execute actions or import playable audio.
    The three cached spaces have no saved interactions; authored fixtures verify
    state scoping, union validation and isolation from source property data. Sixty-one
    standalone checks and cached-assembly source compilation pass. No fresh Unity
    runtime validation was performed for this reporting change.

28. A standalone audit of the 174 installed Maquette project directories converted
    all 2,728 saved procedural states to validated shared recipes, including hierarchy
    linear transforms: 1,344 cylinders, 994 rounded rectangles, 280 frames and 110
    conic sections. There were zero conversion failures. These are state instances,
    not necessarily distinct shape definitions; the corpus contains no capsule
    states. This checks recipe conversion and validation only. It does not evaluate
    Polyhydra meshes, compare source geometry or establish visual parity.

29. ShapesXR model object opacity (`obj-vis`) now maps to the cached model's glTF
    material alpha, preserving material slots, textures and source metadata. Opaque
    source materials ignore their saved alpha before the fade; blended/masked
    materials multiply it by object opacity. Primitives with implicit default
    materials receive an explicit faded default. Full opacity leaves source materials
    unchanged, and the cache distinguishes instances with different opacity.
    No .tilt field was added. Material-entity overrides remain outstanding and reported.
    Sixty-two standalone checks and 89 fresh Unity EditMode tests pass. A live fixture
    placed the same resource at opacity 1, 0.25 and 0. All three retained 4,284 vertices
    and separate cached variants; runtime alpha matched, and faded materials used
    transparent render queues. Saving/reloading retained identical model/layer and
    environment/light metadata and reproduced identical material/geometry records.
    This checks runtime state and persistence, not source-rendered pixel parity.

30. ShapesXR model material-entity overrides now map through `mid`, `mid-1`, etc.
    Slots follow visible mesh groups sorted by the converted nodes' `original_index`,
    after choosing their highest-detail LOD. Material properties resolve against
    each object's selected/background state. Supported overrides are color, opacity,
    texture-alpha mode, metallic/roughness factors and PBR/unlit shading. Absent fields
    inherit the source material. Textures, other material data and geometry buffers
    are retained; assigning a material to one node clones mesh metadata so other
    instances sharing the source mesh remain unchanged. Cached variants distinguish
    material assignments and use the existing .tilt model-reference representation.
    Additive/multiply blending, passthrough, explicit alpha-mask threshold parity,
    specular-glossiness overrides and other unsupported material fields remain reported.
    Sixty-three standalone checks and 90 fresh Unity EditMode tests pass. A live
    fixture imported an original model and two swapped two-group overrides, including
    a color supplied by the selected state's material snapshot. All six groups had
    expected colors, alpha, shader types and PBR factors; all models retained 4,284
    vertices. Save/reload reproduced identical runtime material/geometry records and
    model/layer/environment/light metadata. No source-rendered pixel parity is claimed.

31. Maquette model imports using its own material mode now map the opaque Lit,
    Unlit, Matte, Metallic and Plastic presets to independent standard glTF materials.
    Default/empty IDs select Lit. Opaque presets ignore entity alpha; metallic and
    roughness factors follow the shipped preset settings. Uniform entity appearance
    no longer multiplies old mesh vertex colors. Original-material imports retain
    their materials and vertex colors. No vendor shader/source/asset is shipped.
    Translucent, additive, edge and other effects remain reported. Explicit disabled
    model shadow casting is separately reported because it cannot currently be retained.
    Sixty-four standalone checks and 91 fresh Unity EditMode tests pass. A live
    seven-model fixture verified all five presets, default Lit, and Original behavior,
    including color-space conversion, opaque/blended queues and vertex-color handling.
    Save/reload reproduced identical material/geometry records and model/layer metadata.
    This verifies state and persistence; it does not establish pixel parity with
    Maquette's custom lighting shaders.

2. FEATURE COMPARISON

| ID | Area | Feature | Maquette | ShapesXR | Match and required Open Brush support |
| --- | --- | --- | --- | --- | --- |
| 1 | Documents | Native input container | Project directory or .mqp ZIP; several JSON sections and asset folders. | Downloaded native MessagePack space data; plugin resolves a share code to a download. | Related: separate readers feeding one normalized import model; GLB exports are not substitutes for native documents. |
| 2 | Documents | Format revisions | Older JSON versions and version-specific compatibility branches. | Header/version and migration chain; fixtures include legacy data, schema 3 and schema 5. | Shared: version-aware decoding and normalization before feature mapping. |
| 3 | Documents | Object identity and references | Entity GUIDs separate from scene-state GUIDs. | Entity IDs, creation records, snapshots, state IDs and resource/preset IDs. | Shared: resolve references and keep identities distinct; do not confuse state, geometry and resource identifiers. |
| 4 | Documents | Object definitions versus placed state | Definitions referenced by state trees. | Objects/presets referenced by snapshots and typed properties. | Related: resolve selected state into shared definitions/instances without treating a preset as the final object. |
| 5 | Documents | Asset dependency resolution | Objects, Images, TextContents and tokenized or legacy paths. | Resource IDs, downloaded assets and built-in preset references. | Shared: resolve all dependencies, preserve relative relationships and report missing resources. |
| 6 | Documents | Built-in library dependencies | Content-library model/material references, including shipped assets. | Preset GUIDs refer to plugin assets or procedural defaults. | Related: generate analytic objects from resolved parameters; resolve genuine mesh resources rather than retaining proprietary installation dependencies. |
| 7 | Documents | Remote model identifiers | Legacy Google Poly IDs and remote-asset loading paths. | Cloud resource IDs and share-code transport. | Related: retain source identity and resolve through the correct resource adapter; do not equate identifier namespaces. |
| 8 | Documents | Partial or unfamiliar documents | Optional/missing sections and legacy properties. | Unfamiliar edits, property types, versions or presets. | Shared: retain unsupported data and report per-object failures instead of silently omitting content. |
| 9 | Transforms | Units and ordinary placement | Position, quaternion and source scale conventions. | Local transform records plus geometry dimensions and object size. | Shared: define units/axes and avoid preview normalization changing authored dimensions. |
| 10 | Transforms | Nonuniform scaling | Three-axis scales, sometimes affecting procedural regeneration. | Three-axis transform/size plus separate procedural dimensions. | Related: retain shape-affecting transforms in the recipe/import representation; no new VR scaling UI is required. |
| 11 | Transforms | Mirrors, pivots and orientation | Signed scales, source pivots, rotations and loading bounds. | Local frames, preset pivots and resource bounds. | Shared: preserve pivots, winding and normal transforms; mark singular cases explicitly. |
| 12 | Transforms | Nested hierarchy and groups | State trees and named nested groups. | Parent/link records and group presets. | Related: retain hierarchy and names; existing flat Open Brush group IDs alone are insufficient for all nesting semantics. |
| 13 | Transforms | Instances and shared objects | Definitions can be reused across scene states. | Object IDs, instance IDs and snapshots can reference shared sources. | Shared: preserve references without accidentally linking all independent edits or duplicating every geometry source. |
| 14 | Transforms | Visibility and active state | Per-state visibility. | Active state and per-snapshot visibility, including interpolated visibility. | Related: preserve hidden objects and distinguish absence, disabled state and opacity. |
| 15 | Transforms | Editing locks and placement constraints | Edit locks and group-related editing state. | Lock properties and transform constraints on imported objects. | Related: retain restrictions separately; do not assume a source lock means exactly the same thing as Open Brush pinning. |
| 16 | Geometry | Fixed and imported model meshes | ModelFile primitives; model picker supports more source formats than GLB alone. | Model resources and fixed library objects; documented OBJ/GLB/glTF imports. | Shared: reuse ModelWidget/import paths where possible, with source-format conversion, bounds, subtrees and dependencies. |
| 17 | Geometry | Procedural data and evaluation | Five exposed builder families with named source parameters. | One shared parametric panel construction with editable properties; several named presets. | Related: one generator-plus-ordered-operations representation, shared with 3D Shapes; no vendor-specific .tilt recipe types. |
| 18 | Geometry | Rounded rectangular profiles/solids | RoundedCornerQuad with radius, sampling, dimensions, thickness and pivot. | Panel/box construction with independently shaped outline and depth edges. | Related: Rectangle plus Shell covers ordinary Maquette cases; sampled Panel covers the additional ShapesXR construction requirements. |
| 19 | Geometry | Selected square and rounded corners | Independent corner selection was not established for the audited builder. | Four-corner selection mask; mixed selection present in the user fixture. | Specific: logical named corner selection; translate source mask order rather than copying C# enum bits. |
| 20 | Geometry | Depth-edge bevels and fillets | General equivalence not established for RoundedCornerQuad. | Separate chamfer/radius, sharpness and sampling, independent of outline corners. | Specific: new Panel construction provides this capability; do not assume generic Bevel/Chamfer has identical semantics. |
| 21 | Geometry | Frames and holes | RoundedCornerRim, border width, inner-radius convention and scale_border. | No equivalent exposed parametric frame preset established in the inspected catalogue. | Specific: Frame plus Shell; preserve source border/radius semantics and document collapsed-opening differences. |
| 22 | Geometry | Cylinders, cones and transformed ends | Independent end radii/scales, top offset/rotation, sweep and hard edges. | Cylinder and Tube are panel configurations, not the same cylinder builder. | Related: use Cylinder for Maquette; reconstruct ShapesXR from actual panel/bend values rather than the preset name. |
| 23 | Geometry | Spherical and conic surfaces | Circle, ellipse, hyperbola and parabola conic builders with clipping. | Sphere/Inverted Sphere are shared panel configurations, often bent through full wraps. | Related: Conic for Maquette; Panel plus Bend for the corresponding ShapesXR configuration. Matching names do not establish matching topology. |
| 24 | Geometry | Capsules | Capsule builder with source radius/overall-length and scale rules. | Capsule preset can be edited into a different panel/chamfer/bend configuration. | Related: Capsule generator for Maquette; resolved Panel recipe for ShapesXR. |
| 25 | Geometry | Curvature, signed/full sweeps and open surfaces | Partial/signed sweeps, cap/cut faces and special zero-angle behaviour. | Independent signed bends, full 360-degree wraps and optional rear geometry. | Related: preserve construction-specific semantics; use existing FaceRemove before bends, not a material-only workaround. |
| 26 | Geometry | Bend reference frames and surface sampling | No corresponding exposed general panel-bend feature established. | Two ordered bends use the original generator bounds; sampling depends on bend angles. | Specific: explicit-bounds Bend and adequate cap sampling; never guess the second bounds from the first bent result. |
| 27 | Geometry | Editable resizing of source meshes | Source mesh transforms are confirmed; equivalent border-preserving resizing is not. | Ten copied-mesh presets with border-preserving resizing. | Specific: mesh source plus a separate stretch contract/evaluator; ordinary scale and Polyhydra.Stretch(float) are not equivalents. |
| 28 | Drawing | Flat/freehand brush strokes | Straight/curved flat strokes; source control points contain paired Vector3 values. | Brush presets with point, rotation and size data. | Related: reconstruct source stroke geometry or map to a demonstrably matching Open Brush brush, retaining source controls for later editing. |
| 29 | Drawing | Variable stroke width and orientation | Control-point ribbons encode width/orientation implicitly. | Stroke sizes and rotations are explicit typed properties. | Related: normalize without losing ribbon edges, varying width or orientation; do not reduce all inputs to a centreline. |
| 30 | Drawing | Hull and volume drawings | Hull/volume/tube tools save generated geometry as ModelFile GLB. | Hull brush and mesh-stroke reconstruction exist in the plugin. | Related: load the geometry actually saved; do not invent retained voxel/construction history or require recreating Maquette tools. |
| 31 | Drawing | Stroke shading and brush identity | Legacy material names and material/version changes influence appearance. | Brush presets, materials and hard/smooth hull settings. | Shared: preserve colour, transparency and surface intent; a nearest-name brush substitution is an approximation, not lossless import. |
| 32 | Appearance | Colour, tint and colour space | Entity/stroke colour plus version-dependent material colour handling. | Object/material colour and colour changes per snapshot. | Shared: preserve source colour spaces and alpha; reuse tint support without silently baking the wrong colour twice. |
| 33 | Appearance | Opacity and blending | Transparent/additive/unlit brush and model materials. | Opacity, shading/blend and texture-alpha properties. | Related: map actual blend/cull/depth behaviour, not only RGB or material names; test visual results. |
| 34 | Appearance | Materials and shading models | Named built-in materials and imported model materials. | Material entities, shading types and per-object material bindings. | Related: resolve shared materials and map to Open Brush rendering; preserve unsupported shader intent. |
| 35 | Appearance | PBR and texture channels | Imported-model textures/material attributes; precise source shader mapping varies. | Base, normal, roughness, metalness, occlusion and emission maps/properties. | Related: reuse model PBR capabilities and preserve bindings; validate channel conventions instead of assuming identical shaders. |
| 36 | Appearance | Texture projection and transforms | Imported UV/material transforms; full procedural projection equivalence is unconfirmed. | Projection, wrapping, scale, offset, rotation and texture-mode properties. | Related: retain UVs and transform semantics; new mapping may be required for source procedural projection. |
| 37 | Appearance | Hard edges, normals and triangle layout | Builder-specific hard edges and warped cylinder diagonal rules. | Separate corner/edge sharpness, generated normals and deformed surfaces. | Related: preserve construction-aware smoothing and actual surfaces; matching vertex positions or AutoSmooth alone is insufficient. |
| 38 | Appearance | Per-corner attributes and mesh sections | Imported mesh submeshes, normals, UVs and material sections. | Copied resource meshes plus generated panel/stroke attributes. | Shared: explicit mesh source must preserve seams/sections and attributes; do not collapse all data to one value per geometric vertex. |
| 39 | Appearance | Outline, wireframe, Fresnel and passthrough effects | No complete corresponding native effect-property set established. | Material-effect and passthrough properties are present in the model. | Specific/partial evidence: preserve these properties and assess rendering adapters; native property presence is not proof of every UI control. |
| 40 | Media | Images | PNG/JPG/JPEG image primitives with aspect, transforms, tint/material state. | Image resources and image/rectangle presets. | Shared: reuse ImageWidget where suitable; preserve resolution, aspect, physical dimensions, opacity and orientation. |
| 41 | Media | Editable text | Text entities/content files, source font/style/alignment/fitting state. | Text string, font size, style, horizontal/vertical alignment and fitting. | Related: reuse TextWidget while retaining unsupported typography and reporting layout/font fallback differences. |
| 42 | Media | Font assets and identity | Audited content uses SegoeUI_SemiBold SDF; other source fonts may occur. | Text presets and font assets/settings. | Shared: preserve identity and metrics where available; a substituted font does not preserve exact layout automatically. |
| 43 | Media | Sound resources and playback | A native user-audio entity/importer was not confirmed. | Audio resources, playback actions and volume/distance settings [S2,S5]. | Specific: map available audio playback support and retain missing resource references; application sounds do not establish Maquette scene-audio support. |
| 44 | Media | Video and streamed media | Application/tutorial video exists; native scene-video import was not confirmed. | Documented placed YouTube videos; web-view playback settings [S3]. | Specific: preserve source URL/type and playback settings. A YouTube/browser resource is not interchangeable with an MP4 VideoWidget. |
| 45 | Media | Figma objects | No native equivalent established. | Figma resources, frame imports and synchronization metadata [S4]. | Specific: import the saved visual resource first, retain source identity; live Figma synchronization and prototype behaviour are separate features. |
| 46 | Media | Web/browser content | Legacy browsers.json exists; current functional browser scene feature is unconfirmed. | Web-view resolution, sound and chroma-key fields; YouTube is documented. | Related but unverified breadth: preserve browser-like data without advertising arbitrary web-app support or treating a thumbnail as functional content. |
| 47 | Media | Recorded avatars and Holonotes | No equivalent saved entity established. | Holonote resource type and avatar-recording metadata; exact playback path needs tracing. | Specific: retain recording/resource references and show an appropriate representation; do not mistake metadata preservation for avatar playback. |
| 48 | Environment | Environment and sky background | Gradient, sun, fog, floor and scene/master-stage environment settings. | Environment/skybox presets and exposure properties. | Related: map common components and retain unsupported ones; preset identity alone is not a visual match. |
| 49 | Environment | Dynamic light objects | Saved light entities with intensity/radius/spot/state/shadow parameters. | A comparable per-object light entity set was not established; environment sun controls are present. | Specific: map Maquette light objects to LightWidget where possible; do not infer ShapesXR light entities from global lighting controls. |
| 50 | Environment | Sun and shadow controls | Sun/environment settings plus source object shadow flags. | Sun direction/intensity and global shadow-intensity properties. | Related: preserve the distinction between scene lighting, shadow strength and object shadow casting. |
| 51 | Environment | Baked lighting and reflection data | Equivalent baked-lightmap/probe serialization was not established. | Lightmap resource/exposure and reflection-probe settings in the model. | Specific/partial evidence: retain resources/settings and assess their rendering compatibility separately from live lights. |
| 52 | Environment | Mixed reality, occlusion and spatial anchors | AR simulation, anchors and environment configuration. | Passthrough materials and documented shared spatial anchors [S8]. | Related: retain anchor/frame and occlusion intent. Importing a saved anchor is not recreating a shared-anchor service or hardware environment. |
| 53 | States | Multiple scenes and authored states | Scenes, slides, stages, ordering and selected-scene metadata. | Spaces, scenes, frames/states, ordering and selected snapshots. | Related: define state selection at import and preserve remaining states; one displayed frame is not the full document. |
| 54 | States | Inheritance and state resolution | Entity definitions, per-state trees and master-stage/environment selection. | Object properties, snapshots and inherited/default values. | Related: resolve the chosen state without overwriting source definitions or losing per-state differences. |
| 55 | States | Animation and transitions | User-tested switching between scenes 1 and 2 is instant, with no tweening, consistent with the audited direct transform-restoration path. Ordinary scene switching requires discrete state changes. | Frame transitions, easing, duration and timeline/autoplay data [S6]. | Specific: preserve Maquette discrete states; ShapesXR additionally requires transition/animation data and a playback model. |
| 56 | States | Navigation destinations | Standalone hotspots with state transforms and viewing-orientation metadata. | Viewpoints with viewing pose/zoom and frame association. | Related: preserve the destination pose and naming; assess bookmarks/portals rather than assuming identical hotspot/viewpoint behaviour. |
| 57 | States | Links to other documents/spaces | Cross-project hotspot/link semantics were not established by the audit. | Space-link records and navigation actions. | Specific: retain target references and map supported links to portals; do not turn every local viewpoint into a cross-document link. |
| 58 | Behaviour | UI prototype objects | UI-control asset IDs/property bags; no native UI-control fixture was found in the Maquette corpus. | Figma prototypes, built-in objects and interaction bindings. | Related at purpose level only: preserve visual resources and data; do not assume the applications share a UI layout/event schema. |
| 59 | Behaviour | Interaction triggers | JavaScript/event facilities exist; their full saved event API needs further tracing. | Click, hover, touch, button, collision, frame-enter and delay triggers [S5]. | Related: retain event graphs/references; source events do not automatically map to Open Brush commands. |
| 60 | Behaviour | Interaction actions | Scripts and hotspot/navigation facilities. | Go to frame/viewpoint, haptics, sound, look-at and associated target data [S5]. | Related: map supported actions explicitly; preserve unsupported ones rather than executing a guessed equivalent. |
| 61 | Behaviour | Source scripts | JavaScript runtime and project Scripts data. | Declarative interaction model; a comparable arbitrary user-script format is not established. | Specific: retain source scripts as inert project data; loading a document is not authorization to run it. |
| 62 | Behaviour | Colliders and play-mode physics | Editing/model collision exists; saved equivalent simulation semantics are unconfirmed. | Collider entities, shapes/transforms, enabled physics, frozen/grabbable settings. | Specific/related: retain physics configuration; Open Brush editing colliders are not a play-mode simulation substitute. |
| 63 | Behaviour | Body/viewpoint attachments | AR/anchor data is related but not a confirmed equivalent attachment model. | Attachment points and viewpoint/body-related properties; documented attach-to-body workflow. | Specific: preserve reference frames and following behaviour; initial static placement can only be an approximation. |
| 64 | Behaviour | Input, haptics and playback policy | Application input/runtime state is not necessarily saved document content. | Saved hover/input/haptic settings plus play-mode autoplay/loop/media controls. | Specific: preserve document-owned settings and action data without importing application/controller preferences as geometry. |
| 65 | Persistence | Source IDs, names, provenance and attribution | Entity/group names, asset paths, attribution and project metadata. | Entity/resource/preset/creator IDs and source metadata. | Shared: retain traceability and attribution without making a saved sketch depend on a share-code download. |
| 66 | Persistence | Unknown fields and unsupported entities | Unknown/version-specific JSON fields and optional sections. | Unfamiliar property/edit types, presets or versions. | Shared: a persistent preservation envelope and visible import report are needed; do not silently discard unfamiliar data on .tilt save. |
| 67 | Persistence | Compact editable geometry in .tilt | Procedural builder parameters should remain rebuildable. | Resolved panel/bend parameters and true source-mesh deformations should remain rebuildable. | Shared: use one versioned recipe format shared with 3D Shapes; no duplicate evaluated mesh fallback for every analytic object. |
| 68 | Persistence | Save/reload of scene state and resources | All imported transforms, visibility, groups, materials and resource relationships. | The same, plus snapshots/transitions/behaviour data retained beyond a chosen displayed frame. | Shared: saving must not silently reduce a native document to whichever widgets the initial importer happens to display. |
| 69 | Persistence | Missing-resource and fidelity reports | Missing local/content/Poly resources and unsupported builder/material cases. | Unavailable resources/presets/URLs and unsupported state/effect/action cases. | Shared: per-object diagnostics distinguish usable import, approximation and opaque preservation; a successful parse is not full parity. |
| 70 | Persistence | Representative validation corpus | 174 bundled scenes.json files plus supporting JSON; not independent user projects. | Two older public spaces plus the user-provided version-5 space. | Related evidence: validate meaningful feature combinations and native import/save/reload; corpus counts are not population-frequency claims. |

3. SCOPE BOUNDARIES

3.1 Loading is distinct from authoring UI parity. Toolbars, creation tools, thumbnails,
    controllers and preset browsers need not be recreated to display/edit imported data.
3.2 A static-state importer can be a useful first release, but it must say which state
    was resolved and retain unsupported states/behaviour if it claims preservation.
3.3 Live collaboration, account/team management, a hosted asset service and online Figma
    synchronization are not prerequisites for reading a native document. Their saved
    resource identities or behavioural relationships may still need preservation.
3.4 Recreating Maquette voxel/hull/tube authoring history is not a prerequisite when
    Maquette itself saves a generated GLB without that history.
3.5 No feature listed as unconfirmed should trigger implementation solely because an
    application ships a related class, shader, video or editor tool.
3.6 Schema coverage, executable support, appearance parity and editable authoring parity
    are separate measures. Do not label a feature complete based on parameter retention.

4. EVIDENCE

The table combines the fixed inspected artifacts with official public documentation.
Documentation can establish user-facing features. For Maquette, decompiled metadata
can establish saved fields without proving UI exposure or playback. Runtime parity is not inferred
from either alone. Public documentation was checked on 2026-10-23.

| Source | Evidence | Location |
| --- | --- | --- |
| S1 | Local Maquette format, entity, resource and feature audit | maquette-compatibility-assessment.md; ignored Temp/MaquetteAnalysis |
| S2 | ShapesXR imported resource categories | https://learn.shapesxr.com/dashboard/my-files |
| S3 | ShapesXR placed YouTube content and playback settings | https://learn.shapesxr.com/import/youtube-integration |
| S4 | ShapesXR Figma import, updating and prototype resources | https://learn.shapesxr.com/import/figma-integration |
| S5 | ShapesXR interactivity triggers/actions and Play mode | https://learn.shapesxr.com/prototyping/interactivity-and-play-mode |
| S6 | ShapesXR frames, transitions, easing and autoplay | https://learn.shapesxr.com/animation |
| S7 | ShapesXR object Inspector and editable properties | https://learn.shapesxr.com/object-inspector |
| S8 | Official ShapesXR update including shared spatial anchors | https://www.shapesxr.com/post/update-shapesxr-can-now-import-youtube-and-so-much-more |
| S9 | ShapesXR freehand brush creation and editable brush properties | https://learn.shapesxr.com/objects-creation/draw-tool |
| S10 | Official ShapesXR Unity integration and package download | https://learn.shapesxr.com/export-and-sharing/unity-integration |
| S11 | Local preset/typed-property/native-space audit and implementation limits | procedural-serialization-requirements.md |

The inspected ShapesXR importer is V1.31. Native-space evidence includes older public
spaces and the user's schema-5 fixture; the latter demonstrates mixed corner selection,
independent edge shaping, omitted rear surfaces and nonzero/full-wrap bends.

5. RELATED DESIGN

5.1 procedural-geometry-schema.md proposes the shared geometry serialization subset.
    It is not a schema for every state, media resource or behaviour in this table.
5.2 procedural-serialization-requirements.md records the procedural capability audit
    and the comparison against existing Polyhydra modifiers.
5.3 This matrix is local planning/research documentation, deliberately untracked. No
    implementation files or source fixtures were changed or committed to create it.
