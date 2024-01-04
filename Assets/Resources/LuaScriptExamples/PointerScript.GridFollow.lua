Settings = {
    description="Follows the brush but only following horizontal or vertical lines",
    space="canvas"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=.1, default=.025},
    framesBetweenChanges={label="Number of frames between direction changes", type="int", min=1, max=20, default=10},
}

function Main()

    if Brush.triggerPressedThisFrame then

        --Store the brush transform at the moment we start drawing a line
        currentPosition = Brush.position
        vector = Vector3.zero
        framesSinceChange = 0

    elseif Brush.triggerIsPressed then

        framesSinceChange = framesSinceChange + 1

        if framesSinceChange > Parameters.framesBetweenChanges then

            -- A vector from the actual Brush.Position to the calculated one used for drawing
            vector = Brush.position - currentPosition

            -- Store whether the vector is positive or negative in each axis
            signs = Vector3:New(
                    Math:Sign(vector.x),
                    Math:Sign(vector.y),
                    Math:Sign(vector.z)
            )

            -- Make them all positive
            vector = Vector3:New(
                    Math:Abs(vector.x),
                    Math:Abs(vector.y),
                    Math:Abs(vector.z)
            )

            -- Zero out all directions except the biggest
            -- and set the biggest direction equal to "speed"
            if vector.x > vector.y and vector.x > vector.z then
                vector = Vector3:New(Parameters.speed, 0, 0)
                rotation = Rotation:New(0, 90 * signs.y, 0)
            elseif vector.y > vector.x and vector.y > vector.z then
                vector = Vector3:New(0, Parameters.speed, 0)
                rotation = Rotation:New(90 * signs.x, 0, 0)
            elseif vector.z > vector.x and vector.z > vector.y then
                vector = Vector3:New(0, 0, Parameters.speed)
                local rot = signs.z < 0 and -180 or 0 -- If the z direction is negative, rotate 180 degrees
                rotation = Rotation:New(0, 0, rot)
            end

            -- Restore the positive/negative for each direction
            vector = vector:ScaleBy(signs)
            framesSinceChange = 0

        end

        -- Shift the brush position based on the value calculated above
        currentPosition = currentPosition + vector

        return Transform:New(currentPosition, rotation)

    end

end
