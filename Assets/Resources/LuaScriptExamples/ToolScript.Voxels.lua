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

function Main()

    if Brush.triggerIsPressed then

        cell = Vector3:New(
                quantize(Brush.position.x, Parameters.gridSize),
                quantize(Brush.position.y, Parameters.gridSize),
                quantize(Brush.position.z, Parameters.gridSize)
        )

        key = cell.x .. "," .. cell.y .. "," .. cell.z

        if filledCells[key]==nil then
            filledCells[key] = true
            path = cube(cell, Parameters.gridSize)
            path:SampleByDistance(0.1)
            return path
        else
            return Path:New()
        end

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

    points:Insert(Transform:New(center:Add(d, d, d), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(center:Add(d, d, -d), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(center:Add(d, -d, -d), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(center:Add(d, -d, d), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(center:Add(d, d, d), Rotation:New(0, 0, 90)))

    points:Insert(Transform:New(center:Add(d, d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(-d, d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(-d, -d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(d, -d, -d), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(center:Add(d, d, -d), Rotation:New(0, 0, 180)))

    points:Insert(Transform:New(center:Add(-d, d, -d), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(center:Add(-d, d, d), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(center:Add(-d, -d, d), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(center:Add(-d, -d, -d), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(center:Add(-d, d, -d), Rotation:New(0, 0, -90)))

    return points
end
