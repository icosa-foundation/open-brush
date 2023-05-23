Settings = {
    description="Random hull brush cubes as you draw",
    space="canvas"
}

Parameters = {
    maxSize={label="Maximum Size", type="float", min=0.01, max=1, default=0.25},
    spread={label="Spread", type="float", min=0.01, max=1, default=0.25},
    amount={label="Amount", type="float", min=0.001, max=1, default=0.25},
}

function WhileTriggerPressed()

    Brush.colorRgb = Color:New(Random.value, Random.value, Random.value)
    Brush.type = "ShinyHull"
    Brush.size = 0.1
    origin = Brush.position

    if (Random.value < amount) then
        randomOffset = Random.insideUnitSphere:Multiply(spread)
        return drawCube(
            origin:Add(randomOffset),
            Random.value * maxSize
        )
    end
end

function drawCube(center, size)

    points = Path:New()

    -- front face
    points:Insert(center:Add(-size, size, size)) -- top left
    points:Insert(center:Add(size, size, size)) -- top right
    points:Insert(center:Add(size, -size, size)) -- bottom right
    points:Insert(center:Add(-size, -size, size)) -- bottom left
    points:Insert(center:Add(-size, size, size)) -- top left

    -- back face
    points:Insert(center:Add(size, size, -size)) -- top back
    points:Insert(center:Add(-size, size, -size)) -- top left
    points:Insert(center:Add(-size, -size, -size)) -- bottom left
    points:Insert(center:Add(size, -size, -size)) -- bottom back
    points:Insert(center:Add(size, size, -size)) -- top back

    return points
end
