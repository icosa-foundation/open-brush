Procedural serialization requirements: Maquette and ShapesXR
2026-10-23. Static requirements audit; not a finalized schema.

1. Scope
This document records procedural import requirements, preset parameters and
native-space observations. It does not establish coverage of every current
application feature or exact rendered parity.

2. Actual parametric preset catalogue
The catalogue contains 18 presets: 11 visible library entries and 7 hidden legacy entries.
These combine panel construction with ordered X and Y bends. Preset names are initial
parameter combinations; they are not separate saved procedural generator families.

Name | Visible | Size XY | Thickness | Corner | Chamfer | Back | Bend X,Y | Preset asset
Backplate | 1 | 0.2,0.2 | 0.008 | 0.016 | 0.002 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/01.Backplate.asset
Cube | 1 | 1.0,1.0 | 1 | 0 | 0 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/02.Cube.asset
Rounded Cube | 1 | 1.0,1.0 | 1 | 0.015 | 0.015 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/03.CubeRounded.asset
Plane | 1 | 1.0,1.0 | 0.0002 | 0 | 0 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/04.Plane.asset
Curve plane | 1 | 1.0,0.5 | 0.001 | 0.02 | 0 | 1 | 0,-60 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/05.CurveUI.asset
Sphere | 1 | 1.0,0.5 | 0.0001 | 0 | 0 | 0 | 180,360 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/06.Sphere.asset
Inverted Sphere | 1 | 1.0,0.5 | 0.0001 | 0 | 0 | 0 | -180,-360 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/07.Sphere_Inverted.asset
Cylinder | 1 | 0.7,0.7 | 1 | 10 | 0 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/08.Cylinder.asset
Tube | 1 | 1.0,0.5 | 0.05 | 0 | 0 | 1 | 0,360 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/09.Tube.asset
Capsule | 1 | 0.5,0.5 | 1 | 10 | 10 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/10.Capsule.asset
Torus | 1 | 1.0,0.5 | 0.0002 | 0 | 0 | 0 | 360,-360 | ShapesXR/Runtime/Content/Presets/AssetPacks/2.0/Library/010_Procedural/11.Torus.asset
Button | 0 | 0.16,0.038 | 0.004 | 0.008 | 0 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/Button.asset
Button Rounded | 0 | 0.12,0.038 | 0.004 | 0.016 | 0.001 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/ButtonBubble.asset
Button Chamfer | 0 | 0.16,0.038 | 0.004 | 0.008 | 0.001 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/ButtonChamfer.asset
Button Chamfer | 0 | 0.16,0.038 | 0.004 | 0.008 | 0.001 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/ButtonRounded.asset
Chamfer Cube | 0 | 1.0,1.0 | 1 | 0.05 | 0.05 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/CubeChamfer.asset
Plane Rounded | 0 | 1.0,1.0 | 0.0002 | 0.05 | 0 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/PlaneRounded.asset
Sphere | 0 | 1.0,1.0 | 1 | 10 | 10 | 1 | 0,0 | ShapesXR/Runtime/Content/Presets/AssetPacks/discartedPrimitives/Sphere.asset

3. Saved editable properties and generation semantics
3.1 Geometry size is a Vector3: width, height and thickness.
3.2 Corner radius is common to the four corners; a four-bit mask selects which corners
    are rounded. A sharpness flag selects chamfered versus rounded corner sampling.
3.3 Chamfer/edge radius and its sharpness flag are separate from planar corner rounding.
    The source uses one segment for sharp profiles and eight for rounded profiles.
3.4 Generate Back controls geometry, not simply two-sided material rendering. Sides are
    also conditional on thickness. An open panel cannot be treated as a closed Shell.
3.5 Corner radius clamps to half the smaller XY dimension. Chamfer clamps to half the
    smallest of width, height and thickness. Large-radius presets intentionally use
    those clamps to make cylinder, capsule and sphere-like shapes.
3.6 Before bending, width/height sampling is chosen from the bend angles: one segment
    for zero bend, otherwise approximately 8..64 across the relevant direction.
    A single polygon extruded with Shell is not an equivalent bending substrate.
3.7 The ordered bend pair uses signed degrees and supports full 360-degree wraps.
    Both bends use the ORIGINAL generator bounds, not bounds recomputed after bend one.
    Reference-frame/bounds semantics must be specified, whether encoded explicitly or
    implied by a precisely defined construction. Axis plus angle alone is ambiguous.
3.8 Shading normals are generated for separate surface regions and transformed during
    bending. Topology sampling, triangle layout, normals and UV semantics need defined
    behavior; preserving only an ideal continuous surface is not exact mesh parity.

4. Other reachable geometry paths
4.1 Ten copied-mesh presets reference a source mesh
    with piecewise border-preserving resizing. Examples include WireframeCubeMesh,
    CircleButton variants, FrameStroke, Rectangle, Smartphone, Stool and Table.
    These are mesh resources rather than members of the parametric panel family.
    This needs a mesh source plus an appropriate deformation if preserving resizing
    editability. It does not justify ten new analytic generator types.
4.2 Image and Figma resources remain resource objects rather than procedural solids.
4.3 Additional generator types need an importable preset or native fixture before
    they become confirmed import requirements.

5. Native-space checks
Focused local search: top-level Downloads/Documents, plus existing workspace analysis.
Found Downloads/shapesxr.mp4, not a native fixture in those searched locations.
Two published shared codes were resolved through the same endpoint used by the plugin:
https://api.shapes.app/accesscode/space-url/{code}
No authenticated account access or scene modifications were performed.
5.1 a54rscc7, author-published prototype:
https://spatialandimmersivedesign.substack.com/p/learn-spatial-design-immersive-training
    985,940 bytes; legacy header without explicit schema version; 22,148 edit records.
    382 distinct procedural-property owners matched Cube (374) and Backplate (8).
5.2 265ez32e, author-published prototype:
https://devpost.com/software/creta-learning-pottery-through-the-past
    269,750 bytes; schema 3; 6,174 edit records.
    75 distinct procedural-property owners matched Plane (66) and Cube (9).
5.3 Both contain saved corner/chamfer/sharpness/back-face and bend properties. All sampled
    corner masks were all-four enabled; all sampled bend angles were zero. Thus nonzero
    bends and mixed corner masks are established by the shipped presets/importer code,
    not demonstrated by these downloaded spaces. Counts are property owners after last
    write per entity/property, without executing migrations or proving active visibility.
5.4 These older schemas use unprefixed property names, unlike current snapshot-prefixed
    keys. They were decoded generically as MessagePack; no migration round trip was run.
5.5 One other published code, EW9XZ5A5, returned HTTP 400. It was not a usable fixture.

6. Mapping alongside Maquette
6.1 Maquette CylinderCone -> Cylinder with independent end radii/aspects, top transform,
    signed sweep, body smoothing and inferred warped-strip triangulation.
6.2 Maquette RoundedCornerQuad -> Rectangle + Shell, with source pivot and scale mapping.
6.3 Maquette RoundedCornerRim -> Frame + Shell, with inner/outer-radius interpretation.
6.4 Maquette ConicSection -> Conic, including curve choice, clipping and signed sweep.
6.5 Maquette Capsule -> Capsule with source dimension/scale compensation.
    Earlier assessment details exact mapping and remaining degeneracies; this audit does
    not upgrade those mappings to a lossless-parity claim.
6.6 ShapesXR shared panel family -> NOT fully covered by current Rectangle + Shell.
    It additionally needs mixed rounded/square corners, independent 3D edge shaping,
    controlled open surfaces, and sampling suitable for subsequent two-axis bending.
6.7 ShapesXR nonzero bending -> no working equivalent Bend implementation was found in
    Polyhydra runtime. Transform or Rotate is not a substitute.
6.8 ShapesXR copied-mesh stretch -> Polyhydra Stretch(float) is not evidence of equivalent
    piecewise border-preserving resizing. This is a separate mesh/deformation requirement.

7. Consequences for the proposed common format
7.1 Generator + named typed parameters + ordered operations remains a viable outer
    structure, but the five-generator catalogue is not sufficient for ShapesXR parity.
7.2 Do not serialize ShapesXR preset names as separate generator types, or add all
    Polyhydra generator variants merely because they exist.
7.3 Resolve how to represent the panel requirements (a dedicated construction versus
    practical composition) before finalizing parameter contracts. A plain Rectangle
    with one radius and a closed Shell cannot stand in for the audited family.
7.4 Specify sampling, open/closed surface semantics, smoothing and ordered bend reference
    frames before writing the permanent .tilt contract. More user-facing knobs are not
    automatically required: some behavior can be fixed and versioned by the generator.
7.5 Native mesh resources plus resizing must be distinguished from parametric sources.
    There is still no requirement for a duplicate evaluated mesh fallback per recipe.
7.6 The existing GeometryRecipe runtime API is provisional; it has no .tilt serialization.
    No runtime API, schema, generator or Open Brush implementation was changed in this audit.

8. Validation references
Native-space checks and fixture cases are recorded below.

9. Current-format user fixture (c198see1)
The user supplied this space code for analysis. Do not publish or commit the fixture.
Downloaded 18,975 bytes through the normal plugin endpoint; no app execution.
SHA256 f7e26a4f292f4f0429896e749a7e95554e362b309746c08efd87a4998fe4a89f.
Header [5, [3, 468]]: explicit schema version 5 and 468 edit records.
Found 10 snapshots with complete procedural property sets, referring to 10 distinct object entities.
All geometry values below come from saved snapshot properties, not inferred defaults.
Object preset | Corner mask | Corner sharp | Chamfer sharp | Back | Bend X,Y | Corner | Chamfer
Curve plane | 15 | False | False | True | 0,-60 | 0.02 | 0
Curve plane | 15 | False | False | True | 0,-60 | 0.02 | 0
Backplate | 15 | False | False | True | 0,0 | 0.016 | 0.002
Tube | 15 | False | False | True | 0,360 | 0 | 0
Backplate | 15 | False | False | True | 0,0 | 0.016 | 0.002
Backplate | 15 | False | False | True | 0,0 | 0.016 | 0.002
Backplate | 15 | False | False | True | 0,0 | 0.016 | 0.002
Capsule | 15 | True | True | True | 13,20 | 0.192 | 0.125
Torus | 0 | True | True | False | 360,-360 | 0.225 | 0.03
Backplate | 9 | False | True | True | -7,-5 | 0.081 | 0

9.1 Saved corner masks include 15 (all four), 0 (none), and 9 (a mixed selection).
9.2 Both independent sharpness flags are present; one snapshot has rounded corners
    but sharp chamfer, while other modified snapshots enable both sharpness flags.
9.3 Generate Back is false on the modified Torus snapshot. It is not just a material flag.
9.4 The fixture contains a complete wrap (Tube 0/360), double wrap (modified Torus
    360/-360), and nonzero bends on both axes (13/20 and -7/-5).
9.5 The modified Capsule preset has corner=.192, chamfer=.125, both sharpness flags
    true and bends 13/20. A conventional Capsule recipe selected by preset name cannot
    express that configuration. The modified Torus likewise has nondefault shape flags.
9.6 Source geometry parameters live on snapshots, which reference object entity IDs.
    The importer must resolve a chosen state and construct a self-contained recipe from
    its actual properties; the source preset ID can supply defaults but is not a recipe.
9.7 This fixture supplies current-version evidence for the previously static-only corner,
    back-face and bending requirements. Decoding was execution-free; visual/mesh parity
    and state-resolution behavior have not been run through Unity.

10. Existing-modifier assessment (2026-10-23)
Scope: static code assessment of the current local Polyhydra checkout against the
ShapesXR panel construction requirements confirmed in the user fixture. No new runtime
code, generator, modifier or file-format changes, and no Unity mesh-parity execution.

10.1 Open backs: existing functionality
Shell can construct the closed extrusion and FaceRemove can remove selected faces.
FaceRemove(Filter) supports face predicates; FacingDirection can select the rear cap.
For beveled panels, the removed rear region must include the rear bevel faces too,
before any bending. For source thin sheets, retain both planes when Generate Back is
enabled, otherwise retain only the front. Remove the walls and bevels in either case.
The earlier front-only rule was too broad; the source still generates its back plane
at thickness<=0.0001 when Generate Back is enabled. Exactly zero depth still needs
Polyhydra support. For a front-only selection, removing non-front faces also
removes the narrow wall faces. This is a selection/composition requirement, not evidence
for a new open-back generator or changing Rectangle's single-face topology.
Evidence: PolyMesh.Geometry.cs FaceRemove overloads, Shell; Filters.cs FacingDirection.

10.2 Mixed rounded/square outline corners: missing direct control
Shapes.Rectangle calls RectangleContour with one radius and segment count for all four
corners. It has no per-corner selection. Existing Truncate/Chamfer/Bevel operations do
not directly add independently sampled circular arcs to selected corners of this planar
profile. Vertex movement alone cannot add the required samples. A shared contour helper
can support corner selection without a second outline implementation. This does not
require separate shape types for each corner combination.
Evidence: Generators/Shapes.cs Rectangle and RectangleContour.

10.3 Independent edge rounding/chamfer: not a practical existing composition
Bevel places new points by fractions along face edges and towards face centroids.
Chamfer/OldChamfer are topological operations with relative amount/offset controls.
Neither supplies a world-radius sampled fillet independently of outline rounding.
ChamferedBox builds a cube with a shared three-axis edge/corner construction. Scaling
it to an arbitrary panel changes radii anisotropically; it does not provide independent
outline radius, thickness-edge radius and selected square corners.

ExtrudeAlongProfile IS relevant: it constructs multiple rings with specified offsets,
but each ring's in-plane profile is a scalar interpolation towards the face centroid.
This is homothetic scaling, not a parallel offset preserving circular corner geometry.
Concrete example: a width=1, height=.5, cornerRadius=.1 outline inset by .02 needs
width=.96, height=.46 and radius=.08. Independent axis scales .96 and .92 would produce
corner arc radii .096 and .092, not the required circular .08. ExtrudeAlongProfile's
single centroid ratio cannot even independently provide those width/height scales.

FaceInset does provide polygon offsets, but it returns disconnected inset faces rather
than a connected stack. The transform Loft overload accepts one face and transforms it;
it does not connect a series of independently offset/resampled contours. Offset contours
can change vertex count at radius collapse. A pipeline would have to manage ring
correspondence, caps, smooth/hard boundaries and collapse cases. Implementing that
machinery is a new construction algorithm, not a short existing-modifier recipe.

Evidence: PolyMesh.ConwayOps.cs Bevel, Chamfer, OldChamfer, transform Loft;
PolyMesh.Geometry.cs FaceInset, ExtrudeAlongProfile; VariousSolids.ChamferedBox.
The comment-only ConnectFaces implementation is not an available joining operation.

10.4 Sampling for bends: current generic subdivision is not an equivalent construction
Subdivide is a topological face/edge subdivision. It does not expose independently
controlled horizontal/vertical sampling of the panel's interior plus its rounded and
beveled boundary regions. Grids can produce rectangular sampling but do not directly
join that interior to selectively rounded corners and depth-edge fillets.
Subdivision may provide an approximation, but a reliable exact source-matching layout
would require considerable special assembly. Sampling should be owned by the same
construction that builds those surface regions. Rectangle should remain a single face
when used as an ordinary profile; do not change its default topology for this purpose.

10.5 Bending: genuine missing reusable operation
No working Bend implementation was found in the current runtime. Rotate and affine
transform cannot substitute for a spatially varying bend. PolyMesh.Stretch(float) is a
separate Y-coordinate displacement routine, not bending or border-preserving resizing.
The bend needs a specified axis/frame and reference bounds. For ShapesXR's ordered pair,
both operations receive the original generator bounds, not a second automatically
recomputed bound. Output must have adequate samples, correct smoothing, and defined
triangle behavior for warped faces; moving four corner vertices alone is insufficient.

10.6 Recommended minimum changes, subject to API discussion
A. One dedicated panel/box construction for independent outline/edge shaping and
   structured surface sampling. It can reuse and extend the private RectangleContour
   helper for corner selection; do not create a parallel rectangle algorithm. Existing
   Rectangle/Frame retain their profile behavior. This meets the project's rule for
   new functionality: the modifier-only alternative needs substantial new machinery.
B. One reusable Bend modifier with explicit reference-frame semantics.
C. Reuse FaceRemove and existing filtering for missing rear surfaces, rather than
   introducing another shape or special vendor-only open-back operation.
D. Add recipe adapters for existing operations where needed; that is a recipe-exposure
   gap, distinct from missing mesh-generation functionality.

This assessment does not choose a final generator name/signature, add new serialized
fields or change either Open Brush branch. A generic profiled-extrusion extension could
be explored as an alternative, but it would still need the offset, correspondence,
fillet, smoothing and cap-sampling work identified above. It is not existing parity.

11. Implementation status after the modifier assessment
Polyhydra commits dd0d8f8 and 1c4e093 implement Panel and explicit-bounds Bend, plus
provisional recipe adapters and six gallery examples. Rectangle still returns one
face, with optional corner selection using its shared contour helper. Panel is an XY
solid with Z depth; it has independent outline and depth-edge sampling, plus width/
height cap sampling. FaceRemove is reused to omit rear geometry before bending.
Bend takes signed degrees, an axis and reference Bounds. Both bends can explicitly
use the same original bounds. New warped quads are split; smoothing flags propagate.
No ShapesXR source or assets were incorporated into the implementation.
Geometry regression cases were added, but Unity compilation, test execution, rendered
normals/UVs and exact source-mesh parity remain unverified. The implementations do not
constitute a finalized .tilt schema. No Open Brush runtime or file-format changes.
