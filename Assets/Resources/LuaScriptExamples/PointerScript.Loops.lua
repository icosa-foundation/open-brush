Settings = {
    description="Moves the brush in a circle around the current position"
}

Widgets = {
    speed={label="Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=.25},
}

function WhileTriggerPressed()
    angle = app.time * speed
    r = brush.pressure * radius;
    --Move the pointer in a circular path around the actual brush position
    position = { math.sin(angle) * r, math.cos(angle) * r, 0}
    rotation = { 0, 0, 0}
    return { position, rotation }
end
