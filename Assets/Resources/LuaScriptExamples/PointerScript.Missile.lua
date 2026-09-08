Settings = {
    description="Like Laser Beam except you can steer the line while holding the trigger",
    space="canvas"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
}

function Main()

    if Brush.triggerPressedThisFrame then

        --Store the brush transform at the point we press the trigger
        currentPos = Brush.position
        currentRotation = Brush.rotation

    elseif Brush.triggerIsPressed then

        --Similar to the LaserBeam PointerScript except we can change the direction during "flight"
        direction = Brush.direction
        currentPos = currentPos + (direction * Parameters.speed)
        return Transform:New(currentPos, currentRotation)

    end

end
