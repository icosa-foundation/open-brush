Widgets = {
    freq={label="Frequency", type="float", min=0, max=20, default=5},
    threshold={label="Threshold", type="float", min=0, max=1, default=.75},
}

function Start()
    brush.forcePaintingOff(false)
end

function WhileTriggerPressed()
    wave = math.cos(brush.distanceDrawn * freq)
    if wave > threshold then
        brush.forcePaintingOff(true)
    else
        brush.forcePaintingOff(false)
    end
    --Do nothing to the actual pointer
    pos = {0, 0, 0}
    rot = {0, 0, 0}
    return {pos, rot}
end

function End()
    brush.forcePaintingOff(false)
end
