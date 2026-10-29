# Color jitter scripts

A `JitterScript.Name.lua` plugin returns a color whenever Open Brush requests a
jittered color. This callback is invocation-based, **not a per-frame update or a
guaranteed once-per-stroke callback**.

## Try the bundled example

1. While Open Brush is running, send
   `GET /api/v1?scripts.jitterscript.activate=ColorJitter`.
2. Enable the normal drawing-jitter toggle, then draw. The bundled
   `JitterScript.ColorJitter.lua` alternates warm and cool tints, blending each
   with the incoming color. It also demonstrates the optional `Start()` hook.
3. Send `GET /api/v1?scripts.jitterscript.deactivate` to return to ordinary HSV
   jitter. Activation uses the script name after the `JitterScript.` prefix and
   before `.lua`; an unknown name is rejected.

Custom scripts belong in the Open Brush `Plugins` folder and must retain the
`JitterScript.` filename prefix. There is currently no JitterScript panel button.

## Callback contract

1. Define `Main()` and return a documented `Color`, such as a value from
   `Color:New(r, g, b)` or `Color:Lerp(a, b, t)`. A Transform or Path is not a
   jitter-color result.
2. Read `_currentColor` inside `Main()`. It is the caller's input color before
   jitter. It is assigned after initialization, so `Start()` must not depend on
   that global being available.
3. `Start()` runs through normal script initialization; deactivation ends the
   active script through the normal `End()` hook if one is defined. The example
   resets its alternating state in `Start()`.
4. Returning `nil`, having no callable `Main`, or failing result conversion keeps
   the input color. Interpreter/conversion errors handled by the callback are
   logged. These paths do not apply random HSV jitter afterward.

An enabled jitter script replaces the built-in HSV-randomization step; its output
is not added to the hue/saturation/value sliders. The existing brush
minimum-luminance clamp still runs after the script result. Disabling the script
restores the normal HSV path and its configured ranges.

This category currently changes color only. Keep callbacks small because other
drawing operations may request jittered colors too; it does not define a
nonfinite/HDR output policy or position, size and pressure jitter.
