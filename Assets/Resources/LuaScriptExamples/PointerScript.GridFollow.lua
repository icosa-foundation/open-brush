Settings = {space="canvas"}

Widgets = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
}

function OnTriggerPressed()
    --Store the brush transform at the moment we start drawing a line
    drawingPosition = brush.position
    currentRotation = brush.rotation
    return { drawingPosition, currentRotation}
end

function WhileTriggerPressed()

    -- A vector from the actual brush position to the calculated one used for drawing
    vector = {
        x = brush.position.x - drawingPosition.x,
        y = brush.position.y - drawingPosition.y,
        z = brush.position.z - drawingPosition.z,
    }

    -- Store whether the vector is positive or negative in each axis
    signs = {
        x = unityMathf.sign(vector.x),
        y = unityMathf.sign(vector.y),
        z = unityMathf.sign(vector.z),
    }

    -- Make them all positive
    vector = {
        x = unityMathf.abs(vector.x),
        y = unityMathf.abs(vector.y),
        z = unityMathf.abs(vector.z),
    }

    -- Zero out all directions except the biggest
    -- Set the biggest direction equal to "speed"
    if vector.x > vector.y and vector.x > vector.z then
        vector = {x = speed / 3, y = 0, z = 0}
    elseif vector.y > vector.x and vector.y > vector.z then
        vector = {x = 0, y = speed / 3, z = 0}
    elseif vector.z > vector.x and vector.z > vector.y then
        vector = {x = 0, y = 0, z = speed / 3}
    end

    -- Restore the positive/negative for each direction
    vector = {
        x = vector.x * signs.x,
        y = vector.y * signs.y,
        z = vector.z * signs.z,
    }

    -- Move the position used for drawing by the result of the above calculations
    drawingPosition = {
        x = drawingPosition.x + vector.x,
        y = drawingPosition.y + vector.y,
        z = drawingPosition.z + vector.z,
    }

    return {drawingPosition, currentRotation}

end
