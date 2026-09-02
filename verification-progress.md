# Open Brush HTTP Command Verification Progress

Session started: 2025-01-14

## Scope rules
- Allowed: commands that affect the running scene or create new files on disk.
- Excluded: commands that modify or delete existing saved files.
- Note: network-dependent commands may be attempted and logged as WARN/FAIL if blocked.

## Excluded commands (per scope)
- save.overwrite
- save.as (risk of overwriting existing named sketch)

## Results legend
- OK: Command executed without errors and observed expected effect or log
- WARN: Command executed but verification incomplete/ambiguous
- FAIL: Command returned error or Unity logged errors
- SKIP: Excluded per scope

## Progress
| Command | Params used | Verification | Result | Notes | Timestamp |
| --- | --- | --- | --- | --- | --- |
| debug.brush | (none) | Unity console logs brush position/rotation | OK | Status 200 | 2026-01-08 16:46:56 |
| strokes.debug | (none) | Unity console: Strokes count | OK | Strokes: 0 | 2026-01-08 16:48:26 |
| brush.home.reset | (none) | debug.brush after reset | OK | Brush pos (0.00, 13.00, 3.00) rot (0,0,0) | 2026-01-08 16:48:58 |
| brush.look.forwards | (none) | debug.brush after command | OK | Brush rot (0.00, 0.00, 0.00) | 2026-01-08 16:49:23 |
| brush.turn.y | 45 | debug.brush after command | OK | Brush rot (0.00, 45.00, 0.00) | 2026-01-08 16:49:49 |
| brush.move | 1 | debug.brush after command | OK | Brush pos (0.71, 13.00, 3.71) | 2026-01-08 16:50:15 |
| brush.draw | 1 | strokes.debug after draw | OK | Strokes: 1 | 2026-01-08 16:50:43 |
| brush.new.stroke | (none) | strokes.debug after new stroke + draw | OK | Strokes: 2 | 2026-01-08 16:51:19 |
| brush.turn.x | 30 | debug.brush after command | OK | Brush rot (330.00, 45.00, 0.00) | 2026-01-08 16:51:45 |
| brush.turn.z | 15 | debug.brush after command | OK | Brush rot (330.00, 45.00, 15.00) | 2026-01-08 16:52:12 |
| brush.look.up | (none) | debug.brush after command | OK | Brush rot (270.00, 0.00, 0.00) | 2026-01-08 16:53:32 |
| brush.look.down | (none) | debug.brush after command | OK | Brush rot (90.00, 0.00, 0.00) | 2026-01-08 16:54:02 |
| brush.look.left | (none) | debug.brush after command | OK | Brush rot (0.00, 270.00, 0.00) | 2026-01-08 16:54:29 |
| brush.look.right | (none) | debug.brush after command | OK | Brush rot (0.00, 90.00, 0.00) | 2026-01-08 16:54:57 |
| brush.look.backwards | (none) | debug.brush after command | OK | Brush rot (0.00, 180.00, 0.00) | 2026-01-08 16:55:26 |
| brush.look.at | 0,0,0 | debug.brush after command | WARN | Console: 'Look rotation viewing vector is zero' | 2026-01-08 16:55:52 |
| brush.move.to | 1,2,3 | debug.brush after command | OK | Brush pos (1.00, 2.00, 3.00) | 2026-01-08 16:56:20 |
| brush.move.by | 1,0,-1 | debug.brush after command | OK | Brush pos (2.00, 2.00, 2.00) | 2026-01-08 16:56:47 |
| user.move.to | 1,1,1 | SceneParent transform change | OK | SceneParent pos (-1.00, -1.00, -1.00) | 2026-01-08 16:58:01 |
| user.move.by | 1,0,0 | SceneParent transform change | OK | SceneParent pos (-2.00, -1.00, -1.00) | 2026-01-08 16:58:16 |
| user.turn.y | 45 | SceneParent rotation change | OK | SceneParent rot (0.00, 315.00, 0.00) | 2026-01-08 16:58:33 |
| user.turn.x | 15 | SceneParent rotation check | SKIP | Monoscopic-only; defer to separate run | 2026-01-08 16:58:50 |
| user.turn.z | 10 | SceneParent rotation check | SKIP | Monoscopic-only; defer to separate run | 2026-01-08 16:59:05 |
| user.direction | 45,45,0 | SceneParent rotation change | OK | SceneParent rot (0.00, 45.00, 0.00) | 2026-01-08 16:59:21 |
| user.look.at | 1,2,3 | SceneParent rotation change | OK | SceneParent rot (0.00, 1.97, 0.00) | 2026-01-08 16:59:37 |
| spectator.move.to | 5,5,5 | Checked NoPeekingCamera & main camera transforms | WARN | No observable transform change | 2026-01-08 17:00:24 |
| spectator.move.by | 1,0,0 | Checked NoPeekingCamera & main camera transforms | WARN | No observable transform change | 2026-01-08 17:00:41 |
| spectator.turn.y | 30 | Checked NoPeekingCamera rotation | WARN | No observable rotation change | 2026-01-08 17:00:58 |
| brush.move.to.hand | r | debug.brush after command | OK | Brush pos (-5.57, 20.97, 9.50) | 2026-01-08 17:01:33 |
| brush.look.at | 1,1,1 | debug.brush after command | OK | Brush rot (324.74, 45.00, 0.00) | 2026-01-08 17:02:14 |
| scene.scale.to | 0.5 | SceneParent scale | OK | SceneParent scale (0.5,0.5,0.5) | 2026-01-08 17:02:34 |
| scene.scale.by | 2 | SceneParent scale | OK | SceneParent scale (1.0,1.0,1.0) | 2026-01-08 17:02:51 |
| spectator.move.to | 5,5,5 | DropCam transform | OK | DropCam pos (5.00, 5.00, 5.00) | 2026-01-08 17:09:26 |
| spectator.move.by | 1,0,-2 | DropCam transform | OK | DropCam pos (6.00, 5.00, 3.00) | 2026-01-08 17:09:41 |
| spectator.turn.y | 30 | DropCam rotation | OK | DropCam rot (11.84, 242.23, 21.29) | 2026-01-08 17:09:55 |
| spectator.turn.x | 15 | DropCam rotation | OK | DropCam rot (357.83, 236.84, 20.83) | 2026-01-08 17:10:10 |
| spectator.turn.z | 10 | DropCam rotation | OK | DropCam rot (357.83, 236.84, 30.83) | 2026-01-08 17:10:27 |
| spectator.direction | 45,45,0 | DropCam rotation | OK | DropCam rot (45.00, 45.00, 0.00) | 2026-01-08 17:10:44 |
| spectator.look.at | 0,0,0 | DropCam rotation | OK | DropCam rot (36.70, 243.43, 0.00) | 2026-01-08 17:11:06 |
| spectator.off | (none) | DropCam active state | OK | active=false | 2026-01-08 17:14:53 |
| spectator.on | (none) | DropCam active state | OK | active=true | 2026-01-08 17:15:09 |
| spectator.toggle | (none) | DropCam active state | OK | active=false | 2026-01-08 17:15:28 |
| spectator.toggle | (none) | DropCam active state | OK | active=true | 2026-01-08 17:15:47 |
| spectator.hide | panels | DropCamera cullingMask | OK | cullingMask -2049 -> -67585 (Panels layer bit cleared) | 2026-01-08 17:16:32 |
| spectator.mode | stationary | DropCam transform over 2s | OK | No change in position/rotation | 2026-01-08 17:27:03 |
| spectator.mode | wobble | DropCam transform over 2s | OK | Position changed (wobble) | 2026-01-08 17:27:31 |
| spectator.mode | circular | DropCam transform over 2s | OK | Position/rotation changed (circular) | 2026-01-08 17:28:00 |
| spectator.mode | slowFollow | DropCam transform over 2s after user move | WARN | DropCam pos stayed (0,0,0) - unexpected | 2026-01-08 17:28:36 |
| spectator.mode | slowFollow (retry) | DropCam vs XRRig after user.move.by | WARN | user.move.by did not move XRRig; DropCam unchanged | 2026-01-08 17:32:45 |
| spectator.mode | slowFollow (retry 2) | Move XRRig then sample DropCam | OK | DropCam moved to match XRRig after delay | 2026-01-08 17:33:57 |
| brush.look.at | 0,0,0 then 1,1,1 | Console + debug.brush | FAIL | Bug: uses direction vector, not (target - brushPosition); zero-vector error | 2026-01-08 17:35:39 |
| spectator.hide | widgets | DropCamera cullingMask | OK | -2049 -> -264193 (GrabWidgets cleared) | 2026-01-08 18:02:44 |
| spectator.hide | strokes | DropCamera cullingMask | OK | -264193 -> -1312769 (MainCanvas cleared) | 2026-01-08 18:02:44 |
| spectator.hide | selection | DropCamera cullingMask | OK | -1312769 -> -3409921 (SelectionCanvas cleared) | 2026-01-08 18:02:44 |
| spectator.hide | headset | DropCamera cullingMask | OK | -3409921 -> -3442689 (HeadMesh cleared) | 2026-01-08 18:02:44 |
| spectator.hide | ui | DropCamera cullingMask | OK | -3442689 -> -3442721 (UI cleared) | 2026-01-08 18:02:44 |
| spectator.hide | usertools | DropCamera cullingMask | OK | -3442721 -> -3459105 (UserTools cleared) | 2026-01-08 18:02:44 |
| brush.transform.push | (none) | debug.brush after move+pop | OK | Restored brush pos to (-5.57, 20.97, 9.50) | 2026-01-08 18:04:32 |
| brush.transform.pop | (none) | debug.brush after pop | OK | Brush pos restored to (-5.57, 20.97, 9.50) | 2026-01-08 18:04:37 |
| brush.home.set | (none) | home reset to set position | OK | Set home at (2,2,2) | 2026-01-08 18:05:37 |
| brush.home.reset | (none) | debug.brush after reset | OK | Brush pos (2.00, 2.00, 2.00) | 2026-01-08 18:05:37 |
| draw.path | [0,0,0],[1,0,0],[1,1,0],[0,1,0] | strokes.debug | OK | Strokes: 2 -> 3 | 2026-01-08 18:06:58 |
| draw.stroke | [0,0,0,0,180,90,.75]... | strokes.debug | OK | Strokes: 3 -> 4 | 2026-01-08 18:07:35 |
| draw.polygon | 5,2.5,45 | strokes.debug | OK | Strokes: 4 -> 5 | 2026-01-08 18:08:10 |
| draw.text | hello | strokes.debug | OK | Strokes: 5 -> 13 | 2026-01-08 18:08:43 |
| draw.paths | [[0,0,0],[1,0,0],[1,1,0]],[[0,0,-1],[-1,0,-1],[-1,1,-1]] | strokes.debug | OK | Strokes: 13 -> 15 | 2026-01-08 18:09:21 |
| draw.svg.path | M 0 0 L 10 0 L 10 10 Z | strokes.debug | OK | Strokes: 15 -> 16 | 2026-01-08 18:09:56 |
| draw.svg | <svg><path d='M 0 0 L 10 0 L 10 10 Z'/></svg> | strokes.debug | OK | Strokes: 16 -> 17 | 2026-01-08 18:10:40 |
