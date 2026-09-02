# Stroke Sculpting UX Plan

## Goal

Replace the mesh-vertex assumptions in Push/Pull with interactions designed for ordered brush-stroke control points. Sculpting should feel predictable across brushes, control-point densities, frame rates, and canvas scales while preserving curve continuity and brush orientation.

## Design principles

1. Treat a stroke as an ordered curve, not a collection of independent vertices.
2. Make the visible tool volume match the mathematical influence volume.
3. Use smooth, normalized influence weights that reach zero at selection boundaries.
4. Use controller motion for transform-like operations and elapsed time for continuous operations.
5. Preserve or transport control-point orientation whenever positions are transformed.
6. Keep one trigger gesture in one undo group, including edits to multiple strokes.
7. Keep GPU and CPU intersection time slicing intact.
8. Make radius, strength, direction, sound, and haptics mean the same thing across modes where possible.

## Intended mode set

1. **Grab**: Softly translate and optionally rotate a region using controller motion. This becomes the default general reshaping mode.
2. **Attract/Repel**: Continuously draw points toward or push them away from the tool centre. This is the revised Push/Pull mode.
3. **Smooth**: Relax local curvature using neighboring control points and arc-length-aware weights.
4. **Project to Plane**: Gradually project a region onto a visible controller-aligned plane. This is the revised Flatten mode.
5. **Pinch/Bundle**: Draw multiple strokes toward a shared controller-aligned line, or spread them away from it. This replaces Crease.

## Phase 1: Shared stroke influence and feedback

1. Introduce a common normalized radial influence function based on `distance / radius`.
2. Use a smooth falloff that is one at the centre and zero at the boundary, with no discontinuity as points enter or leave the tool.
3. Multiply continuous sculpt strength by brush trigger pressure.
4. Express continuous speed relative to tool radius so changing radius does not make small tools disproportionately strong.
5. Feather influence along the ordered stroke by smoothing displacement weights, including a small number of neighboring points outside the spatial selection.
6. Keep selection tests and deformation calculations in canvas space with explicit canvas-scale conversion.
7. Aggregate haptics to at most one pulse per sculpt update rather than one pulse per control point.
8. Rate-limit or sustain modification audio so intersecting several strokes does not chatter.

Acceptance criteria:

1. Influence approaches zero continuously at the visible boundary.
2. A stroke crossing the boundary does not acquire an obvious kink after one update.
3. Trigger pressure changes effect strength without changing selection radius.
4. Comparable gestures produce comparable world-space results at different canvas scales.
5. Dense and sparse strokes deform in the same general shape.
6. A frame that edits many control points produces one haptic event.

## Phase 2: Attract/Repel

1. Rename the user-facing Push/Pull operation to Attract/Repel.
2. Replace the asymmetric constant-push and distance-squared-pull formulas with the shared falloff.
3. Make attraction approach the centre without overshooting or crossing it.
4. Stabilize the direction at or extremely near the tool centre.
5. Apply the curve-feathered displacement rather than modifying isolated points independently.

Acceptance criteria:

1. Attract and Repel have perceptually matched strength.
2. Neither direction produces a hard shell at the tool radius.
3. Attract cannot throw a point through the centre in one update.
4. Holding the tool stationary gives a smooth, pressure-controlled continuous deformation.

## Phase 3: Grab

1. Capture the tool pose, affected control points, and influence weights when the trigger is pressed.
2. Apply controller translation relative to the captured pose instead of integrating velocity each frame.
3. Apply controller roll about the captured tool pivot and initial forward axis.
4. Apply the same rigid rotation to each affected control point's orientation.
5. Preserve the captured selection and weights for the gesture so points do not pop in and out while moving.
6. Reuse the existing one-gesture undo grouping.

Acceptance criteria:

1. Holding the controller still produces no movement.
2. Returning the controller to its captured pose returns the previewed points to their starting poses.
3. Movement is independent of frame and intersection scheduling frequency.
4. Orientation-sensitive brushes retain their expected facing through rotations.

## Phase 4: Smooth

1. Compute a length-weighted neighbor target for each selected interior control point.
2. Relax toward that target using trigger pressure, elapsed time, and the shared influence.
3. Preserve stroke endpoints unless an explicit endpoint-editing option is introduced.
4. Avoid changing control-point count in the first implementation.
5. Evaluate curvature or displacement smoothing if simple positional relaxation shrinks strokes excessively.

Acceptance criteria:

1. Repeated application removes sharp local kinks without collapsing the entire stroke.
2. Endpoints remain fixed.
3. Unevenly spaced control points do not bias the curve toward the denser side.

## Phase 5: Project to Plane

1. Replace `Collider.ClosestPoint` and the fixed dead zone with an explicit plane equation.
2. Use signed perpendicular distance to move points toward the plane.
3. Limit the operation with a visible disk or rectangle whose boundary matches the shared falloff.
4. Make projection gradual and pressure-controlled while held.
5. Consider a separate hard-project gesture only after the gradual interaction is validated.
6. Transport orientation from the original tangent to the deformed tangent while preserving roll.

Acceptance criteria:

1. The visual plane matches the resulting plane.
2. Points on either side converge without a thickness-dependent dead zone.
3. The flattened region joins the untouched stroke without a sharp boundary kink.

## Phase 6: Pinch/Bundle

1. Replace the per-stroke batch-bounds target with one controller-aligned line shared by all intersected strokes.
2. Move each point toward its closest projection on that line.
3. Use the reverse direction to spread points away from the line.
4. Base influence on distance from the line and the visible finite tool extent.
5. Replace the world-axis-aligned collider-bounds reach test with an oriented local-space test.

Acceptance criteria:

1. Several strokes in one gesture converge on the same line.
2. Rotating the controller rotates both the displayed and effective pinch region.
3. Reverse mode spreads the same region with matched strength.

## Phase 7: Captured Grab Rotation

1. Delete the tangential-displacement implementation.
2. Capture a pivot and controller-aligned axis at trigger press.
3. Derive angle from controller roll or another direct user-controlled rotation input.
4. Apply an exact quaternion rotation to positions, weighted by the shared soft selection.
5. Apply the corresponding quaternion to control-point orientations.
6. Keep the shipped name Grab while allowing controller roll to twist the captured region.

Acceptance criteria:

1. Full-weight points preserve their radius from the rotation axis.
2. Holding the controller still produces no automatic spiraling.
3. Reversing controller rotation reverses the edit without needing a direction toggle.
4. Ribbon and tube brushes rotate without stale-orientation artifacts.

## Phase 8: Visuals, naming, and input

1. Make Grab the initial active mode once it is stable.
2. Replace inherited mesh-sculpting names in buttons, prompts, tooltips, and controller hints.
3. Show a distinct and accurate interaction volume for every mode.
4. Show inner strength and outer falloff regions where that distinction is useful.
5. Only expose a direction toggle for modes with an actual signed operation.
6. Validate mode switching, including resetting signed modes to their primary direction, trigger cancellation, tool hiding, undo, redo, and multiplayer-disabled behavior.

## Orientation handling

1. Rigid transformations must multiply `m_Orient` by the same rotation applied to `m_Pos`.
2. Non-rigid deformation should derive old and new curve tangents and parallel-transport the original orientation between them.
3. Preserve the original roll around the curve tangent wherever possible.
4. Test at least one flat ribbon brush, one tube brush, one particle brush, and one hull-based brush before enabling orientation transport globally.

## Validation

1. Add edit-mode tests for falloff endpoints, symmetry, centre stability, plane projection, line projection, and radius-preserving rotation.
2. Add tests using both dense and sparse control-point arrays.
3. Add tests for unchanged endpoints and orientation transformation.
4. Run `git diff --check` and the available focused C# tests after every implementation commit.
5. Use one persistent visible Unity Editor instance for interactive validation when static tests are insufficient.
6. Validate each mode with a single stroke, several crossing strokes, a large canvas scale, a small canvas scale, and a deliberately stalled frame.
7. Inspect current Unity Editor logs after compilation and compare relevant error timestamps with the current clock.

## Proposed commit sequence

1. `Document stroke sculpting UX redesign`
2. `Add shared stroke sculpt influence weights`
3. `Use pressure and aggregate sculpt feedback`
4. `Rework Push Pull as symmetric Attract Repel`
5. `Add soft Grab stroke sculpt mode`
6. `Add stroke Smooth sculpt mode`
7. `Project sculpted strokes onto an explicit plane`
8. `Replace Crease with controller-axis Pinch`
9. `Replace tangential Rotate with captured Grab twist`
10. `Transport sculpted control point orientations`
11. `Update sculpt mode names visuals and hints`

Each implementation commit should leave undo grouping and intersection time slicing intact and should include its focused tests where practical.

## Status

Implementation is complete through the proposed commit sequence, with these additional corrections:

1. The tool ghost shader now supports single-pass stereo rendering.
2. Bundle / Spread influence is measured from its controller-aligned line.
3. Project to Plane influence is measured within its visible plane.
4. Switching modes cancels a held sculpt gesture and waits for trigger release.

The user-facing tool name is **Reshape**, avoiding a naming conflict with a separate Sculpt feature.

The remaining work is interactive validation rather than another implementation phase:

1. Confirm the tool ghost renders in both headset eyes.
2. Check every mode's visible volume against its effective influence boundary.
3. Exercise undo, redo, mode switching, trigger cancellation, and tool hiding in normal use.
4. Check orientation behavior with ribbon, tube, particle, and hull brushes.
5. Compare dense and sparse strokes at small and large canvas scales.
6. Confirm stalled frames and delayed GPU intersections do not cause jumps or strength loss.
7. Confirm the tool remains unavailable where multiplayer does not support it.
