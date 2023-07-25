Settings = {
    description="Draws dashes with configurable frequency and spacing"
}

Parameters = {
    frequency={label="Frequency", type="float", min=0, max=20, default=5},
    spacing={label="Spacing", type="float", min=0, max=1, default=.75},
}

function Start()
    Brush:ForcePaintingOff(false)
end

function Main()

    wave = Math:Cos(Brush.distanceDrawn * frequency)

    -- turn off painting when we are over the threshold
    Brush:ForcePaintingOff(wave > spacing)

    --Leave the pointer position unchanged
    return Transform.identity
end

function End()
    Brush:ForcePaintingOff(false)
end
