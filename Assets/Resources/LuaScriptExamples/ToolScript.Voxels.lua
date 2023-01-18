Settings = {
    space="canvas",
}

Widgets = {
    gridSize={label="Grid Size", type="float", min=0.01, max=20, default=2},
}

function OnStart()
    filledCells = {}
end

function WhileTriggerPressed()

    cell = {
        x=Quantize(brush.position.x, gridSize),
        y=Quantize(brush.position.y, gridSize),
        z=Quantize(brush.position.z, gridSize),
    }
    key = cell.x .. "," .. cell.y .. "," .. cell.z

    if (filledCells[key]==nil) then
        filledCells[key] = true
        return Cube(cell, gridSize)
    else
        return {}
    end
end

function Quantize(val, grid)
    return Mathf.Round(val / grid) * grid
end

function Cube(cell, gridSize)

    n = gridSize / 2
    x = cell.x
    y = cell.y
    z = cell.z

    points = {}

    table.insert(points, {{-n + x, n + y, n + z}, {0, 0, 0}})
    table.insert(points, {{n + x, n + y, n + z}, {0, 0, 0}})
    table.insert(points, {{n + x, -n + y, n + z}, {0, 0, 0}})
    table.insert(points, {{-n + x, -n + y, n + z}, {0, 0, 0}})
    table.insert(points, {{-n + x, n + y, n + z}, {0, 0, 0}})

    table.insert(points, {{n + x, n + y, n + z}, {0, 0, 90}})
    table.insert(points, {{n + x, n + y, -n + z}, {0, 0, 90}})
    table.insert(points, {{n + x, -n + y, -n + z}, {0, 0, 90}})
    table.insert(points, {{n + x, -n + y, n + z}, {0, 0, 90}})
    table.insert(points, {{n + x, n + y, n + z}, {0, 0, 90}})

    table.insert(points, {{n + x, n + y, -n + z}, {0, 0, 280}})
    table.insert(points, {{-n + x, n + y, -n + z}, {0, 0, 280}})
    table.insert(points, {{-n + x, -n + y, -n + z}, {0, 0, 280}})
    table.insert(points, {{n + x, -n + y, -n + z}, {0, 0, 280}})
    table.insert(points, {{n + x, n + y, -n + z}, {0, 0, 280}})

    table.insert(points, {{-n + x, n + y, -n + z}, {0, 0, 270}})
    table.insert(points, {{-n + x, n + y, n + z}, {0, 0, 270}})
    table.insert(points, {{-n + x, -n + y, n + z}, {0, 0, 270}})
    table.insert(points, {{-n + x, -n + y, -n + z}, {0, 0, 270}})
    table.insert(points, {{-n + x, n + y, -n + z}, {0, 0, 270}})

    return points
end
