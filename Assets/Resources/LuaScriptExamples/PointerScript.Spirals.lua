Settings = {
    description="Moves in a circle that grows bigger as long as the trigger is help down"
}

Widgets = {
    speed={label="Speed", type="float", min=0.01, max=20, default=16},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function WhileTriggerPressed()
    angle = app.time * speed
    r = 0
    if (brush.triggerIsPressed) then
        r = radius * brush.timeSincePressed
    end
    position = {math.sin(angle) * r, math.cos(angle) * r, 0}
    return {position}
end
