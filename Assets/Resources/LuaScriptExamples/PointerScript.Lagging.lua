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
    if (Brush.triggerIsPressedThisFrame) then
        Brush:ForceNewStroke()
    end

    -- Pick an old position from the buffer
    oldPosition = Brush:GetPastPosition(delay)

    -- How much to mix old and new positions
    mix = (Waveform:Sine(App.time, frequency) + 1) * amplitude

    --Interpolate back and forth between old and current positions
    newPosition = Vector3.LerpUnclamped(oldPosition, Brush.position, mix)
    return Transform:New(newPosition, Brush.rotation)

end
