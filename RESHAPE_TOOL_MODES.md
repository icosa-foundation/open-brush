# Reshape Tool Modes

The Reshape tool edits the control points of existing brush strokes. Hold the activation trigger while the tool overlaps a stroke to apply the selected mode. Trigger pressure controls the strength where analog pressure is available.

Most modes use a smooth spatial falloff: control points near the centre of the tool receive the strongest effect, while points near its boundary receive little or no effect. Influence is also feathered a short distance along the stroke so that edits blend into neighboring control points instead of ending abruptly.

## Mode summary

| Mode shown in the UI | What it does | Alternate-mode toggle |
| --- | --- | --- |
| Attract / Repel | Moves control points radially toward or away from the tool centre. | Yes: switches between Repel and Attract. |
| Bundle / Spread | Moves control points toward or away from the tool's oriented centre line. | Yes: switches between Bundle and Spread. |
| Project to Plane | Draws control points toward the tool's oriented plane. | No. |
| Grab | Translates the affected stroke region and twists it with controller roll. | No. |
| Smooth | Relaxes interior control points toward the path between their neighbors. | No. |

## Attract / Repel

This mode moves affected control points along the line between each point and the centre of the tool.

Repel moves points outward from the tool centre. Attract moves points inward toward it. Attract is clamped so that a point cannot cross through the centre in one update.

The effect is continuous while the trigger is held. Its strength scales with the tool radius, trigger pressure, and radial falloff.

Alternate-mode toggle: **Yes.** The toggle switches between Repel and Attract. A newly instantiated tool begins in Repel state.

## Bundle / Spread

This mode uses the oriented line shown by the tool rather than treating the tool as only a sphere.

Bundle moves control points perpendicularly toward that line, gathering nearby parts of a stroke into a tighter bundle. Spread reverses the direction and moves them away from the line. Motion is proportional to distance from the line, so Bundle naturally slows as points converge on it.

Influence falls off with perpendicular distance from the line, while the subtool's finite box limits how far along the line it can affect strokes.

Alternate-mode toggle: **Yes.** The toggle switches between Bundle and Spread.

## Project to Plane

This mode draws affected control points perpendicularly toward the oriented plane shown by the tool. Repeated updates progressively flatten the stroke region onto that plane rather than snapping it there immediately.

Influence depends on lateral distance across the plane, not height above or below it. This lets points at different heights converge consistently as long as they are inside the plane's usable footprint.

Alternate-mode toggle: **No.** The mode always projects toward the plane.

## Grab

When the trigger first contacts a stroke, this mode captures the affected control points and their influence weights. Moving the controller translates those points from their captured positions.

Rolling the controller also twists the affected region around the controller's forward axis as it was oriented when the trigger was pressed. Translation and rotation both fade toward the edge of the influenced area, and control-point orientations rotate with the stroke.

Because every update is calculated from the captured starting state, the result does not accumulate frame-by-frame drift. Moving without rolling performs a plain translation; rolling without moving performs a twist.

Alternate-mode toggle: **No.** Translation and twist direction come directly from controller movement and roll.

## Smooth

This mode reduces local bends and irregularities in a stroke. Each affected interior control point moves toward a length-weighted position between its previous and next neighbors. Using segment lengths keeps the result sensible when control points are unevenly spaced.

Stroke endpoints remain fixed. The effect is applied continuously and is scaled by trigger pressure and spatial influence, allowing gradual smoothing with repeated passes.

Alternate-mode toggle: **No.** There is currently no sharpen or roughen counterpart.

## Alternate-mode state

Selecting a different mode resets the alternate-mode state. Attract / Repel starts in Repel, while Bundle / Spread starts in Bundle. This prevents a mode from inheriting the alternate state selected in another mode.

Reselecting the already-active mode does not reset its current direction.

Project to Plane, Grab, and Smooth ignore the alternate-mode command, and the controller toggle indicator is hidden for those modes.
