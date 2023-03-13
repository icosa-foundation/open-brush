Settings = {
    description="The brush oscillates between your left and right hand positions",
    space="canvas"
}


Parameters = {
    freq={label="Frequency", type="float", min=0, max=50, default=40},
    amp={label="Amplitude", type="float", min=0, max=5, default=0.5},
}

function WhileTriggerPressed()

    mix = (math.sin(app.time * freq) + 1) * amp

    position = {
        unityMathf.lerpUnclamped(brush.position.x, wand.position.x, mix),
        unityMathf.lerpUnclamped(brush.position.y, wand.position.y, mix),
        unityMathf.lerpUnclamped(brush.position.z, wand.position.z, mix),
    }

    rotation = {
        unityMathf.lerpUnclamped(brush.rotation.x, wand.rotation.x, mix),
        unityMathf.lerpUnclamped(brush.rotation.y, wand.rotation.y, mix),
        unityMathf.lerpUnclamped(brush.rotation.z, wand.rotation.z, mix),
    }

    return {position, rotation}

end
