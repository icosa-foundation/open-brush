Settings = {
    description="Draws tiles that follow a hilly landscape as you hold the trigger",
    space="canvas"
}

Parameters = {
    scale={label="Scale", type="float", min=0.01, max=2, default=0.2},
    height={label="Height", type="float", min=0.01, max=20, default=2},
    offset={label="Offset", type="float", min=0, max=10, default=10},
    grid={label="Grid Size", type="float", min=0.01, max=20, default=2},
}

function Start()
    --An empty table we can use to store the coordinates where we have drawn a tile
    filledCells = {}
end

function WhileTriggerPressed()

    cell = {
        x=quantize(Brush.position.x, grid),
        z=quantize(Brush.position.z, grid),
   }

    --A unique string key for each potential tile
    key = cell.x .. "," .. cell.z

    --Only draw tiles in empty cells
    if filledCells[key]==nil then
        filledCells[key] = true
        Brush:JitterColor()
        return patch(cell, grid)
    else
        return Path:New()
    end
end

function quantize(val, size)
    return Math:Round(val / size) * size
end

--Generates the path for each tile
function patch(cell, gridSize)

    --Half the size of each tile
    distance = gridSize / 2

    points = Path:New()

    --Left, right, forward and back offsets
    l = -distance + cell.x
    r = distance + cell.x
    f = distance + cell.z
    b = -distance + cell.z

    points:Insert(Transform:New(Vector3:New(l, GetHeight(l, f), f), Rotation.zero))
    points:Insert(Transform:New(Vector3:New(r, GetHeight(r, f), f), Rotation.zero))
    points:Insert(Transform:New(Vector3:New(r, GetHeight(r, b), b), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(Vector3:New(l, GetHeight(l, b), b), Rotation:New(0, 0, 270)))
    points:Resample(0.1)

    return points
end

function GetHeight(x, y)
    return Math:PerlinNoise(x * scale, y * scale) * height + offset
end
