Settings = {
    description="Moves in a circle that grows bigger as long as the trigger is help down"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=20, default=16},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function WhileTriggerPressed()
    angle = App.time * speed
    r = 0
    if (Brush.triggerIsPressed) then
        r = radius * Brush.timeSincePressed
    end
    return Transform:New(
        Math:Sin(angle) * r,
        Math:Cos(angle) * r,
        0
    )
end
