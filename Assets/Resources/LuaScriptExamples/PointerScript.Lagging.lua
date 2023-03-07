Settings = {
    space="canvas"
}

Widgets = {
    delay={label="Delay", type="int", min=0, max=100, default=20},
    frequency={label="Frequency", type="float", min=0, max=50, default=40},
    amplitude={label="Amplitude", type="float", min=0.001, max=5, default=0.5},
}

function WhileTriggerPressed()

    -- Don't allow painting immediately otherwise you get stray lines
    brush.forcePaintingOff(brush.triggerIsPressedThisFrame)

    -- Pick an old position from the buffer
    oldPosition = brush.pastPosition(delay)

    -- How much to mix old and new positions
    mix = (waveform.sine(app.time, frequency) + 1) * amplitude

    newPosition = {
        unityMathf.lerpUnclamped(oldPosition.x, brush.position.x, mix),
        unityMathf.lerpUnclamped(oldPosition.y, brush.position.y, mix),
        unityMathf.lerpUnclamped(oldPosition.z, brush.position.z, mix),
    }
    return {newPosition, brush.rotation}
end
