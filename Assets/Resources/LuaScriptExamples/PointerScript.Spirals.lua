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
    pos = {math.sin(angle) * r, math.cos(angle) * r, 0}
    rot = {0, 0, 0}
    return {pos, rot}
end
