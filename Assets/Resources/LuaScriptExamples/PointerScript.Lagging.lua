Settings = {
    description="Oscillates the brush between the current position and an earlier one",
    space="canvas"
}

Parameters = {
    delay={label="Delay", type="int", min=0, max=100, default=20},
    frequency={label="Frequency", type="float", min=0, max=50, default=20},
    amplitude={label="Amplitude", type="float", min=0.001, max=5, default=0.5},
}

function WhileTriggerPressed()

    -- Don't allow painting immediately otherwise you get stray lines
    if (brush.triggerIsPressedThisFrame) then
        brush.forceNewStroke()
    end

    -- Pick an old position from the buffer
    oldPosition = brush.pastPosition(delay)

    -- How much to mix old and new positions
    mix = (waveform.sine(app.time, frequency) + 1) * amplitude

    --Interpolate back and forth between old and current positions
    newPosition = {
        unityMathf.lerpUnclamped(oldPosition.x, brush.position.x, mix),
        unityMathf.lerpUnclamped(oldPosition.y, brush.position.y, mix),
        unityMathf.lerpUnclamped(oldPosition.z, brush.position.z, mix),
    }

    return {newPosition, brush.rotation}

end
