Settings = {
    space="canvas",
}

Widgets = {
    scale={label="Scale", type="float", min=0.01, max=2, default=0.2},
    height={label="Height", type="float", min=0.01, max=20, default=2},
    offset={label="Offset", type="float", min=0, max=10, default=10},
    grid={label="Grid Size", type="float", min=0.01, max=20, default=2},
}

function Start()
    filledCells = {}
end

function WhileTriggerPressed()
    if not (app.frames % 4 == 0) then
        return {}
    end

    cell = {
        x=Quantize(brush.position.x, grid),
        z=Quantize(brush.position.z, grid),
    }
    key = cell.x .. "," .. cell.z

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

function Patch(cell, gridSize)

    n = gridSize / 2
    x = cell.x
    z = cell.z

    points = {}

    l = -n + x
    r = n + x
    f = n + z
    b = -n + z
    table.insert(points, {{l, GetHeight(l, f), f}, {0, 0, 0}})
    table.insert(points, {{r, GetHeight(r, f), f}, {0, 0, 0}})
    table.insert(points, {{r, GetHeight(r, b), b}, {0, 0, 90}})
    table.insert(points, {{l, GetHeight(l, b), b}, {0, 0, 270}})

    return points
end

function GetHeight(x, y)
    return unityMathf.perlinNoise(x * scale, y * scale) * height + offset;
end
