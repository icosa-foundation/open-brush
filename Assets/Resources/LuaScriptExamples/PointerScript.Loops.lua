Settings = {
    description="Moves the brush in a circle around the current position"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=.25},
}

function WhileTriggerPressed()

    size = Brush.pressure * radius

    --Move the pointer in a circular path around the actual brush position
    angle = App.time * speed
    position = Vector2:PointOnCircle(angle):Multiply(size)
    return Transform:New(position, Brush.rotation)
end
