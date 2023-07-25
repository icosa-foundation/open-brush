Settings = {
    description="Moves in a circle that grows bigger as long as the trigger is help down"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=20, default=16},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function Main()

    if Brush.triggerIsPressed then

        currentRadius = radius * Brush.timeSincePressed
        angle = Brush.timeSincePressed * speed
        position = Vector2:PointOnCircle(angle):Multiply(currentRadius):OnZ()
        return Transform:New(position, Brush.rotation)

    end

end
