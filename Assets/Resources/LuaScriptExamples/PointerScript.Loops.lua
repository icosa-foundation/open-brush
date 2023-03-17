Settings = {
    description="Moves the brush in a circle around the current position"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=.25},
}

function WhileTriggerPressed()

    size = brush.pressure * radius;

    --Move the pointer in a circular path around the actual brush position
    angle = app.time * speed
    position = {
        x = math.sin(angle) * size,
        y = math.cos(angle) * size,
        z = 0
    }
    rotation = {0, 0, 0}
    return {position, rotation}
end
