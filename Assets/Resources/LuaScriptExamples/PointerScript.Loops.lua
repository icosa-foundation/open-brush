Settings = {
    description="Moves the brush in a circle around the current position"
}

Parameters = {
    speed={label="Speed", type="float", min=1, max=1000, default=500},
    radius={label="Radius", type="float", min=0.01, max=5, default=.25},
}

function Main()

    --Move the pointer in a circular path around the actual brush position
    angle = (App.time * Parameters.speed) % 360
    position2d = Vector2:PointOnCircle(angle) * Parameters.radius
    return Transform:New(position2d:OnZ())

end
