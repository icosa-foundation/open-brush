PROCEDURAL GEOMETRY IN .TILT
Proposed version 1 — Maquette and ShapesXR requirements
2026-10-23

1. STATUS AND SCOPE

This is a proposed serialization contract for review, not a released .tilt extension.
It defines one shared geometry representation for imported objects and Open Brush's
3D Shapes tools, rather than separate vendor-specific procedural formats.

The scope is procedural geometry and source meshes with geometric operations.
Appearance, placement, layers, groups, lights, text, interactions and animation remain
scene/document concerns. The importer resolves a Maquette stage or ShapesXR snapshot
before encoding its geometry; this proposal does not add animation to .tilt.

The audited scope comprises Maquette's five builder families, ShapesXR Unity plugin
V1.31's 11 visible and 7 legacy parametric presets, its source-mesh resizing path, and
the user's version-5 space. Accounting for these in the data model is NOT a claim of
already tested mesh/normal/UV parity. Section 15 identifies implementation blockers.

2. OWNERSHIP

2.1 Polyhydra owns geometry semantics, parameter types, execution order, topology and
    smoothing. It has no dependency on .tilt, Open Brush materials or UI panels.
2.2 Open Brush owns JSON encoding, the container, definition/instance IDs, scene state,
    appearance and preservation of unsupported data.
2.3 Importers translate source parameters into the common representation. They do not
    implement vertex/ring construction or require proprietary presets to rebuild.
2.4 The 3D Shapes branch must adapt to this representation, not maintain another
    permanent serializer/evaluator alongside Maquette import.

3. RECIPE OBJECT

A recipe has one Generator followed by ordered Operations. Both use a stable Type
identifier and named Parameters. Organizational families are not a second stored
hierarchy: no redundant Family/Subtype/GeneratorType fields.

```json
{
  "Version": 1,
  "Generator": {
    "Type": "frame",
    "Parameters": {
      "width": 2.0, "height": 1.0, "borderWidth": 0.1,
      "radius": 0.2, "cornerSegments": 4
    }
  },
  "Operations": [
    {
      "Type": "shell",
      "Parameters": { "thickness": 0.15, "symmetric": true }
    },
    {
      "Type": "transform",
      "Parameters": {
        "translation": [0,0,0],
        "rotation": [0.70710678,0,0,0.70710678],
        "scale": [1,1,1]
      }
    }
  ]
}
```

3.1 Version is a required integer identifying the COMPLETE recipe language, initially
    1. There are no separate generator or operation versions.
3.2 Generator is required and must name a generator, not an operation.
3.3 Missing Operations means []; missing Parameters means {}.
3.4 Type is required, case-sensitive and stable. It is not a CLR class name, reflection
    target, enum ordinal, preset GUID or Inspector control name.
3.5 Parameter names are case-sensitive. Object member order has no meaning; operation
    array order is significant. Unsupported operations must not be silently skipped.
3.6 No evaluated fallback mesh is automatically saved beside a parametric recipe.

4. JSON TYPES, UNITS AND COORDINATES

4.1 Values are native JSON values, never JSON encoded inside strings.
4.2 Float: finite number, written with sufficient precision to round-trip binary32.
    Do not apply Inspector/display rounding to saved values.
4.3 Integer: integral number in range. A reader may accept 4.0 as 4, not truncate 4.5.
4.4 Boolean: true/false, not numbers or strings.
4.5 Vector2: [x,y]. Vector3: [x,y,z]. Components must be finite.
4.6 Quaternion: [x,y,z,w], unit length within tolerance. Writers normalize valid
    rotations; an all-zero quaternion is invalid.
4.7 Enum-like values use documented strings, such as "ellipse".
4.8 Corner selection is a set represented as an array drawn from "topRight",
    "topLeft", "bottomLeft", "bottomRight". [] means none; order is irrelevant.
4.9 null is invalid unless a parameter explicitly permits it. Omission uses its
    versioned default; null does not mean zero or default.
4.10 Proposed .tilt geometry units are model-local metres. Polyhydra remains
     unit-agnostic. Open Brush converts to its canvas units exactly once, without
     silently normalizing to a unit bounding box. RawTransforms keeps its existing
     canvas-unit convention. The adapter/normalization review in section 15 is required.
4.11 Axes follow Unity/Open Brush: X right, Y up, Z forward. Rotations use quaternions
     in that basis. Angle parameters are signed degrees.
4.12 Recipe transforms alter geometry; instance transforms place the result. Bounds
     are in the local coordinate system at the step where they are used.

5. DEFINITIONS AND INSTANCES

Reuse the 3D Shapes branch's definition/instance separation:
EditableModelDefinitions maps file-local IDs to recipes.
EditableModelIndex refers to those IDs through AssetId and places instances.
A definition ID is not an Icosa asset ID or a source application's preset ID.

```json
{
  "EditableModelDefinitions": {
    "frame-1": {
      "Version": 1,
      "Generator": {
        "Type": "frame",
        "Parameters": {
          "width": 2, "height": 1, "borderWidth": 0.1,
          "radius": 0.2, "cornerSegments": 4
        }
      },
      "Operations": [
        { "Type": "shell", "Parameters": { "thickness": 0.15 } }
      ]
    }
  },
  "EditableModelIndex": [
    {
      "AssetId": "frame-1",
      "RawTransforms": [
        [[0,0,0], [0,0,0,1], 1]
      ],
      "PinStates": [true],
      "GroupIds": [0],
      "LayerIds": [0]
    }
  ]
}
```

5.1 This is a metadata.json excerpt, not a complete .tilt document.
5.2 RawTransforms retains [translation, quaternion, uniformScale]. Parallel instance
    arrays follow the existing metadata rules; their missing-value rules are unchanged.
5.3 Definitions may be shared for storage. This does not automatically mean linked
    editing: independently editing one instance forks its definition unless an explicit
    shared-geometry edit is intended.
5.4 Placement, appearance, pinning, groups and layers do not belong inside the recipe.
    This does not define a new material schema or make a material-array index portable.
5.5 Rebuilding must not require a share code, cloud request, proprietary installation
    path or preset asset. An imported object is not a new widget category merely
    because it originated in Maquette or ShapesXR.

6. GENERATORS

The initial parametric catalogue is rectangle, frame, panel, cylinder, capsule and
conic. mesh is an actual-source case, not an automatic fallback. Other 3D Shapes
families can use this structure after their contracts are defined; they are not
required merely to complete these importers.

Defaults below are FORMAT defaults, independent of later C# optional-argument changes.
Writers should explicitly save behavior-affecting values, including sampling and
zero-sweep policy, for imported objects.

6.1 rectangle — one planar XZ profile facing +Y

Parameter          Type        Default   Meaning
width              float       1         Positive full X extent.
height             float       1         Positive full Z extent.
radius             float       0         Common selected-corner radius; >=0.
cornerSegments     integer     4         Samples per quarter corner, 1..128.
roundedCorners     string[]    all four  Logical selected corners; top is +Z.

Radius is capped at half the smaller dimension. Radius zero yields four vertices
regardless of cornerSegments. An unselected corner stays square. The result remains
one face; it does not silently become a grid or solid.

6.2 frame — planar XZ frame facing +Y

Parameter          Type        Default   Meaning
width              float       1         Positive outer X extent.
height             float       1         Positive outer Z extent.
borderWidth        float       0.1       Inward border width; >=0.
radius             float       0         Outer corner radius; >=0.
cornerSegments     integer     4         Samples per quarter corner, 1..128.

The ordinary inner contour is derived from border width, with inner radius
max(radius-borderWidth,0). Faces join corresponding contour sections. Collapsed
openings follow Polyhydra Frame semantics, not an assurance of reproducing every
minimum-radius or malformed source contour.

6.3 panel — sampled XY panel/box with depth along Z

Parameter          Type        Default   Meaning
width              float       1         Positive full X extent.
height             float       1         Positive full Y extent.
depth              float       0.1       Positive full Z extent.
cornerRadius       float       0         Outline radius; >=0.
edgeRadius         float       0         Depth-edge transition radius; >=0.
roundedCorners     string[]    all four  Logical selected corners; top is +Y.
cornerSegments     integer     8         Quarter-outline samples, 1..128.
edgeSegments       integer     8         Quarter-edge samples, 1..128.
widthSegments      integer     1         Interior/straight subdivisions along X; >=1.
heightSegments     integer     1         Interior/straight subdivisions along Y; >=1.

The panel is centred. cornerRadius clamps to min(width,height)/2; edgeRadius clamps
to min(width,height,depth)/2. One segment creates a flat chamfer; additional samples
create rounded transitions with construction-defined smoothing. Outline and depth-edge
shaping are independent. Caps contain samples for bending. The generator produces a
closed solid; faceRemove can omit caps/bevel regions before deformation. Missing
geometry is not a two-sided material setting.

6.4 cylinder — independently shaped ends, centred about Y

Parameter          Type        Default          Meaning
sides              integer     24               Strips across the sweep; >=3.
height             float       1                Positive Y distance between end centres.
bottomRadius       float       0.5              Bottom X radius; >=0.
bottomAspect       float       1                Bottom Z/X ratio; >0.
topRadius          float       0.5              Top X radius; >=0.
topAspect          float       1                Top Z/X ratio; >0.
sweepAngle         float       360              Signed degrees, clamped to [-360,360].
topOffset          Vector2     [0,0]            Top centre's XZ offset.
topRotation        quaternion  [0,0,0,1]        Rotation about the top centre.
smoothSides        boolean     false            Blend between wall strips.

Bottom centre is [0,-height/2,0]; top is [offset.x,height/2,offset.y]. Radius sets X
extent, aspect sets Z extent, then top rotation/offset apply. Either end may collapse
to a point. Zero sweep or two zero radii produce empty geometry.
Planar strips remain quads. Warped strips split automatically: bottom i to top i+1
when smooth, otherwise top i to bottom i+1. Inserted diagonals are smooth; caps and
sweep cuts remain hard. There is no triangle/diagonal Inspector parameter. Positive
sweep goes from +X towards +Z; negative sweep reverses it.

6.5 capsule — hemispherical ends and straight section, about Y

Parameter          Type        Default   Meaning
sides              integer     24        Angular resolution; >=3.
capSegments        integer     8         End-profile samples; >=2 for imports.
cylinderHeight     float       2         Straight section, NOT overall length; >=0.
radius             float       1         Actual radius; >0.
sweepAngle         float       360       Signed degrees in [-360,360].
smooth             boolean     false     Smooth body/ends; preserve sharp sweep cuts.
zeroSweep          string      thinWedge Policy in 6.7.

Nonuniform shape dimensions can use an ordered transform. Source total length and
radius conventions must be converted, not blindly copied into these fields.

6.6 conic — sampled section swept about Y

Parameter          Type        Default   Meaning
curve              string      circle    circle, ellipse, hyperbola, parabola.
sides              integer     24        Angular strips; >=3.
verticalSegments   integer     16        Profile samples; >=2.
radius             float       1         Overall profile scale; >0.
majorAxis          float       1         Used except by circle; >0 when used.
minorAxis          float       1         Used by ellipse/hyperbola; >0 when used.
centerOffset       float       0.2       Parabola radial offset; signed.
bottomClip         float       0         Lower profile fraction in [0,1].
topClip            float       1         Upper profile fraction in [0,1].
sweepAngle         float       360       Signed degrees in [-360,360].
smooth             boolean     false     Smooth body; preserve caps/cuts.
zeroSweep          string      thinWedge Policy in 6.7.

The current ordinary contract requires topClip>bottomClip. Circle ignores axes/offset;
ellipse/hyperbola ignore centerOffset; parabola ignores minorAxis. Defined but irrelevant
values can be retained. Equal/reverse clips are a known source-compatibility gap, not
silently reordered or claimed to be losslessly supported.

Canonical profile: for j=0..verticalSegments, let t be the linear interpolation from
bottomClip to topClip at j/verticalSegments, h=-cos(pi*t), and q=sin(pi*t). At the
unclipped poles t=0 or 1, q is exactly zero. Before multiplying both coordinates by
radius, the (radial,height) pair is:
circle:    (q, h)
ellipse:   (majorAxis*q, minorAxis*h)
hyperbola: (majorAxis*sqrt(1+h*h/(minorAxis*minorAxis)), h)
parabola:  (h*h/(4*majorAxis)+centerOffset, h)
Sweep position is [radial*cos(theta), height, radial*sin(theta)]. Signed radial
values remain signed; folded/self-intersecting profiles are not silently repaired.

6.7 Zero-sweep policies for capsule/conic

thinWedge          Explicit narrow wedge (currently 0.036 degrees).
empty              No geometry.
doubleSidedProfile Planar section with opposing faces, not a closed swept solid.

These policies apply to exactly zero. Very small nonzero Capsule sweeps still require
compatibility verification. A double-sided conic section lies in XY even though the
ordinary solid is swept about Y.

6.8 mesh — explicit geometry as the ACTUAL source (proposed, not implemented)

Parameter          Type           Default   Meaning
vertices           Vector3[]      required  Model-local source positions.
faces              integer[][]    required  Polygon loops indexing vertices.
cornerNormals      Vector3[][]    absent    Optional per-face-corner normals.
cornerUVs          Vector2[][]     absent    Optional per-face-corner UVs.
smoothEdges        integer[2][]   []        Unordered pairs of vertex indices.
faceTags           string[][]     absent    Optional face tags.

Indices are zero-based. Faces require at least three distinct valid indices.
Attribute rows align with face loops. A shared geometric vertex may have different
corner normals/UVs on different faces: do not collapse these into one vertex normal.
Authored source attributes must be preserved; absence requests documented derived
attributes. Exact attribute support remains an implementation requirement.

This is for actual mesh sources, direct vertex editing and the CopyMeshGenerator
path, not duplicate fallback data for every procedural object. Store a source once
per definition, then its operations. Resource-backed models may continue using the
existing model-resource path; proprietary preset IDs alone are not portable sources.

7. OPERATIONS

Each consumes the previous result. Coordinates/bounds are local to THAT step.
Parameters have semantic names, not generic Param1/Param2 slots.

7.1 shell
thickness: float, default 0.1; separation between the two surfaces.
symmetric: boolean, default true; offset half on either side, otherwise keep the
original surface and offset the other. Shell closes the original boundaries.

7.2 transform
translation: Vector3, default [0,0,0].
rotation: quaternion, default [0,0,0,1].
scale: Vector3, default [1,1,1].

Apply componentwise scale, rotation, then translation about the local origin.
Represent a different pivot with surrounding translations, not a hidden preset.
Reflections reverse winding to retain orientation. Explicit normals, when supported,
need inverse-transpose transformation and normalization. Current Polyhydra derives
normals; preservation of arbitrary imported attributes remains pending. Do not apply
this scale again as instance scale.

7.3 faceRemove
direction: Vector3, default [0,0,-1]; nonzero selection direction.
angle: float, default 0.1; degrees, with strict less-than comparison.
invert: boolean, default false; invert the face selection.

Remove faces whose current geometric normals lie within angle of direction. For an
open panel, remove the cap and appropriate rear bevels BEFORE bending. This is one
exposed selection mode, not a claim that Polyhydra lacks other face filters.

7.4 bend
angle: finite float, default 0; signed degrees across the reference span.
axis: string, default x; x, y or z.
referenceCenter: Vector3, REQUIRED.
referenceSize: Vector3, REQUIRED; nonnegative dimensions, positive bend span.

Fixed frames:
x varies along Y and bends towards +Z; X is unaffected.
y varies along X and bends towards +Z; Y is unaffected.
z varies along X and bends towards -Y; Z is unaffected.

Bounds are explicit data, not guessed from earlier/future operations. For ShapesXR's
two bends, write the SAME original generator bounds into both operations. Do not
recompute the second bounds from the first bent result. Span is height for x and
width for y/z. Zero angle is identity. Bend does not invent surface samples. Warped
quads split on vertex 0-to-2; old smoothing boundaries remain and new diagonals smooth.

Canonical mapping: subtract referenceCenter, project onto the frame's along/depth
coordinates u,v, and let L be the reference span and a be angle in radians. For a!=0,
theta=a*u/L and R=L/a. Replace u with (R-v)*sin(theta) and v with
R-(R-v)*cos(theta), retaining the unaffected coordinate, then restore the centre.
Implementations should evaluate this stably near zero rather than subtract large
nearly equal radii. Explicitly repeated bounds make this mapping deterministic.

7.5 autoSmooth
angle: float, default 60; maximum dihedral in degrees for the existing AutoSmooth rule.
This is explicit shading intent, not a blanket replacement for construction-specific
hard boundaries such as partial cylinder cuts or independently shaped panel edges.

7.6 flip
No parameters. Reverse winding.

7.7 stretch — border-preserving source-mesh resizing (required, NOT implemented)

Distinct from transform and from the existing Polyhydra.Stretch(float).
The audited mesh-resizing path requires this proposed typed input contract:
size: Vector3 target dimensions.
referenceCenter/referenceSize: original source-mesh bounds.
lowerSlice/upperSlice: Vector3 cuts normalized within original bounds, in [0,1].
pivot: Vector3 normalized target anchor, default [0.5,0.5,0.5].
preScale: positive float, default 1; source normalization before resizing.
stretchInterior: boolean, default true.

The operation moves outer regions while preserving border thickness and optionally
stretches the interior. It must handle targets smaller than the combined original
borders. Final point-mapping/fitting semantics, pivot behavior and zero-width regions
need implementation and verification on a source-mesh fixture BEFORE this operation
can be frozen in version 1. These are explicit remaining design requirements, not
undocumented reader defaults. The common step shape can retain these values today;
retention is not executable resize parity.

8. MAQUETTE MAPPING

8.1 CylinderCone -> cylinder
bottomRadius = radius_bottom * bottom_scale.x
bottomAspect = bottom_scale.y / bottom_scale.x
topRadius = radius_top * top_scale.x
topAspect = top_scale.y / top_scale.x
height = 1 in source-local geometry.
topOffset = top_offset; topRotation = quaternion from Unity top_rotation Euler angles.
sweepAngle = sweep_angle; sides = sides; smoothSides = !hard_edge.
Append flip for invert. Singular/reflected end scales need an explicit unsupported-
case policy, not division by zero or discarded values.

8.2 RoundedCornerQuad -> rectangle + shell + orientation/pivot transforms
Map width/height/radius/sampling to the profile, and thickness to Shell. Source object
scale affects the radius clamp: calculate working dimensions and compensate retained
instance scaling. Rotate XZ into source XY and restore the source pivot explicitly.
Do not confuse radius-preserving regeneration with stretching the finished mesh.

8.3 RoundedCornerRim -> frame + shell + orientation/pivot transforms
Source radius is INNER radius. For ordinary rounded contours, recipe outer radius is
inner radius + border. Square-corner mode maps to zero radius. scale_border affects
converted border width. Source minimum-radius/collapsed-opening behavior is not fully
equivalent to the current Frame contract; see section 15.

8.4 ConicSection -> conic
Translate curve to its semantic name. Convert source dimensions into the canonical
profile convention; preserve clips, sampling and signed sweep. Source radius has a
diameter-like convention and must be converted to actual dimensions. The detailed
Maquette audit records its profile equations; similar field names do not establish
identical meanings. Append flip for invert.

8.5 Capsule -> capsule + compensation/orientation
Convert total source length and radius to actual radius/cylinderHeight. Preserve
sampling/sweep; round_profile and object scale affect effective dimensions. Append
flip for invert. The special zero-sweep upper-cap section can use a conic circle,
clipped to its upper half, with doubleSidedProfile at zero sweep and a translation
to the cap centre. It does not require a source-vendor generator type.

8.6 Hierarchies, scales and pivots
Resolve the selected source state and hierarchy. Shape-affecting nonuniform transforms
belong in the recipe; ordinary placement remains on the instance. Nested nonuniform
scale plus rotation may produce shear: preserve an equivalent sequence of transforms,
rather than silently losing shear in a single TRS decomposition.

9. SHAPESXR PARAMETRIC MAPPING

9.1 Resolve actual snapshot properties, not merely preset names
Version-5 geometry properties live on snapshots referencing object entities. Resolve
the chosen scene state and inherited values first. A preset supplies defaults and
construction information; the recipe stores resolved geometry, not a preset GUID.

9.2 Shared panel
obj-pmugs xyz -> width, height, depth.
obj-pmugcor -> cornerRadius.
obj-pmugcos true -> cornerSegments 1; false -> 8.
obj-pmugchr -> edgeRadius.
obj-pmugchs true -> edgeSegments 1; false -> 8.
obj-pmugcors -> logical roundedCorners names, not copied C# enum bits.

For the inspected plugin's forward surface (-Z), source bits are:
bit 0 topLeft; bit 1 bottomLeft; bit 2 bottomRight; bit 3 topRight.
Source mask 9 therefore means topLeft + topRight. Polyhydra's C# corner flags have a
DIFFERENT bit order. Named JSON corners deliberately avoid dependence on either.

9.3 Sampling
The inspected source chooses one segment for an unbent direction; otherwise:
round(64 * clamp(abs(angle)/360, 0.125, 1)).
Bend x determines heightSegments; bend y determines widthSegments. Save the resulting
integers explicitly. Preset names/defaults alone are not a sampling contract.

9.4 Omitted surfaces and orientation
The source retained forward surface faces -Z. Generate Back controls its +Z surface.
Without reorientation, obj-pmuggb=false removes +Z faces and rear bevels, NOT -Z.
Use faceRemove with direction [0,0,1] and a range near 90 degrees before bending.
The source also omits side walls at its thin-sheet threshold, depth<=0.0001. Use the
appropriate front-only selection for that case. A material culling change is not
an equivalent representation of these missing surfaces.

9.5 Bends
obj-pmbh -> axis x; obj-pmbv -> axis y, in that order. Both reference centres are zero
for the centred base panel; both sizes are the ORIGINAL width/height/depth. They are
not the bounds after face removal or the first bend. Keep signed/full-wrap angles.

9.6 Presets
Backplate, Cube, Rounded Cube, Plane, Curve plane, Sphere, Inverted Sphere, Cylinder,
Tube, Capsule and Torus all configure the same inspected parametric base prefab.
Seven hidden legacy presets also configure it. Names are initial combinations, not
separate generator identities. The user's Capsule object has altered corner/chamfer
settings and bends 13/20; mapping its name to capsule would produce the wrong object.

9.7 Source meshes
The ten CopyMeshGenerator + StretchDeformer presets need a mesh source plus resizing,
not ten analytic generators. Preserve their source attributes and deformation inputs.
Image/Figma/resource objects remain appropriate resource types, not analytic solids.
This path has the explicit stretch/mesh implementation blockers described above.

10. JSON EXAMPLE: MODIFIED SHAPESXR PANEL

This combines source mask 9, omitted +Z rear surfaces, and the 13/20 bend pair from
the user's space. Dimensions are illustrative metres, not a claim that every value
below was copied from one object.

```json
{
  "Version": 1,
  "Generator": {
    "Type": "panel",
    "Parameters": {
      "width": 0.4, "height": 0.3, "depth": 0.05,
      "cornerRadius": 0.08,
      "roundedCorners": ["topLeft", "topRight"],
      "edgeRadius": 0.01,
      "cornerSegments": 8, "edgeSegments": 1,
      "widthSegments": 8, "heightSegments": 8
    }
  },
  "Operations": [
    {
      "Type": "faceRemove",
      "Parameters": { "direction": [0,0,1], "angle": 89.9, "invert": false }
    },
    {
      "Type": "bend",
      "Parameters": {
        "angle": 13, "axis": "x",
        "referenceCenter": [0,0,0], "referenceSize": [0.4,0.3,0.05]
      }
    },
    {
      "Type": "bend",
      "Parameters": {
        "angle": 20, "axis": "y",
        "referenceCenter": [0,0,0], "referenceSize": [0.4,0.3,0.05]
      }
    }
  ]
}
```

The bounds are intentionally identical in both bend operations. No vendor component
graph or preset asset is needed by a reader of this recipe.

11. NONUNIFORM SCALE AND AUTHORING

11.1 Keep nonuniform scale as a recipe transform while the instance uses ordinary
     uniform placement. Rebuilding applies the same shape transform again.
11.2 Never bake that scale into vertices AND save it a second time as another recipe
     or instance transform. Apply the effective transform once.
11.3 Scale compensation follows source construction semantics. Preserving a radius
     while resizing is not the same as stretching the final solid.
11.4 A recipe is authoritative while an object is procedural. Direct vertex edits
     that replace that authoring model explicitly change its source to mesh. Do not
     leave a stale generator recipe attached to newly edited vertex positions.
11.5 These requirements do not add a nonuniform-scale VR interaction UI.

12. VALIDATION AND UNSUPPORTED DATA

12.1 Known values have strict types: no numeric strings, truncated segment counts,
     nonfinite numbers or arbitrary CLR-object deserialization.
12.2 Reject duplicate JSON member names rather than choosing first/last ambiguously.
12.3 Validate the whole recipe before replacing an object with a generated result.
     An unknown operation is not silently skipped while claiming the result is correct.
12.4 Preserve unfamiliar definitions, steps, parameters and extension fields as raw
     JSON on read/save. Preservation does not imply the old evaluator can render them.
12.5 Report unsupported objects; do not discard their saved data or overwrite their
     recipe with an empty mesh. Rendering/placeholder UI is a separate implementation
     decision, not a second procedural representation.
12.6 Missing values use versioned defaults. A misspelled parameter is unsupported
     data, not a request to use a different known parameter's default.
12.7 Radius clamping is documented construction semantics. Arbitrary hidden repairs
     to source geometry are not. Import must report material fidelity reductions.

13. EVOLUTION

13.1 There are no older released versions of this procedural format today. Do not
     add speculative aliases or permanent migrations for current prototype schemas.
13.2 Version 1 covers the recipe language's types, defaults, coordinates, sampling,
     shading and construction semantics as one unit.
13.3 After release, a change in saved meaning requires a deliberate format revision
     and migration/compatibility decision. Renaming implementation methods alone does
     not change the format if stable data identifiers and semantics remain unchanged.
13.4 A new type does not require serializing CLR names. Readers preserve unfamiliar
     recipes rather than substituting a similarly named supported generator.
13.5 Open Brush's existing metadata SchemaVersion is separate. Its update and older
     application behavior need integration review. Older released Open Brush is NOT
     assumed to preserve these new fields automatically.
13.6 Both branches must converge on one representation and Polyhydra evaluator, with
     no Maquette-only definition/index collections. Broader 3D Shapes operation types,
     selections and authoring controls require their own documented contracts before
     release. Any randomized operation needs reproducible saved inputs, not an
     unseeded random result chosen again on every reload.

14. COMPACTNESS

14.1 Analytic objects store parameters, not generated vertices and triangles.
14.2 Multiple placed instances can reference one definition.
14.3 Only actual mesh sources carry topology/attributes. Do not attach a duplicate
     evaluated mesh fallback to every analytic recipe.
14.4 Editor component graphs, thumbnails, snapshot histories, vendor assemblies,
     share codes and access URLs are not saved geometry definitions.

15. IMPLEMENTATION STATUS AND RELEASE BLOCKERS

15.1 Implemented in the local Polyhydra checkout:
     GeometryRecipe/GeometryStep, named parameter evaluation; rectangle, frame, panel,
     cylinder, capsule, conic; shell, transform, faceRemove, bend, autoSmooth and flip;
     gallery consumers. Relevant commits: 121f57a, 5f74d53, dd0d8f8 and 1c4e093.
15.2 Not implemented:
     This JSON serializer/deserializer, .tilt integration and unknown-JSON round trips;
     complete 3D Shapes adapters; source-mesh attribute storage and stretch evaluation;
     native source import and state resolution. No released .tilt uses this contract.
15.3 Not validated:
     Unity compilation/test execution of Panel/Bend; exact triangle, analytic-normal
     and UV equivalence to ShapesXR; native import/save/reload fidelity. Parsing an
     example as JSON proves none of those properties.
15.4 Known source boundary differences requiring decisions or tests:
     Maquette equal/reversed conic clips, tiny/collapsed frame openings, singular end
     scales, degenerate/very small sweeps and source capsule scale limits. Fitting
     fields in a recipe does not make those cases lossless imports.
15.5 Before schema freeze:
     Complete the mesh/stretch contract, verify units and existing widget normalization,
     resolve boundary-case policy, and show representative native objects surviving
     .tilt save/reload without geometry/parameter loss. Include the user's version-5
     corner/bend cases. Implemented capabilities and expressible requirements must
     never be presented as already proven full interoperability.

16. RELATED LOCAL EVIDENCE

16.1 procedural-serialization-requirements.md contains the preset catalogue, native
     fixture findings, modifier assessment and implementation status.
16.2 maquette-compatibility-assessment.md records Maquette's builder/dimension audit
     and remaining boundary differences.
16.3 Third-party decompilation, presets and native spaces remain only in ignored
     Temp/ShapesXRAnalysis and Temp/MaquetteAnalysis. They are analysis evidence,
     not implementation code or assets to commit with this proposal.

This is one shared schema proposal covering the audited requirements and identifying
its remaining blockers. It does not disguise partial implementation as full parity.
