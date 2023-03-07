Widgets = {
    freq={label="Frequency", type="float", min=0, max=20, default=5},
    threshold={label="Threshold", type="float", min=0, max=1, default=.75},
}

function Start()
    brush.forcePaintingOff(false)
end

function WhileTriggerPressed()

    wave = math.cos(brush.distanceDrawn * freq)

    -- turn off painting when we are over the threshold
    brush.forcePaintingOff(wave > threshold)

    --Leave the pointer position unchanged
    return {{0, 0, 0}}
end

function End()
    brush.forcePaintingOff(false)
end
