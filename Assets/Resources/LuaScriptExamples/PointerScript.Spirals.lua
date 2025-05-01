Settings = {
    description="Moves in a circle that grows bigger as long as the trigger is help down"
}

Parameters = {
    speed={label="Speed", type="float", min=1, max=1000, default=500},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function Main()

    if Brush.triggerIsPressed then
        currentRadius = Parameters.radius * Brush.timeSincePressed
        angle = Brush.timeSincePressed * Parameters.speed
        position2d = Vector2:PointOnCircle(angle) * currentRadius
        return Transform:New(position2d:OnZ(), Brush.rotation)
    end

end
