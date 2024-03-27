Settings = {
    description="Draws dashes with configurable frequency and spacing"
}

Parameters = {
    frequency={label="Frequency", type="float", min=0, max=1, default=.5},
    spacing={label="Spacing", type="float", min=0.1, max=.9, default=.5},
}

function Start()
    Brush:ForcePaintingOff(false)
end
    
function Main()

    wave = Waveform:Pulse(Brush.distanceDrawn, Parameters.frequency, Parameters.spacing)

    -- turn off painting when we are over the threshold
    Brush:ForcePaintingOff(wave > 0)

    --Leave the pointer position unchanged
    return Transform.identity
end

function End()
    Brush:ForcePaintingOff(false)
end
