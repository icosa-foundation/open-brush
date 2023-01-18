Settings = {
    space="canvas"
}

Widgets = {
    freq={label="Frequency", type="float", min=0, max=50, default=40},
    amp={label="Amplitude", type="float", min=0, max=5, default=0.5},
}

function WhileTriggerPressed()

    posMix = (math.sin(app.time * freq) + 1) * amp
    rotMix = (math.sin(app.time * freq) + 1) * amp

    pos = {
        Mathf.LerpUnclamped(brush.position.x, wand.position.x, posMix),
        Mathf.LerpUnclamped(brush.position.y, wand.position.y, posMix),
        Mathf.LerpUnclamped(brush.position.z, wand.position.z, posMix),
    }

    rot = {
        Mathf.LerpUnclamped(brush.rotation.x, wand.rotation.x, rotMix),
        Mathf.LerpUnclamped(brush.rotation.y, wand.rotation.y, rotMix),
        Mathf.LerpUnclamped(brush.rotation.z, wand.rotation.z, rotMix),
    }

    return {pos, rot}

end
