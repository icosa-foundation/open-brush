Settings = {
    description="As you draw, extra lines are added from the start of your stroke to the current position"
}

Parameters = {
    rate={label="Rate", type="int", min=1, max=10, default=10},
}

function OnTriggerPressed()
    initialPos = Brush.position
    currentPos = initialPos
end

function WhileTriggerPressed()

    currentPos = Brush.position

    if App.frames % rate == 0 then
        path = Path:New({
            Transform:New(initialPos, Brush.rotation),
            Transform:New(currentPos, Brush.rotation),
        })
        path:Resample(0.1)
        path:Draw()
    end

    --Leave the actual pointer position unchanged
    return Transform.zero
end
