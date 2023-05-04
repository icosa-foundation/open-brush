Settings = {
    description="Draws regular blocks in space as you draw (best with the hull brush)",
    space="canvas"
}

Parameters = {
    gridSize={label="Grid Size", type="float", min=0.01, max=20, default=2},
}

function Start()
    filledCells = {}
end

function WhileTriggerPressed()

    cell = Vector3:New(
        quantize(Brush.position.x, gridSize),
        quantize(Brush.position.y, gridSize),
        quantize(Brush.position.z, gridSize)
    )
    key = cell.x .. "," .. cell.y .. "," .. cell.z

    if (filledCells[key]==nil) then
        filledCells[key] = true
        return cube(cell, gridSize)
    else
        return Path:New()
    end
end

function quantize(val, grid)
    return Math:Round(val / grid) * grid
end

function cube(center, gridSize)

    points = Path:New()
    d = gridSize / 2

    points:Insert(Transform:New(center:Add(-d, d, d), Rotation.zero))
    points:Insert(Transform:New(center:Add(d, d, d), Rotation.zero))
    points:Insert(Transform:New(center:Add(d, -d, d), Rotation.zero))
    points:Insert(Transform:New(center:Add(-d, -d, d), Rotation.zero))
    points:Insert(Transform:New(center:Add(-d, d, d), Rotation.zero))

    points:Insert(Transform:New(center:Add(d, d, d), Rotation.clockwise))
    points:Insert(Transform:New(center:Add(d, d, -d), Rotation.clockwise))
    points:Insert(Transform:New(center:Add(d, -d, -d), Rotation.clockwise))
    points:Insert(Transform:New(center:Add(d, -d, d), Rotation.clockwise))
    points:Insert(Transform:New(center:Add(d, d, d), Rotation.clockwise))

    points:Insert(Transform:New(center:Add(d, d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(-d, d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(-d, -d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(d, -d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(d, d, -d), Rotation:New(0, 0, 180)))

    points:Insert(Transform:New(center:Add(-d, d, -d), Rotation.anticlockwise))
    points:Insert(Transform:New(center:Add(-d, d, d), Rotation.anticlockwise))
    points:Insert(Transform:New(center:Add(-d, -d, d), Rotation.anticlockwise))
    points:Insert(Transform:New(center:Add(-d, -d, -d), Rotation.anticlockwise))
    points:Insert(Transform:New(center:Add(-d, d, -d), Rotation.anticlockwise))

    return points
end
