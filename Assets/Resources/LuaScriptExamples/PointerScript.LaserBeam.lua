Settings = {
    description="Draws a line in the chosen direction for as long as you hold the trigger",
    space="canvas"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
}

function Main()

    if Brush.triggerPressedThisFrame then

        --Store the brush transform at the point we press the trigger
        direction = Brush.direction
        currentPos = Brush.position
        currentRotation = Brush.rotation

    elseif Brush.triggerIsPressed then

        -- Move the pointer in the direction we were facing when we pressed the trigger
        currentPos = currentPos + (direction * Parameters.speed)
        return Transform:New(currentPos, currentRotation)

    end

end
