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

    --Do nothing to the actual pointer
    pos = {0, 0, 0}
    rot = {0, 0, 0}
    return {pos, rot}
end
