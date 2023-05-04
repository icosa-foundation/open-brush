Settings = {
    description="Follows the brush but only following horizontal or vertical lines",
    space="canvas"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
    framesBetweenChanges={label="Number of frames between direction changes", type="int", min=0, max=20, default=5},
}

function OnTriggerPressed()
    --Store the brush transform at the moment we start drawing a line
    currentPosition = Brush.position
    currentRotation = Brush.rotation
    vector = Vector3.zero
    framesSinceChange = 0
    return Transform:New(currentPosition, currentRotation)
end

function WhileTriggerPressed()

    framesSinceChange = framesSinceChange + 1

    if framesSinceChange > framesBetweenChanges then

        -- A vector from the actual Brush.Position to the calculated one used for drawing
        vector = Brush.position.Subtract(currentPosition)

        -- Store whether the vector is positive or negative in each axis
        signs = Vector3:New(
            Math.Sign(vector.x),
            Math.Sign(vector.y),
            Math.Sign(vector.z)
        )

        -- Make them all positive
        vector = Vector3:New(
            Math.Abs(vector.x),
            Math.Abs(vector.y),
            Math.Abs(vector.z)
        )

        -- Zero out all directions except the biggest
        -- Set the biggest direction equal to "speed"
        if vector.x > vector.y and vector.x > vector.z then
            vector = Vector3:New(speed/3, 0, 0)
            rotation = Rotation:New(0, currentRotation.y, currentRotation.z)
        elseif vector.y > vector.x and vector.y > vector.z then
            vector = Vector3:New(0, speed/3, 0)
            rotation = Rotation:New(currentRotation.x, 0, currentRotation.z)
        elseif vector.z > vector.x and vector.z > vector.y then
            vector = Vector3:New(0, 0, speed/3)
            rotation = Rotation:New(currentRotation.x, currentRotation.y, 0)
        end

        -- Restore the positive/negative for each direction
        vector = currentPosition:Scale(signs)
        framesSinceChange = 0

    end

    -- Move the position used for drawing by the result of the above calculations
    currentPosition = currentPosition:Add(vector)

    return Transform:New(currentPosition, currentRotation)

end
