Settings = {
    description="Draws tiles that follow a hilly landscape as you hold the trigger",
    space="canvas",
}

Widgets = {
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
        x=Quantize(brush.position.x, grid),
        z=Quantize(brush.position.z, grid),
    }

    --A unique string key for each potential tile
    key = cell.x .. "," .. cell.z

    --Only draw tiles in empty cells
    if filledCells[key]==nil then
        filledCells[key] = true
        color.jitter()
        return Patch(cell, grid)
    else
        return {}
    end
end

function Quantize(val, size)
    return unityMathf.round(val / size) * size
end

--Generates the path for each tile
function Patch(cell, gridSize)

    --Half the size of each tile
    distance = gridSize / 2

    points = {}

    --Left, right, forward and back offsets
    l = -distance + cell.x
    r = distance + cell.x
    f = distance + cell.z
    b = -distance + cell.z

    table.insert(points, {{l, GetHeight(l, f), f}, {0, 0, 0}})
    table.insert(points, {{r, GetHeight(r, f), f}, {0, 0, 0}})
    table.insert(points, {{r, GetHeight(r, b), b}, {0, 0, 90}})
    table.insert(points, {{l, GetHeight(l, b), b}, {0, 0, 270}})

    return points
end

function GetHeight(x, y)
    return unityMathf.perlinNoise(x * scale, y * scale) * height + offset;
end
