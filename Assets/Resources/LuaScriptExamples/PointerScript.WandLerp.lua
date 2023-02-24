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
        unityMathf.lerpUnclamped(brush.position.x, wand.position.x, posMix),
        unityMathf.lerpUnclamped(brush.position.y, wand.position.y, posMix),
        unityMathf.lerpUnclamped(brush.position.z, wand.position.z, posMix),
    }

    rot = {
        unityMathf.lerpUnclamped(brush.rotation.x, wand.rotation.x, rotMix),
        unityMathf.lerpUnclamped(brush.rotation.y, wand.rotation.y, rotMix),
        unityMathf.lerpUnclamped(brush.rotation.z, wand.rotation.z, rotMix),
    }

    return {pos, rot}

end
