Settings = {
    space="canvas"
}

Widgets = {
    delay={label="Delay", type="int", min=0, max=100, default=20},
    freq={label="Frequency", type="float", min=0, max=50, default=40},
    amp={label="Amplitude", type="float", min=0.001, max=5, default=0.5},
}

function WhileTriggerPressed()
    mix = (math.sin(app.time * freq) + 1) * amp
    pos = brush.position
    oldpos = brush.pastPosition(delay)
    pos = {
        Mathf.LerpUnclamped(oldpos.x, pos.x, mix),
        Mathf.LerpUnclamped(oldpos.y, pos.y, mix),
        Mathf.LerpUnclamped(oldpos.z, pos.z, mix),
    }
    rot = brush.rotation
    return {pos, rot}
end
