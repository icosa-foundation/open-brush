Settings = {
    description="Random hull brush cubes as you draw",
    space="canvas"
}

Parameters = {
    maxSize={label="Maximum Size", type="float", min=0.01, max=1, default=0.25},
    spread={label="Spread", type="float", min=0.01, max=1, default=0.25},
    amount={label="Amount", type="float", min=0.001, max=1, default=0.25},
}

function Start()
    originalBrushType = Brush.type
    originalBrushSize = Brush.size
    Brush.type = "Shiny Hull"
    Brush.size = 0.1
end

function Main()

    if Brush.triggerIsPressed then

        Brush.colorRgb = Color:New(Random.value, Random.value, Random.value)
        origin = Brush.position

        if Random.value < Parameters.amount then
            randomOffset = Random.insideUnitSphere * Parameters.spread
            return drawCube(
                origin + randomOffset,
                Random.value * Parameters.maxSize
            )
        end

    end

end

function End()
    -- If the user hasn't changed settings then restore the previous values
    if Brush.type == "Shiny Hull" then
        Brush.type = originalBrushType
    end
    if Brush.size == 0.1 then
        Brush.size = originalBrushSize
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
