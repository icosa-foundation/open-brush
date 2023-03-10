Settings = {
    description="As you draw, extra lines are added from the start of your stroke to the current position"
}

Widgets = {
    rate={label="Rate", type="int", min=1, max=10, default=10},
 }

function OnTriggerPressed()
    initialPos = {
        brush.position.x,
        brush.position.y,
        brush.position.z,
    }
    currentPos = initialPos
end

function WhileTriggerPressed()

    currentPos = {
        brush.position.x,
        brush.position.y,
        brush.position.z,
    }

    if app.frames % rate == 0 then
        draw.path({
            {initialPos, brush.rotation},
            {currentPos, brush.rotation},
        })
    end

    --Leave the actual pointer position unchanged
    return {{0, 0, 0}}
end
