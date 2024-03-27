Settings = {
    description="Oscillates the brush between the current position and an earlier one",
    space="canvas"
}

Parameters = {
    delay={label="Delay", type="int", min=0, max=30, default=6},
    frequency={label="Frequency", type="float", min=0, max=20, default=10},
    amplitude={label="Amplitude", type="float", min=0.001, max=2, default=0.7},
}

function Main()

    if Brush.triggerPressedThisFrame then

        counter = 0

    elseif Brush.triggerIsPressed then

        -- Pick an old position from the buffer
        oldPosition = Brush:GetPastPosition(Math:Min(counter, Parameters.delay))

        counter = counter + 1

        -- How much to mix old and new positions
        mix = (Waveform:Sine(App.time, Parameters.frequency) + 1) * Parameters.amplitude

        --Interpolate back and forth between old and current positions
        newPosition = Vector3:LerpUnclamped(oldPosition, Brush.position, mix)
        return Transform:New(newPosition, Brush.rotation)

    end

end
