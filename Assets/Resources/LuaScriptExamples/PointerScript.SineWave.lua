Settings = {
    description="Moves the brush in a simple up/down wave as you draw"
}

Parameters = {
    frequency={label="Frequency", type="float", min=0.1, max=2, default=1},
    amplitude={label="Amplitude", type="float", min=0.1, max=1, default=.2},
}

function Main()
    y = Waveform:Sine(Brush.distanceMoved, Parameters.frequency) * Parameters.amplitude
    return Transform:Position(0, y, 0)
end
