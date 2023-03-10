Settings = {
    description="Draws dashes with configurable frequency and spacing"
}

Widgets = {
    frequency={label="Frequency", type="float", min=0, max=20, default=5},
    spacing={label="Spacing", type="float", min=0, max=1, default=.75},
}

function Start()
    brush.forcePaintingOff(false)
end

function WhileTriggerPressed()

    wave = math.cos(brush.distanceDrawn * frequency)

    -- turn off painting when we are over the threshold
    brush.forcePaintingOff(wave > spacing)

    --Leave the pointer position unchanged
    return {{0, 0, 0}}
end

function End()
    brush.forcePaintingOff(false)
end
