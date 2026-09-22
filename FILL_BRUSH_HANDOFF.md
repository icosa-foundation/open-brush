# Fill Brush — design, decisions, and state

Handoff notes for whoever picks this up. Branch `claude/3d-stroke-filling-1o9aha`,
10 commits off `main`.

**Read this first:** nothing here has ever run in Unity. See
[Verification](#verification-and-what-it-does-not-cover) before trusting any of it.

---

## What it is

A brush that fills the closed loop traced by a stroke — the 3D analogue of filling a
closed path in a 2D drawing app. Structurally the sibling of `HullBrush`: the stroke's
control points feed a whole-shape solver rather than a ribbon, so all geometry is rebuilt
whenever the control points change and lives on a single knot (`m_knots[1]`).

Where Hull Brush wraps the points in their convex hull, Fill Brush spans them with a
surface that follows the path's concavities.

## Files

| File | Lines | Role |
| --- | --- | --- |
| `Assets/Scripts/Mesh Processing/PathFill.cs` | 669 | Public API and the pipeline |
| `Assets/Scripts/Mesh Processing/PathFillGeometry.cs` | 910 | Geometry primitives, Unity-scene-free |
| `Assets/Scripts/Mesh Processing/PathFillTessellator.cs` | 169 | Bridge to the libtess in `com.unity.vectorgraphics` |
| `Assets/Scripts/Brushes/FillBrush.cs` | 333 | The brush |
| `Assets/Editor/Tests/TestPathFill.cs` | 1012 | Edit-mode tests |

Assets: five brushes under `Assets/Resources/X/Brushes/` (MatteFill, ShinyFill,
DiamondFill, UnlitFill, FlatFill), registered in `Manifest_Experimental.asset`.

Entry points are `PathFill.Fill(path, options)` and `PathFill.BuildMesh(...)`.

## The algorithm

One construction for every curve. No branches on shape type.

1. **Weld** coincident control points (`kWeldFraction = 1e-5` of the bounding diagonal —
   duplicates only; see [Negative results](#negative-results)).
2. **Simplify** the boundary with Ramer–Douglas–Peucker, in fixed index-bounded blocks of
   `kSimplifyBlockSize = 256` so a growing stroke only pays for its tail. If the result
   still exceeds `MaxBoundaryPoints`, a tolerance search runs *on the block result*, not on
   the raw path.
3. **Fit a plane** by PCA (Jacobi eigendecomposition of the covariance), oriented to agree
   with the Newell normal so it matches the loop's winding.
4. **Project** to 2D in the plane's basis, keeping each boundary point's signed
   out-of-plane residual. Normalize to `kTessellationExtent = 100` so the tessellator's
   absolute epsilons land in the range they were tuned for.
5. **Triangulate** under a fill rule (non-zero or even-odd) via libtess.
6. **Refine** by uniform 1→4 subdivision, but only while it still moves the surface
   (see [Refinement](#refinement-stops-when-it-stops-mattering)).
7. **Lift** every vertex off the plane by mean-value interpolation of the boundary
   residuals, clamped to the range the boundary itself spans.

The projection is a parameterization, not a flattening. Boundary vertices return to their
exact input positions because mean value coordinates interpolate the boundary; a saddle
gets a saddle-shaped fill; a planar loop gets exactly the 2D fill in its own plane.

### Why libtess rather than an ear clipper

Going through `com.unity.vectorgraphics` is what gives the fill genuine 2D fill semantics:
non-zero vs even-odd winding, self-intersecting outlines, and multiple contours forming
holes. Rolling our own would have meant reimplementing all of it.

### Refinement stops when it stops mattering

Subdivision exists to give the lift somewhere to curve. On a flat stroke — which is most
of them — every vertex it adds sits exactly where the coarse surface already was.

The error from *not* splitting an edge is how far the lift at its midpoint differs from the
average of its endpoints, and computing it costs one evaluation per vertex the pass would
have added. `Options.SurfaceTolerance` sets the bar. A flat stroke takes zero passes; a
saddle takes as many as it needs.

The lift itself is skipped outright when the stroke is flat to within that tolerance.
Evaluating mean value coordinates per vertex to multiply by zeros was the single most
expensive thing here, and it is quadratic — vertices × boundary points.

Measured at prefab settings, flat circle: **1089 verts / 1920 tris / 3.83 ms** before,
**32 / 30 / 0.24 ms** after, with the lift identically zero in both.

### Determinism is a hard constraint

Strokes persist as control points and their geometry is rebuilt on load
(`Stroke.Recreate` → `RecreateLineFromMemory`). A fill derived from stroke knots must be
reproducible from those knots alone.

Every stage is deterministic: fixed iteration counts, no dependence on hash ordering, no
time- or frame-dependent input. **Keep it that way.** In particular the simplification
cache memoizes, it does not decide — its result is a pure function of (path, tolerance,
block size), tested at every prefix of a growing stroke and warm-against-cold on the
finished one.

This is also why hysteresis is not available as a tool for smoothing any future mode
switch: it would make the result depend on draw history.

---

## Decisions worth knowing about

### One algorithm, no special cases

There was, for four commits, a second construction (`PathFillSpiral`) that surfaced spirals
between their turns — a quad strip between the path and itself one revolution later. It
turned the conical spiral tool script into a cone and the spherical one into a shell, and
it measured well: 0.0011–0.0314 deviation from the ideal shape on unit-scale geometry.

**It was removed at the owner's direction: "no special casing, never, one algorithm for all
curves."** The objection was that a threshold (1.3 revolutions) meant strokes either side of
it got fundamentally different treatment, the switch flipped mid-draw as a stroke grew past
it, and the seam was visible. It had also been added as a rescue for output that looked
bad, which is the shape of a special case however principled the trigger is made.

If you are tempted to reintroduce anything like it, **don't** without asking. The
reasoning that a closed curve bounds a region and an open one doesn't — so the two need
different constructions — was considered and rejected as insufficient justification.

Consequence, stated plainly: a spiral now gets a bounded fill on a best-fit plane that
means little, because a spiral does not bound a region. That is duller than the loft and it
is predictable, and predictable was the requirement.

### What keeps pathological input sane, without branching

Two unconditional mechanisms, applied to every fill:

- **Weight-spread fallback.** Mean value coordinates are only well-behaved on a simple
  polygon. Where the outline overlaps itself the weights take large values of both signs
  that still sum to one after normalizing, so the total gives no warning while the
  interpolation swings far outside the boundary's range. The *magnitudes* are the tell:
  they sum to one only while the weights form a convex combination, and that sum is exactly
  the factor by which the interpolation can overshoot. Measured across an interior grid:
  1.0 convex, 2.3 concave, 8.8 figure-eight, 266 and 535 for the tool-script spirals. Past
  `kMaxWeightSpread = 4.0` it falls back to inverse-distance weighting, whose weights are
  all positive and therefore a genuine convex combination.
- **Range clamp.** The lift is clamped to the range the boundary itself spans. A harmonic
  interpolant attains its extremes on the boundary and that is the surface being
  approximated, so this enforces a property the exact answer already has.

Together these took the worst overshoot on the spiral paths from **138.7 to zero**.

### Tolerance is anchored to the brush, not the stroke

The simplification tolerance used to be a fraction of the stroke's bounding box, which
grows while drawing — so the part already drawn kept being re-simplified more coarsely and
its outline shifted under the user. Measured on a circle, points kept from the first
hundred fell from 11 to 6 between a quarter drawn and finished.

`FillBrush` now sets `Options.SimplifyToleranceAbsolute` from its own brush size, which is
fixed for the stroke. Detail finer than the brush was never visible anyway. **Note the
prefab value changed meaning** — from a fraction of the stroke to a multiple of the brush
size, hence `0.002` becoming `0.25`.

This is also what made the simplification cache sound.

### 16-bit geometry counts

`Knot.nVert` and `Knot.nTri` are `ushort` (`GeometryBrush.cs:87`). Overflowing them does not
throw; it wraps, leaving the knot describing a range unrelated to the geometry in the pool,
which draws as stray triangles stitched between unrelated vertices and persists on the
finished stroke. This was a real reported bug.

`MaxVertices` is therefore a hard cap, not a refinement budget: the boundary is coarsened
and retried up to `kMaxCoarseningAttempts = 3` times, and the fill is refused rather than
overrunning. `FillBrush.CreateGeometry` checks the 16-bit limits itself rather than trusting
a prefab-configurable cap.

`FillBrush` also clamps its vertex budget against `GeometryBrush`'s soft vertex limit
(9000) and `NS`. Overrunning that limit ends the stroke and starts a new one — invisible for
a ribbon, but for a fill it means half a shape with a second fill on top. Faceting plus
double-sided geometry makes this easy to trip.

### The tessellator hands back garbage

LibTess pads polygon slots it did not fill with `Undef = -1` (`Tess.cs:546`), and
`VectorSceneTessellation.cs:301` casts indices to `UInt16` unchecked — so that padding
arrives as index **65535**. It also runs LibTess with `NoEmptyPolygons` off, so degenerate
faces come through. Unfiltered, the bad index throws `ArgumentOutOfRangeException` during
refinement and the whole fill disappears.

Both are dropped, in the bridge and again in `PathFill` so a custom tessellator cannot get
past it. Non-finite vertices are dropped back onto the plane.

---

## Negative results

Things that look like obvious wins and are not. Please don't re-try these without new
evidence.

**Welding control points closer than a fraction of the simplification tolerance.** The
pointer spawns a knot every couple of millimetres, so a large stroke carries far more points
than its shape needs, and dropping them in one linear pass looks free. It is not: welding
snaps the path onto a coarser chain, and those small deviations are exactly what stops RDP
collapsing a smooth curve afterwards. On a 1570-point half-metre loop, welding at half the
tolerance took the boundary from **33 points to 123** and the rebuild from **9 ms to 55 ms**.
The weld stays at duplicates only. This is recorded in a comment on `kWeldFraction`.

**Blaming mean value coordinates for the reported spikes.** They were the obvious suspect
and were innocent — worst overshoot beyond the boundary range was 0.004 on a self-crossing
loop and 0.002 straddling a concave notch. The actual causes were the 16-bit overflow and,
separately, the weight-spread problem on *many-turn* outlines (a single crossing is nowhere
near enough cancellation to trigger it).

**Deciding "is this a spiral" on net swept area.** It changes with how finely the path is
sampled — it rejected a 3-turn sphere spiral while accepting a 10-turn one. (Moot now that
the spiral path is gone, but the general lesson stands: normalize shape criteria so they do
not move with sampling density.)

---

## Tuned constants, and where the numbers came from

| Constant | Value | Basis |
| --- | --- | --- |
| `PathFill.kTessellationExtent` | 100 | Puts coordinates in the range libtess's absolute epsilons expect |
| `PathFill.kWeldFraction` | 1e-5 | Duplicates only — see negative results |
| `PathFill.kMaxCoarseningAttempts` | 3 | Arbitrary; bounded retry |
| `PathFill.kSimplifyBlockSize` | 256 | Larger blocks simplify slightly better, re-simplify more when the tail changes |
| `PathFillGeometry.kMaxWeightSpread` | 4.0 | Measured separation: 1.0 convex / 2.3 concave / 8.8 figure-eight / 266–535 spirals |
| `Options.SurfaceTolerance` | 0.002 | Matches the boundary simplification tolerance — no point holding the interior to a standard the outline is not held to |
| Prefab `m_SimplifyTolerance` | 0.25 | Multiple of brush size |
| Prefab `m_MaxVertices` | 3000 (Matte) / 3600 (Flat) | Under the 9000 soft limit after double-siding |

---

## Performance

Rebuild cost, strokes sampled at 2 mm as the pointer spawns them. These are from the
offline harness with an ear-clipping stand-in for libtess, so **absolute times are
indicative, not Unity-accurate** — the relative wins and the vertex counts are real.

| Stroke | Originally | Now |
| --- | --- | --- |
| 157 points | 2.28 ms | 0.18 ms |
| 628 points | 11.20 ms | 0.32 ms |
| 1570 points, flat | 53.49 ms | 8.02 ms |
| 1570 points, wobbly | ~60 ms | ~53 ms |

Simplification alone on a 1570-point stroke went 11.363 ms → 0.021 ms with the cache.

The wobbly case is the remaining cost. It genuinely needs a dense outline (115 boundary
points) and two refinement passes, so the time is in the lift — O(vertices × boundary) —
not in decimation. **If that case matters, the lever is the boundary cap, not more caching.**

---

## Verification, and what it does not cover

**Nothing has run in Unity.** There is no .NET toolchain in the environment this was built
in, so the edit-mode tests in `TestPathFill.cs` have never executed.

What was done instead: Mono was installed and two offline harnesses built in a scratchpad.
One stubs `UnityEngine` and swaps in an ear clipper for the tessellator, then runs the full
pipeline — **68 assertions, all passing**. The other stubs the VectorGraphics and brush APIs
with signatures copied from the real sources, and type-checks the bridge, the brush and the
test file. Every threshold asserted in the Unity tests was verified in the harness first
rather than guessed.

**The harness lived in an ephemeral scratchpad and is gone.** If you want it back it is
worth rebuilding: stubs for `Vector2`/`Vector3`/`Mathf`/`Color32`/`Debug`, an ear-clipping
`PathFillTessellator`, and a `Main` that exercises `PathFill.Fill`. It paid for itself many
times over. Consider committing it under `Assets/Editor/Tests/` or a tools folder this time.

Not covered by any of it:
- The real libtess. Self-overlapping **planar** fills are the one path that could not be
  exercised offline, because the ear-clipping stand-in cannot triangulate them.
- Anything about how this looks. No stroke has been drawn with it by this author.
- Hand-drawn input. Every path tested was generated analytically; real VR strokes wobble
  far more than script output.

---

## Open work

**Needs eyes in the app first.** The last few commits changed behaviour materially — the
spiral construction was removed, and the tolerance changed meaning from a fraction of the
stroke to a multiple of the brush size. Both want looking at before anything else is built
on top.

Then, roughly in order:

1. **Run the edit-mode tests.** They have never executed. Expect some tolerance fiddling.
2. **The Flat variant is coarse** (~1200 triangles vs ~5900 smooth), because faceting makes
   vertices equal the index count and the budget has to absorb that. If it reads as too
   chunky, the honest fix is a single-sided descriptor, which doubles the available budget.
3. **Shared materials.** All five variants borrow their Hull siblings' materials, because
   brush `.mat` files are not in this repository (Matte Hull's own material GUID is dangling
   in a fresh checkout too — they resolve from elsewhere). A dedicated material per variant
   is outstanding.
4. **Procedural button icons.** Generated with a Python PNG encoder, 128×128 greyscale on
   black to match the others. Fine as placeholders, not drawn by an artist.
5. **No localisation entries.** Names fall back to durable names via
   `BrushDescriptor.Description`'s catch. Eight shipping brushes already do this, so it
   works, but proper entries are missing.
6. **Anomaly reporting.** `Result.ProjectedSelfIntersections`, `ClampedVertices`,
   `DroppedTriangles` and `RepairedVertices` exist and `FillBrush` warns when they change,
   gated on `m_LogAnomalies`. This is reporting, not branching. The owner has queried
   whether the brush should stay silent about inputs it cannot do much with — unresolved.
7. **Boundary cap tuning** for the expensive wobbly-stroke case, per Performance above.

### Hard constraints — do not break these

- **Determinism.** Fixed iteration counts, no hash-order dependence, no history dependence.
  Strokes rebuild from control points on load and must come back identical.
- **One algorithm for all curves.** No branching on shape type. This is a direct
  instruction from the repository owner, not a preference.
- **16-bit geometry counts.** Anything that raises vertex or triangle counts must respect
  `ushort` limits and `GeometryBrush`'s soft vertex limit.
- `AGENTS.md` forbids committing temporary design or planning documents without explicit
  instruction. This file is committed because it was asked for, as a handoff. Don't take it
  as licence to add more: keep working notes untracked.
