Microsoft Maquette -> Open Brush: functional gap assessment
2026-10-09. Initial static audit; no importer or product changes.

1. Scope and evidence

Target: importing native Maquette projects into Open Brush, preserving data,
appearance, and editability progressively. These are separate coverage measures.
Importing a mesh can preserve geometry without preserving the authoring operation.
Saving and reloading in Open Brush must not silently discard retained source data.

Examined the installed Maquette version 2020.3.9.0, its managed assembly's type
metadata and selected serialization/loading behavior, and the installed content
library in place. Decompiled inspection files are local, ignored analysis only;
they are not implementation code. No Microsoft geometry, textures, fonts, or
shader assets were copied into Open Brush.

The installed library contains 874 JSON files, including 174 scenes.json files.
Scene-file versions observed: 2 (134), 13 (28), 17 (1), 18 (2), 19 (1), 20 (8).
Across old scenes plus newer slides/stages: 166 scenes, 30 slides, 66 stages;
120,580 entity-state records; 14,454 records with children; 109,408 with
non-uniform scale; 29,010 with material IDs. Counts are records, not unique
objects or independent projects; some content/state is repeated.

Documents/Maquette/Projects has Recents.json but no user project fixtures.
Installed MaquetteLibrary/Maquettes and StartSpace/Spotlights provide a substantial
local corpus. Online searching did not establish an additional downloadable native
test project; no third-party assets were downloaded. Handcrafted format fixtures
would be unvalidated until loaded/saved by Maquette and should not be called
authentic application-generated fixtures.

2. Confirmed format facts

The installed version uses .mqp and reads native projects as directories or ZIP
archives. A historical .mqt package is mentioned online; its compatibility with
this reader has not been established.

Native data is spread across manifest.json.mq, scenes.json, brushes.json,
primitives.json, named_groups.json, proceduralmeshes.json, textindex.json,
TextContents, lights.json, hotspots.json, uicontrols.json, project_settings.json,
anchors.json, arsim_settings.json, Objects, Images, and Scripts. Not all projects
have every section. Asset paths use tokens including <MAQUETTEOBJECT>,
<MAQUETTEIMAGE>, and <MAQUETTECONTENT>, as well as legacy paths and Poly IDs.

The installed version's format enum extends through version 21. Its loader has
explicit compatibility branches for older brush materials, straight-edge rotation,
primitive loading, scene color settings, slides/stages, and shadow defaults.
Reading every version as the latest schema would change behavior.

Entities and scene states have separate GUIDs. Each scene-state tree includes
target entity, parent/children, transform, visibility, edit lock, color, material
ID, shadow flag, optional text/light state, and object property bags.
Transform data contains position, quaternion, and Vector3 scale. Both canvas-
relative and local caching operations exist; exact rules by version and node
role still require tracing. Do not assume all transforms are parent-relative.

Flat brush strokes retain drawingMode plus control points made of TWO Vector3
points. Older brushType strings include material names rather than necessarily
describing geometry. Current flat-stroke drawing modes are Straight and Curved.
Spline/rotation/material version changes need independent compatibility handling.

Hull, voxel/volume, and tube drawers create ModelFile primitive entities.
The saving path exports generated mesh data as GLB. Their drawings should first
be treated as mesh imports; recreating those drawing tools is not a prerequisite
for importing the geometry that Maquette itself saves. Preservation of editable
voxel history or tube construction is not established by this save format.

3. Prioritized gaps and coverage strategy

P0: Native project reader, version normalization, and preservation envelope.
Open Brush has no Maquette-specific reader. It needs directory/archive loading,
all entity sections, GUID reference resolution, version-aware normalization,
asset dependency resolution, and retained unknown fields/sections. Existing
Open Brush metadata is typed; an explicit persistence extension is needed for
unsupported source data and provenance. Unsupported content needs a visible
placeholder and reason rather than silent omission. Preserve asset references
and attribution. Load compatibility failures must be reported per feature.

P0: Non-uniform transforms, pivots, and nested groups.
Open Brush TrTransform and widget save transforms use uniform scalar scale.
Its group IDs represent flat membership, whereas Maquette has nested state
trees and named groups. Imported model hierarchy can retain some nesting,
but it does not give independent native widget groups equivalent semantics.
Keeping non-uniform transforms inside model hierarchy can preserve appearance;
baking them into vertices loses original transform editability unless source
transforms are also retained. Add persistent Vector3 scale/hierarchy support
where needed, including mirrored scale, normal handling, pivots, and bounds.
Do not flatten nested groups or normalize imported content to preview size.

P0: Asset and geometry resolution.
Open Brush already imports GLB/glTF/FBX/OBJ and supports model subtrees and
breaking models apart. Reuse these capabilities for mesh-backed primitives,
including saved hull/volume/tube drawings. Native Maquette assets may be in the
project, installation library, a user folder, or an unavailable external source.
Do not ship Microsoft library assets. Offer resolution against the user's local
installation or project-supplied dependencies, with missing-asset placeholders.
Geometry bounds and assetLoadOption affect centering/loading and need parity
checks. Regenerating generic primitives is independent work, but is not proven
to match Maquette's exact topology, UVs, normals, or tessellation.

P1: Materials, color/opacity, and shadows.
Observed material state counts: Lit 17,539; Unlit 7,699; Matte 1,170;
Metallic 849; Additive 745; TranslucentUnlit 494; TranslucentLit 269;
Plastic 200; DefaultUnlit 36; EdgesLight 9. There may be others outside the corpus.
Open Brush can render imported materials, but native Maquette material IDs and
per-scene overrides have no established importer/save mapping. Add persistent
per-instance overrides and independently implemented material equivalents.
Verify color space, alpha, blend mode, double-sided behavior, vertex colors,
shadow casting, and image material/tint version changes. Matching a material
name to an arbitrary glTF PBR preset is not evidence of visual parity.

P1: Straight and curved flat strokes.
Open Brush has native brush strokes, but its centerline/orientation/pressure
representation is not the same as Maquette's paired edge points. A naive brush
GUID substitution or centerline conversion may alter width, twist, caps,
tessellation, and smoothing. First preserve paired control points and reproduce
the saved surface independently. Native brush editing can follow through a
specialized compatible brush or another editable surface representation.

P1: Slides, stages, and shared entity states.
Open Brush has layers, but the inspected metadata does not provide Maquette's
separate slide/stage collections, ordering, last selected scene, per-scene
states, and master-stage environment behavior. Importing only the active scene
is partial coverage. Preserve all states immediately; add state/scene switching
and shared-entity identity later. Avoid duplicating/flattening into layers and
describing the result as semantically lossless.

Scene-switch validation (2026-10-23): the user loaded the generated native
Scene Switch Probe and confirmed that switching between scenes 1 and 2 is
instant, with no tweening. The UI calls these scenes; slides/stages are internal
data distinctions. This agrees with the audited switch path, which directly
restores saved transforms. Ordinary scene switching therefore requires discrete
state changes, not tween animation. The temporary fixture and decompilations
were removed after validation. The fixture used shared entity identities across
two saved scenes with different transforms and colours, plus stationary reference
objects. Analysis target: installed Assembly-CSharp.dll, SHA-256
`5ce4f78092e1cdd254157d78155c094bcca4d7507d430571d44c440487200dc2`.

P2: Procedural shapes and parameter editing.
Two library files contain 1,611 procedural entities: rounded corner rim (184),
rounded corner quad (552), cylinder/cone (761), conic section (114). Parameters
cover dimensions, radii, thickness/borders, tessellation, sweep, inversion,
clipping, and top/bottom transforms. Additional builder classes exist, so this
is not an exhaustive feature list. Open Brush's generic model import does not
retain a procedural editor. Preserve parameters; independently generate matching
meshes for display; add parameter editing afterward. Version 21 specifically
changes inward rim scaling and is not represented by the sampled scene files.

P2: Text and images.
Open Brush has TextWidget and ImageWidget. Text contents, font identity, size,
style, alignment, auto sizing, and formatting state need mapping; TiltText's
saved fields do not express all Maquette text formatting. The corpus uses
SegoeUI_SemiBold SDF; do not redistribute Maquette's font atlas. Missing-font
fallback should preserve text and source font identity while reporting layout
differences. Image materials/tints, transparency, aspect, and transforms also
need version-aware mapping and persistence.

P2: Lighting and environment.
Open Brush already has light widgets, custom lights, fog, and gradient settings.
Maquette state includes light intensity, radius, spot angle, enabled/shadow
flags, background gradient, sun, fog, floor, and later environment settings.
Map existing equivalents first; persist unsupported fields. Verify units and
rendering behavior, especially per-light shadows/enabled state and scene/master-
stage environment selection. This is partly mapping work, partly save/render
feature expansion; exact visual equivalence remains untested.

P3: UI controls, scripting, hotspots, and AR simulation.
The installed assembly includes UI asset IDs and string property bags, a
JavaScript integration/runtime, hotspots, navigation state, and AR simulation
settings. No uicontrols.json instance was found in this installed corpus.
Open Brush's scripting facilities do not establish compatibility with Maquette's
JavaScript API or event/object model. Preserve descriptions and scripts as inert
project data, display representations where possible, and defer behavioral
compatibility. Hotspots might map to bookmarks/viewpoints but equivalence has
not been established. AR occlusion/environment behavior needs its own design.
The presence of browsers.json in older content does not establish a current
functional browser feature; keep it as legacy data pending further investigation.

P3: Video and other media beyond confirmed image/model paths.
Open Brush has VideoWidget and audio support. The installation also includes
video/tutorial facilities, but native project serialization of embedded video
or audio entities was not confirmed in this audit. Do not count those as a
confirmed missing importer feature until an actual project or save path is found.

4. Recommended implementation sequence

1) Establish a versioned intermediate representation and Open Brush persistence
   for all source entities/states/unknown data. Build an inspection-only reader
   that reports dependencies and unsupported features before scene conversion.
2) Import mesh-backed entities and images with accurate transforms, names,
   nested grouping, visibility/locks, material overrides, and useful diagnostics.
   Solve persistent non-uniform transforms and grouping before claiming lossless
   native object editing. Resolve dependencies without bundling library assets.
3) Add accurate flat-stroke surface reconstruction and procedural geometry,
   keeping source control points/parameters. Expand editable representations
   feature by feature, prioritized by corpus prevalence and user projects.
4) Add slides/stages/state switching, text fidelity, lighting/environment parity,
   then behavioral/UI/navigation features as their demand and fixtures justify.

5. Validation boundaries and minimum fixtures

No application launch, Unity refresh, runtime import, visual comparison, or
save/reload test was performed. This report is a static functional audit, not
a claim of working compatibility. No changes were made to tracked project code.

Use the installed samples for read-only coverage measurements. Add original
fixtures, generated and saved by Maquette where possible, for: nested groups
with rotations and mirrored/non-uniform scale; shared entity in two scenes;
every material on a model and image; straight/curved paired-edge strokes;
hull/volume/tube saved meshes; procedural parameter extremes; formatted text;
point/spot lighting and shadows; missing/relative asset paths; and UI/script
properties. Compare data retention, geometry/transforms, rendered appearance,
native editing behavior, and Open Brush save/reload separately. Do not use the
scene-record frequencies as a proxy for user demand or exhaustive feature coverage.

6. Public documentation consulted

https://learn.microsoft.com/en-us/windows/mixed-reality/design/maquette
Documents export to FBX/GLB/glTF and preservation through the Unity add-on.
It does not provide a complete native project-format specification.

https://marketplace.visualstudio.com/items?itemName=ms-maquette.vscode-maquette-javascript
Microsoft's Maquette JavaScript integration describes evaluating .mqjs text in
Maquette; this supports treating scripts as a distinct behavior-compatibility area.

7. Local evidence index

library-schema.json: aggregate JSON property names and file counts, no assets.
corpus-summary.json: scene versions, state counts, and local example paths.
material-summary.json: observed materials, procedural paths/parameter names, fonts.
Maquette.JSONEntityState.txt and Maquette.CachedTransform.txt: state schema.
Maquette.MqMaquetteFile.txt: directory/ZIP reading.
Maquette.MqDocumentManager.txt: load/version/save behavior, generated GLB meshes.
Maquette.MaquetteFileFormatVersion.txt: version changes through 21.
Maquette.JSONBrushStroke.txt and Maquette.ControlPoint.txt: flat stroke schema.
Maquette.MqBrushDrawer_Hull/Voxel/Tube.txt: mesh-backed primitive creation.
Open Brush Assets/Scripts/Util/TrTransform.cs: scalar uniform scale.
Open Brush Assets/Scripts/Sketch/GroupManager.cs: flat group IDs.
Open Brush Assets/Scripts/Save/SketchMetadata.cs: current persisted widget fields.
Open Brush Assets/Scripts/Widgets/ModelWidget.cs and Commands/BreakModelApartCommand.cs:
model hierarchy/subtree capabilities, without equivalent native nested groups.

Supporting inspection outputs and tools remain under ignored Temp/MaquetteAnalysis.
The assessment is saved separately in the project root and remains untracked.
Do not commit decompiled code or Microsoft assets as implementation material.


8. Imported content comparison

Verified against the installed Maquette 2020.3.9.0 assembly and this Open Brush
checkout. A file appearing in a picker filter is not proof that every file of
that format loads successfully. No runtime format tests were performed.

1) 3D model files.
   Maquette's object picker explicitly lists: .3mf, .fbx, .blend, .3ds, .gltf,
   .glb, .obj, .stl, .dxf. Its loader has a dedicated glTF/GLB path and uses
   TriLib for other model loading. Dependencies and format-version limits,
   particularly for .blend, have not been established.
   Open Brush's model catalog lists .gltf2, .gltf, .glb, .obj, .vox, .svg,
   .ply, .spz, .sog; USD formats and FBX are conditional on build configuration.
   PLY/SPZ/SOG entries include Gaussian-splat content; they should not all be
   treated as ordinary editable polygon meshes. Maquette's additional picker
   formats are potential importer coverage gaps in Open Brush, distinct from
   native-project import. Do not infer parity from a shared third-party loader.

2) Local images.
   Maquette's image browser accepts .png, .jpg, .jpeg.
   Open Brush's image extension helper lists .jpg, .jpeg, .png, .svg, .hdr, .exr.
   Format acceptance does not establish equivalent placement, tint, material,
   transparency, or lighting behavior.

3) Video.
   Open Brush supports scene video widgets; the Main scene configures .mp4,
   .mkv, .mov, .webm, plus .txt descriptor files. Codec/platform support may vary.
   Maquette has video playback, tutorial, preview, and capture facilities.
   However, its AssetSourceType enum contains no VideoFile category, and no
   user video asset picker or persistent scene video entity was established.
   Therefore user video import is UNCONFIRMED, rather than assumed supported
   because the installation includes MP4s or a VideoPlayer class.

4) Audio.
   Open Brush has persistent sound-clip widgets/catalog support.
   No corresponding Maquette user audio importer or saved scene audio entity
   was identified. Application sounds and video audio are separate facilities.

5) Native projects and reusable snippets.
   Maquette exposes reusable Maquette content/snippets and native project
   loading. The installed version uses .mqp with directory/ZIP reading.
   Open Brush loads its own/Tilt Brush sketches but currently has no native
   Maquette reader. Historical .mqt package compatibility remains unverified.

6) Online models.
   Maquette contains GooglePoly and SketchFab source categories and integration
   paths. These establish historical integration, not that the services still
   function in the installed application today.
   Open Brush has Icosa integration and model URL import through its API.

7) Online images.
   Maquette contains Bing image-search UI and an ImageFromWeb source category.
   Current Bing service functionality was not tested.
   Open Brush's image.import API accepts a URL or media-library image filename.

8) Scripts.
   Maquette supports .mqjs JavaScript files and a corresponding editor
   integration. Open Brush has Lua scripting/plugins with a different runtime
   and object API. These are not interchangeable media import formats.

9) Built-in authoring content, rather than external file imports.
   Maquette offers primitive/library objects, procedural shapes, straight/curved
   flat strokes, hull/volume/tube tools, text, lights, and UI controls. Preserve
   their project data independently of the external-format comparison above.

Additional local evidence:
Maquette.MqAssetContent.txt: object picker extension list and asset-menu modes.
Maquette.MqImagesSubContent.txt: PNG/JPG/JPEG image filter.
Maquette.AssetSourceType.txt: native and online source categories.
Maquette.MqAssetCache.txt: dedicated glTF path and TriLib model loading.
Open Brush Assets/Scripts/ModelCatalog.cs: model discovery formats/conditions.
Open Brush Assets/Scripts/ReferenceImageFormat.cs: image extension helper.
Open Brush Assets/Scenes/Main.unity: configured video extensions.
Open Brush Assets/Scripts/API/ApiMethods.cs: image.import URL support.

9. Procedural geometry implementation status (2026-10-09)

Scope: independently implemented geometry and editable recipe persistence;
no Maquette source/assets copied and no Open Brush VR panel changes.

1) Rectangle added to Polyhydra Shapes. XZ-plane surface, facing up.
   Generic recipe: A=height relative to unit width, B=outer corner radius,
   C=segments per corner. Zero radius or segments gives square corners.
2) Frame added to Shapes. Corresponding contour points form an ordered band
   of quads around the empty center; triangles occur only at collapsed corners.
   Square corners produce eight vertices and four quads (Kis produces 16 faces).
   Generic recipe: A=height, B=inward border width, C=outer corner radius,
   D=segments per corner. Inner radius=max(0, outer radius-border width).
   Existing extrusion/Shell provides thickness. Experimental FaceInset frame
   mode and explicit-contour FaceFrame API were removed.
3) Open Brush's 3d-shapes checkout now dispatches these generators and retains
   their parameters in editable-model definitions. Frame adds serialized d;
   absent d defaults to four segments. No panel controls were added.
4) Existing Revolve modifier extended to support negative normalized turns,
   including correct cap orientation. Supports independently sampled conic
   profiles; no parallel Maquette-specific conic solid family added.
5) TranslateX/Y/Z corrected: previously each added 1 on the other two axes.
   This would corrupt imported pivots and procedural offsets.

Remaining geometry work:
- Cylinder/cone construction now has a direct Loft overload accepting mesh-space
  translation, quaternion rotation, and Vector3 scale. It duplicates one face,
  scales/rotates about its centroid, translates it, and joins corresponding
  vertices with quads. Optional end caps and a collapsed cone apex are supported.
  This avoids depending on detached-face transforms. Recipe/serialization support
  for this overload is intentionally deferred. Partial-sweep profile construction
  and exact Maquette parameter mapping still require importer work.
- Polyhydra constructs conic profiles through Shapes.ConicProfile. The importer
  maps and retains source sampling, clipping, dimensions, sweep convention,
  and cap parameters; it does not generate vertices.
- Rectangle/Frame source mapping must account for Maquette's inner-radius rim
  convention, scale-dependent radii/borders, pivot, inversion, and version.
  The new generators alone do not establish lossless native-scene import.

Validation and dependency state:
Focused runtime regression cases added for frame holes/borders, rounded-frame
Shell topology, signed Revolve, axis translation, and direct transform Loft. Static diff checks pass;
Unity compilation and runtime tests have not been run. The Open Brush checkout
still references the published UPM branch; it requires the updated package
before compiling these new generator references. No package cache or dependency
manifest was edited, and no commits or pushes were made.

10. Conic profile / Revolve comparison (2026-10-09)

Static result: all four conic families can be expressed as a sampled planar
profile generated by Shapes.ConicProfile and passed to the existing Revolve
modifier. No parallel conic solid family added. Runtime confirmation is pending
Unity compilation and test execution.

Sampling specification, independently expressed from the binary analysis:
For row j=0..vertical_segments, theta=pi*lerp(bottom_clip,top_clip,j/segments),
u=-cos(theta). Coordinates below are (radial distance, height), in the XY plane.

Circle:    (radius*sin(theta), radius*u)
Ellipse:   (0.5*major_axis*sqrt(1-u*u), 0.5*minor_axis*u)
Hyperbola: (0.5*major_axis*sqrt(1+u*u/(minor_axis*minor_axis)), 0.5*u)
Parabola:  (0.5*(u*u/(4*major_axis)+center_offset), 0.5*u)

The non-circle builders use an effective radius of 0.5. Clip fractions specify
sampling angles, not linear fractions of height. Close the sampled profile to
the axis at its lower and upper endpoint heights, omitting duplicate pole
vertices. Revolving these closing edges creates the clipped end surfaces.

Maquette samples positive sweeps toward +Z from +X. Polyhydra Revolve around Y
uses the opposite angular direction: import with turns=-sweep_angle/360.
Partial sweeps need the two profile caps; full sweeps weld the seam. Apply the
existing halfedge winding flip for inverted surfaces.

Observed bundled content: 114 conics (18 circle, 2 ellipse, 2 hyperbola,
92 parabola). Ten have a non-default top clip and nine have a non-default bottom
clip. All observed axes and center offsets are positive and clip intervals
ordered. Sweeps are full for 113 objects and zero for one object.

Zero sweep is an explicit API policy. Existing thin-wedge behavior remains the
default; importer code can request ZeroSweepBehavior.DoubleSidedProfile for
Maquette compatibility, or callers can request Empty. No serialization added.

Identified rendering gap: concave caps were tessellated using fixed XY projection
(normal Vector3.back), then reversed unconditionally. Mesh generation now uses
the actual face normal and retains the tessellator's matching winding. This is
an existing mesh-rendering correction, not a Maquette-specific shape feature.

Added focused runtime checks:
- Four independently sampled conic profiles, full and clipped vertically.
- Full revolutions and quarter sweeps in both directions.
- Closed halfedge topology and sphere-like Euler characteristic.
- Rendered signed volume against the independent polygonal-frustum formula.
- Concave face area/winding in XY and YZ orientations and reversed orientation.
- Explicit zero-sweep policies while retaining the default thin wedge.

Limits: checks are authored, not yet executed. Exact source-parameter mapping,
source material/normals and preservation of original parameter bags remain
importer work. Profile sampling itself belongs to Polyhydra. Reversed/equal clip ranges, singular axes, and negative
parabola radii are outside this comparison; no complete parameter-space parity
claim is made. Current source/profile equations suffice for observed valid
bundled conics, subject to the pending runtime checks.

11. Capsule comparison (2026-10-09)

Installed Maquette has five concrete procedural builders, all present in its
palette: Capsule, ConicSection, CylinderCone, RoundedCornerQuad, RoundedCornerRim.
Capsule was absent from the two sampled procedural scene files, but is exposed
through the shipped palette. Its fields are radius, length, round_profile,
sweep_angle, sides, end_segments, and invert.

Maquette's capsule has two sampled hemispherical ends joined by a straight tube.
Bicupola and birotunda instead use polygonal end faces and cupola/rotunda side
patterns. They are useful related solids, but do not directly reproduce the
capsule's pole-to-equator quarter-circle sampling or adjustable end segments.

VariousSolids.Capsule already uses the matching hemispherical profile, with
sides, capSegments, cylinderHeight, and actual radius. For positive unit object
scale, map Maquette's radius parameter to actual radius R=radius/2, and map
length to cylinderHeight=max(length-2*R,0.001). Maquette's length means overall
height before its minimum straight-section clamp; Polyhydra's cylinderHeight
means only the straight section. Its radius field therefore acts like a diameter
under unit scale. Maquette clamps sides to 3..100 and end_segments to 2..50.

For partial or negative sweeps, construct the same capsule half-profile, close
it along the axis, and use existing Revolve around Y with
turns=-sweep_angle/360. No new capsule generator is required.

Nonuniform positive scale mapping, with source scale (sx,sy,sz):
R=radius*max(sx,sz)/2; H=max(length*sy-2*R,0.001).
Construct a radius-R capsule with straight section H, then scale mesh coordinates:
- round_profile=true: (1/sx,1/sy,1/sz), preserving circular world cross-section.
- round_profile=false: (1/max(sx,sz),1/sy,1/max(sx,sz)), allowing elliptical world
  cross-section while retaining the radius chosen by the larger horizontal scale.
The source object's own transform remains separate and is applied afterward.

Zero-sweep quirk: the shipped builder displays only the double-sided upper-cap
profile, not a full capsule cross-section; its lower-cap mirror and connecting
tube are built only in the nonzero-sweep branch. If exact compatibility is
required, handle this importer case using that upper profile and the explicit
double-sided zero-sweep option. Do not alter generic capsule behavior for it.

Conclusion: existing operations suffice once profile construction is exposed by
Polyhydra. VariousSolids.Capsule now accepts signed/partial sweep angles; the
shared Shapes.CapsuleProfile API exposes its outline. Exact parameter conversion
and source quirks belong in the importer, while vertex generation stays in the
library. Runtime parity remains unverified.

12. Geometry generation ownership and direct APIs (2026-10-09)

Geometry generation belongs to Polyhydra. Importers only decode/map parameters,
call library generators/modifiers, and retain source metadata. They do not
sample curves or build vertex/face arrays.

Shapes.ConicProfile(type, segments, radius, majorAxis, minorAxis, centerOffset,
                    bottomClip, topClip)
- ConicType: Circle, Ellipse, Hyperbola, Parabola.
- Closed single-face radial outline in XY, facing +Z, intended for Revolve about Y.
- Radius is uniform profile scale. Clip values are angular fractions of PI.
- Positive axes/radius and increasing clip ranges are required; clips clamp to
  [0,1]. No new Maquette-specific types, recipe enums, or controls were added.

Shapes.CapsuleProfile(capSegments, cylinderHeight, radius)
- Closed half-capsule outline in XY, with semicircular ends and a straight side.
- Shares its point construction with the existing Capsule solid generator.

VariousSolids.Capsule(sides, capSegments, cylinderHeight, radius,
                     sweepAngle=360, zeroSweepBehavior=ThinWedge)
- Existing positive full-sweep calls retain the original lathe path.
- Signed/partial sweeps use the shared profile and existing Revolve.
- Positive capsule sweep is toward +Z; direct Revolve about Y has opposite sign.
- Zero policy is explicit; generic double-sided Capsule returns the whole half
  profile. To match Maquette's unusual upper-cap-only zero case, compose the
  library's clipped Circle ConicProfile (clips .5..1), translation, scaling, and
  zero-sweep DoubleSidedProfile rather than generating importer-side geometry.

Conic regression checks now use Shapes.ConicProfile for the tested geometry,
retaining independently sampled analytic values only as the volume oracle.
Added checks for capsule profile/full-generator geometry equivalence and signed
sweeps. Static diff checks pass. Unity compilation/runtime checks remain pending.

13. Construction-aware smoothing (implemented; Unity checks pending)

PolyMesh.Loft(translation, rotation, scale, capEnds=true, smoothSides=false)
- Opt-in side-to-side normal blending; end caps remain sharp.
- Applies to transformed prisms, open lofts, and collapsed-apex cones.
- Existing calls remain faceted; the input profile is unchanged.

VariousSolids.Capsule(..., zeroSweepBehavior=ThinWedge, smooth=false)
- Opt-in smoothing across the curved body, poles, and hemisphere/tube joins.
- Partial-sweep cut faces remain sharp, including their boundary with the body.
- Empty/double-sided zero-sweep output has no smoothed edges.

Uses existing Halfedge.IsSmooth and corner-normal rendering; no data structure
changes. Added focused regression cases; static whitespace checks pass, but
Unity compilation and runtime tests have not been run.

Rounded Rectangle/Frame solids: use AutoSmooth for ordinary cases. The proposal
to carry corner intent through modifiers was withdrawn; no demonstrated need
justifies that metadata. Generic Revolve has no new smoothing policy.


14. Exposed procedural builder coverage audit (2026-10-10)

14.1 Scope and evidence

The installed assembly contains five concrete procedural builders. All five
were previously traced to shipped palette entries. This audit maps their
user-exposed controls, stored dimensions, runtime clamps, and edge cases to the
current local Polyhydra checkout; it does not claim runtime or pixel parity.
No Microsoft code or assets were incorporated into Polyhydra.

Evidence is the locally decompiled builder and context-menu files under
Temp/MaquetteAnalysis. Field attributes distinguish visible controls from
hidden serialized parameters. The context menu skips HideInContextMenu fields
and supports scalar, boolean, Vector2 and Vector3 controls. Sliders enable
useDynamicRange: declared *_range fields are UI hints, not reliable hard limits.
Builder RebuildMesh clamps are recorded separately below.

All generated geometry remains Polyhydra's responsibility. Import mapping may
convert units, calculate dimensions, apply transforms, choose operations, set
smoothing markers and preserve source parameters. It must not sample curves or
construct vertex/face arrays. Recipe/serialization limitations remain deferred.

14.2 CylinderCone -> Polygon/Sector + transform Loft

Visible controls:
1. radius_top=.4, radius_bottom=.6; declared ranges 0..1 and .01..1.
2. sweep_angle=360; declared range 0..360; runtime clamp -360..360.
3. sides=32; runtime clamp 3..100.
4. hard_edge=false and invert=false.
5. More Parameters: top_offset=(0,0), top_rotation=(0,0,0), top_scale=(1,1),
   bottom_scale=(1,1). These are real exposed vector controls.

Construction:
1. Height is fixed at one local unit, with end centres at Y=-.5 and +.5.
   Object scale supplies other heights.
2. Full sweep: Shapes.Polygon(sides), scaled to the bottom ellipse, then Loft
   to the independently scaled/rotated/offset upper ellipse.
3. Partial sweep: Shapes.Sector(sides, abs(sweep)/360), with angular reflection
   for a negative sweep, then the same Loft. Its two radial boundary segments
   produce the two cut faces. Exactly 360 degrees uses Polygon.
4. top radius zero: Loft with collapsed scale produces the cone apex.
5. hard_edge=false: smooth only the curved side strips. Both end caps and
   partial-sweep cut faces remain sharp. hard_edge=true: faceted side strips.
6. Invert by existing Halfedges.Flip(); apply smoothing after topology changes.

Mapping details, not new generators:
1. Relative end scaling must not divide by a zero bottom-scale component.
   For singular inputs, start from a unit profile and transform the generated
   end vertices using existing mesh/vertex APIs, rather than inverting that
   component. Whether those singular source outputs deserve exact emulation
   still needs a policy decision.
2. A partial sector's centroid is off-axis. Loft transforms about its centroid,
   while Maquette transforms around the ellipse centre. Compensate translation:
   if c is the input face centroid, b its intended bottom centre, q the desired
   top centre and L the end linear transform, pass q + L(c-b) - c to Loft.
   Computing this from existing geometry is mapping, not curve generation.

Confirmed gaps:
1. Warped side surfaces: independent top rotation or anisotropic end transforms
   can produce nonplanar quads. Maquette triangulates each strip using a
   diagonal; Polyhydra's convex polygon renderer uses a centroid fan. Matching
   ring vertices therefore does not guarantee matching surface geometry.
   Moreover, Maquette's hard_edge branches can choose different diagonals.
   Example: four sides, both radii one, top rotated 45 degrees around Z.
   One strip's scalar triple product is .292893219 (not planar); its centroid
   is .059786578 units off a source hard-edge triangle plane. This is a surface
   difference, not just a normal-weighting difference. A triangulation decision
   is required; do not silently replace editable Loft quads everywhere.
2. Selective side smoothing: Loft(smoothSides:true) smooths all generated side
   faces, including the radial cuts of a Sector. AutoSmooth cannot always repair
   this: a 300-degree cylinder with three arc strips has body dihedrals of 100
   degrees and body/cut dihedrals of 40 degrees. No angle threshold smooths only
   the body. Existing Halfedge.IsSmooth can express the correct result, but a
   concise construction-level way to select the strips remains an API gap.
3. Tiny sweeps: Sector clamps turns to at least .0001 (.036 degrees), although
   the source permits smaller nonzero angles. Zero sweep is a degenerate source
   cylinder construction, not a meaningful wedge; exact behavior is undecided.

14.3 RoundedCornerQuad -> Rectangle + Loft/Shell

Visible controls: radius=.01 (declared 0...01), sides=8, invert=false.
Stored but hidden: width=.5, height=.3, thickness=.1, pivot=(.5,.5,.5).
DisableAutoScale is an implementation field, not a procedural menu control.
Runtime: sides clamp 0..25; radius floors at zero and caps at half the smaller
scaled dimension. Corners are square if sides=0 or effective radius<=.0001.

Construction for positive nonzero source scale (sx,sy,sz):
1. Build Rectangle(width*sx, height*sy, effectiveRadius, sides) in working units.
2. Rotate XZ to XY, undo sx/sy on the profile, and use Loft/Shell for local
   thickness along Z. Apply source scale only once afterward.
3. Match the pivot by translating the resulting centred solid by
   ((.5-pivot.x)*width, (.5-pivot.y)*height, (.5-pivot.z)*thickness).
4. Use zero Rectangle radius when the source square-corner condition holds.
   Both libraries sample rounded corners with the same quarter-circle step.
   Source duplicate vertices used for sharp shading need not become duplicate
   editable topology. AutoSmooth suffices for ordinary rounded cases.
5. Invert using existing halfedge winding flip.

Status: ordinary geometry is expressible. No additional rounded-solid family
or corner-intent metadata is justified. Zero thickness, maximum-radius corner
collapse, and mirrored/zero scales need explicit boundary comparison; they are
not evidence of a missing normal-case generator.

14.4 RoundedCornerRim -> Frame + Shell

Visible controls: border_size=.02 (declared .001...5), radius=.04 (0...5),
sides=8, invert=false, scale_border=false.
Stored but hidden: width=.5, height=.3, thickness=.1, pivot=(.5,.5,.5).
Runtime: sides clamp 0..25. Border in working units is
b=border_size*(scale_border ? max(sx,sy) : 1), capped at half each scaled
width/height. radius means INNER corner radius, unlike Polyhydra Frame.

Construction for positive nonzero source scale:
1. W=width*sx, H=height*sy. In the ordinary rounded case call
   Frame(W,H,b,radius+b,sides), then Shell for thickness, rotate to XY and
   undo sx/sy as for Rectangle. Frame's inner radius becomes radius.
2. When source radius<=.0001 or sides=0, both contours are square: pass outer
   radius zero, not radius+b. scale_border changes b before construction.
3. Apply the same centred-solid pivot offset and winding inversion as Quad.
4. AutoSmooth is sufficient for ordinary curved walls and sharp front/back.

Boundary mismatch requiring a decision:
1. Maquette separately clamps each contour radius, then floors it at .0005
   in its rounded branch. Frame instead derives inner radius from the outer
   radius and border and removes the hole when border reaches half the smaller
   dimension. These agree for ordinary noncollapsed contours but can differ
   when the inner opening collapses or becomes extremely small. Source arcs
   can even exceed the remaining inner half-dimension because of that floor.
2. No new general frame API is warranted until deciding whether these source
   degeneracies should be emulated. Preserve the original parameter bag.

14.5 ConicSection -> ConicProfile + Revolve

Visible controls: radius=.5 (.01..1), major_axis=1 (.01..1), minor_axis=1
(.01..1), center_offset=.2 (-1..1), sweep_angle=360 (0..360),
bottom_clip=0 and top_clip=1 (both 0..1), sides=32, vertical_segments=16,
and invert=false. curve_type is stored but hidden; the palette chooses one of
Circle, Ellipse, Hyperbola, Parabola. type itself is not a procedural parameter.
Runtime: sides clamp 3..100, vertical_segments 2..50, sweep -360..360, both
clips independently 0..1. It does NOT require bottom_clip<top_clip.

Construction:
1. Use the corresponding Shapes.ConicProfile type, with the same vertical
   segment count and angular clips. Formulas are specified in section 10.
2. Circle uses the source radius; all other types force effective radius .5.
   Circle ignores axes/offset; Ellipse uses both axes; Hyperbola uses both axes;
   Parabola uses major_axis/center_offset and ignores minor_axis.
3. Revolve about Y with turns=-sweep_angle/360 and the clamped side count.
4. Zero sweep uses DoubleSidedProfile; inversion uses Halfedges.Flip().
5. AutoSmooth is appropriate for ordinary resolved curves; exact normals and
   triangle allocation remain unverified and are not claimed as matching.

Gaps/decisions:
1. ConicProfile rejects equal and reversed clips, but both independently exposed
   source sliders permit them. Reversed ranges may be expressible by reordered
   sampling/winding; equal ranges can collapse to disks or degenerate surfaces.
   Decide desired compatibility behavior before relaxing validation blindly.
2. Negative parabola center_offset is a real menu choice. It can yield negative
   radial coordinates or cross the axis: e.g. major_axis=1, center_offset=-.1,
   full clips crosses at u=+/-sqrt(.4). The current formula accepts it, but its
   closed profile can self-intersect; valid halfedge/triangulation behavior is
   not established. The earlier positive-radius comparison did not cover it.
3. Revolve clamps every nonzero sweep below .036 degrees upward and treats
   sufficiently near-full sweeps as closed through Mathf.Approximately.
   Source uses exact full-sweep equality. These are genuine range differences.
4. Singular axes are not guarded by the source formulas and can divide by zero.
   Live UI entry of these values has not been verified; treat them separately
   from supported finite shapes.

14.6 Capsule -> VariousSolids.Capsule / CapsuleProfile + Revolve

Visible controls: round_profile=false, sweep_angle=360 (0..360), sides=12,
end_segments=6, invert=false. Stored but hidden: radius=.5 and length=1;
their declared ranges are .01..1. Dimensions are also controlled through object
scaling. Runtime: sides 3..100, end_segments 2..50, sweep -360..360.

Construction:
1. For positive scale use R=radius*max(sx,sz)/2 and
   H=max(length*sy-2*R,.001). Call Capsule(sides,end_segments,H,R,sweep_angle,
   smooth:true), then apply the inverse scale compensation from section 11.
2. This covers full/partial, positive/negative sweeps and round_profile choices
   without importer-side sampling. Invert with the existing winding operation.
3. At exactly zero sweep Maquette shows only a double-sided upper-cap profile.
   Compose a Circle ConicProfile clipped .5..1, translate it to H/2, then use
   Revolve(DoubleSidedProfile) with the same scale compensation. Generic Capsule
   intentionally retains its whole-profile zero policy.

Status: ordinary capsule geometry and smoothing are expressible. Tiny sweeps
inherit the Revolve range gap. Negative/zero object scales and the source's
minimum straight section remain boundary validation cases, not new builders.

14.7 Priorities and limits

1. Resolve warped-Loft triangulation: a demonstrated surface-geometry gap in an
   exposed feature. Preserve the ordinary quad topology unless explicitly
   choosing a different mode for warped strips.
2. Resolve partial-Loft smoothing selection: demonstrated failure of both
   all-sides smoothing and an angle threshold; existing edge flags can express
   the answer, so this is API convenience rather than a new geometry family.
3. Remove or explicitly accommodate tiny/near-full sweep discrepancies if exact
   angular coverage is required. This is an existing-operation behavior issue.
4. Decide handling of equal/reversed clips, axis-crossing parabola profiles,
   collapsed rims, and other degenerate source geometry one case at a time.
5. Then run the already-authored Unity checks and targeted comparisons. No
   claim of complete parameter-space, rendered-surface, or shading parity yet.

Analytic counterexamples are saved separately in
Temp/MaquetteAnalysis/procedural-coverage-counterexamples.txt. They are math
checks, not execution of either application's mesh builder. No production code
was changed in this audit. This section supersedes broad earlier parity claims.


15. Optional Loft side triangulation (2026-10-10)

Implemented the requested trailing optional parameter on transform Loft:
Loft(translation, rotation, scale, capEnds=true, smoothSides=false,
     triangulateSides=false)

1. Default false retains side quads. True splits each non-apex side into two
   triangle faces, without adding vertices or changing the cap polygons.
2. The diagonal joins original vertex i to transformed vertex i+1. Both
   triangles inherit the side strip's role and tags. Collapsed-apex cones
   already have triangular sides and remain unchanged.
3. smoothSides applies across all generated side triangles and keeps caps sharp.
4. Existing regression cases now cover both modes for transformed profiles,
   outward winding in either loft direction, open ends, cones and smoothing.
   Static diff checks pass; Unity compilation/runtime checks remain pending.
5. This supplies explicit triangle surfaces without changing normal Loft
   topology. Exact Maquette diagonal mapping (including its hard_edge branch
   differences) and partial-sweep smoothing selection remain separate work.


16. Dedicated transformed cylinder/cone generator (2026-10-10)

Implemented VariousSolids.Cylinder in Polyhydra after agreeing that the
Sector -> Loft construction was becoming unwieldy. This supersedes section
14.2's recommended composition and proposed boundary-smoothing metadata.

API:
Cylinder(sides=24, height=1, radiusBottom=.5, radiusTop=.5, sweepAngle=360,
             topOffset=null, topRotation=null, bottomScale=null, topScale=null,
             smoothSides=false, triangulateSides=false)

1. Optional topOffset is XZ Vector2; topRotation is Quaternion. Both optional
   end scales are XZ Vector2. Null means zero offset, identity rotation and
   unit scales. The lower centre is (0,-height/2,0); the upper centre is
   (offset.x,height/2,offset.y). Rotation is about the upper centre, regardless
   of partial sweep, so there is no sector-centroid compensation.
2. Generates its own end profiles, wall strips, end caps and radial cuts.
   Positive sweep runs from +X toward +Z, negative reverses direction. A zero
   end radius creates a shared apex; either end may be the apex.
3. smoothSides=true smooths only edges between wall faces, including internal
   diagonals for triangular sides. Both caps and both sweep-cut faces stay
   sharp, even when their dihedral angle is smaller than wall-to-wall angles.
4. Default wall topology is quads; triangulateSides=true uses two triangles
   per strip, with a consistent bottom-i to top-(i+1) diagonal. Cap polygons
   are unchanged. Exact source hard_edge-dependent diagonal conventions are
   not reproduced by this single fixed diagonal policy.
5. Sweep clamps to +/-360 degrees and tests full sweep using exact equality.
   Nonzero tiny sweeps are not promoted to a minimum angle. Zero sweep or both
   radii zero return an empty mesh. Height and scale components must be positive;
   radii must be nonnegative. Singular/reflected end transforms and emulation
   of Maquette's degenerate zero-sweep cylinder remain outside this API.
6. No boundary intent flags, vertex index lists, special tags, or extra Loft
   callbacks were added. Existing Loft APIs remain available and unchanged.
7. Authored regression cases cover closed signed sweeps and frustum volumes,
   both apex directions, independently transformed ends, triangle/quad walls,
   smoothing of the three-strip 300-degree counterexample, and angular limits.
   Static whitespace checks pass. Unity compilation and runtime execution have
   not been performed, in accordance with the package checkout's AGENTS.md.

This is a direct Polyhydra API only; UI, enum/recipe registration, serialization,
and Open Brush integration remain deferred. No Microsoft implementation code
or assets were copied into this generator.


17. Negative parabola offsets / axis-crossing profiles (2026-10-10)

17.1 Source behavior

1. center_offset is an exposed float with declared range -1..1. Parabola
   radius is .5 and radial coordinate is .5*(u*u/(4*major_axis)+center_offset),
   where u=-cos(theta). Maquette retains its sign: it does not clamp at zero,
   take an absolute radius, insert roots, or split/rebuild the profile.
2. The builder connects sampled rings with fixed triangle winding regardless
   of radial sign. Consequently mixed-sign profiles can fold/self-intersect.
   Its partial-sweep cap fans also do not repair the resulting intersections.
   Negative radii shift the corresponding angular points by 180 degrees; for
   a partial sweep this changes which side of the axis those patches occupy.
3. At major_axis=1, center_offset=-.1 and full clips, roots are at
   u=+/-sqrt(.4). A 16-segment profile changes sign between rows 4/5 and 11/12.
   At offset=-.125 roots coincide approximately with rows 4 and 12. Offsets
   below -.25 put the entire sampled curve on the negative radial side.
4. None of the 92 parabola entries in the two bundled procedural scene files
   has negative center_offset. This is a genuinely exposed option, not an
   observed requirement in the currently available example corpus.

17.2 Concrete Polyhydra bug and fix

1. ConicProfile already retains signed coordinates correctly. Its axis closure
   can therefore self-intersect as a planar polygon; it does not sanitize or
   split that input. This behavior is now documented on the API.
2. Revolve previously reversed each generated face independently to align with
   an expected normal. Across a signed-radius transition this can reverse one
   adjacent strip but not another, leaving same-direction shared edges that
   Halfedges.MatchPairs cannot pair. Partial caps were oriented independently
   from a centroid tangent and could also disagree with adjacent strips.
3. Revolve now determines a common winding from the first nondegenerate strip
   orientation and applies it consistently to the whole surface. Start/end
   cap boundary order is derived from strip connectivity, not independently
   guessed from centroid position. There are no new public parameters.
4. Independent analytic graph checks used 12 angular strips, 16 profile
   segments, radius .5, major_axis=1 and offset=-.1: old full sweep has 48
   unpaired directed edges; old quarter sweep has 76. The consistent winding
   has zero for both. Positive-offset, fully negative, sampled-axis-contact,
   and both signed quarter sweeps were also checked.
5. These are Python math/connectivity checks, not execution of Unity or the
   production mesh builders. Script and results are local at
   Temp/MaquetteAnalysis/analyze-signed-revolve.py and
   Temp/MaquetteAnalysis/signed-parabola-winding-audit.txt.
6. Authored Unity regression cases exercise those signed profiles, including
   X/Z-axis equivalents, retained negative coordinates and paired halfedges.
   Existing positive conic/capsule checks remain in place. Static whitespace
   checks pass; Unity compilation and runtime execution remain pending.

17.3 Limits: edge pairing is not geometric validity or exact source rendering

1. Consistent winding fixes broken connectivity without silently altering the
   sampled parameterization. It does not turn folded source geometry into a
   non-self-intersecting solid. A sampled axis contact can have a singular
   vertex even when every halfedge has a pair; PolyMesh.IsValid alone is not a
   proof of a manifold solid.
2. An axis-crossing strip can be a self-intersecting quadrilateral. Maquette's
   two-triangle strip and Polyhydra's polygon triangulation can produce
   different piecewise surfaces there. Self-intersecting partial cap polygons
   are likewise not guaranteed to tessellate like Maquette's triangle fans.
   Therefore this fixes a concrete Revolve topology defect, but does not claim
   lossless rendered geometry for every negative-offset source shape.
3. Exact reproduction of the source folded triangle surfaces versus repaired
   solids is a separate compatibility decision. No new conic solid generator,
   repair mode, source-specific winding option or importer geometry code was
   introduced during this fix.


Arc consolidation (2026-10-10)

The separate Sector shape has been removed. Shapes.Build(ShapeTypes.Arc,
segments, 0f, turns) produces a single filled face in XY. Partial sweeps use
one centre vertex and segments + 1 perimeter vertices; full turns use only
segments perimeter vertices. Earlier Sector examples above are superseded.
Extrude this filled Arc along Z. Cylinder remains the dedicated solid generator.


Smoothing coverage review (2026-10-10)

1. Cylinder: smoothSides already selects wall/wall edges and excludes end caps
   and radial cuts. No further smoothing API is established as necessary.
2. Capsule: smooth already selects the curved surface and excludes sweep cuts.
3. Rounded Rectangle and Frame: ordinary cases remain covered by AutoSmooth;
   no new corner metadata or solid generator is justified by this review.
4. Conic sections: AutoSmooth is insufficient for some exposed low-resolution
   partial sweeps. Circle, full vertical clips, vertical_segments=2, sides=3,
   sweep=300 degrees has adjacent body-face angles of 80.241 degrees
   and body/cut angles of 49.879 degrees. A threshold that smooths the
   body also smooths the cuts. These values are an analytic geometry check,
   not an execution of Unity or Maquette. The decompiled Conic_Section builder
   independently allocates body, sweep cuts and end-cap vertices before
   recalculating normals, establishing that those boundaries remain sharp.
   Existing halfedge flags can represent this. The unresolved design is how
   conic solid construction assigns them; generic Revolve must not infer
   curve intent from arbitrary profiles. No implementation added here.
5. Exact rendered normals remain unverified: boundary smoothing coverage does
   not establish matching triangle tessellation or normal weighting.

Ring and Arc remain separate existing APIs. The earlier filled-Arc fix is
also used by Ring at B=0; no Ring consolidation was retained.


Conic solid construction implemented (2026-10-10)

VariousSolids.Conic(type, sides=24, verticalSegments=16, radius=1,
majorAxis=1, minorAxis=1, centerOffset=.2, bottomClip=0, topClip=1,
sweepAngle=360, smooth=false, triangulateSides=false,
zeroSweepBehavior=ThinWedge) now provides construction-aware body smoothing.
It shares a private cross-section sweep routine with Cylinder. Cylinder's
public API and its transformed-end construction are retained. ConicProfile
and the new solid share one internal curve sampler; generic Revolve is unchanged.

Positive sweeps run from +X toward +Z, matching Cylinder and the source sweep
convention; unlike the earlier direct Revolve recipe, do not negate sweepAngle.
Body faces are tracked separately from end caps and radial sweep cuts. Smooth
marks only body/body edges. Caps/cuts stay sharp; wall quads are the default
and optional triangles use the same diagonal as Cylinder. Signed radial
profiles remain signed; folded/self-intersecting source cases are not repaired.

Zero sweep retains the Capsule/Revolve ThinWedge default with explicit Empty
and DoubleSidedProfile options. Equal/reversed clip rejection remains inherited
from ConicProfile. Exact triangle tessellation/normal weighting parity remains
unverified. Regression cases cover profile/revolution position equivalence,
signed/clipped closure and selective smoothing of the low-resolution example.
Unity compilation and test execution are left to the user per project guidance.
No Open Brush integration or settings/UI changes were added.
