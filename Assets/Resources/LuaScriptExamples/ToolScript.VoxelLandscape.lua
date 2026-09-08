Settings = {
    description="Draws a blocky landscape (best with the hull brush)",
    space="canvas"
}

Parameters = {
    xzGrid = {label="Horizontal Spacing", type="float", min=0.01, max=20, default=2},
    yGrid={label="Vertical Spacing", type="float", min=0.01, max=20, default=0.25},
}

function Start()
    filledCells = {}
end

function Main()

    if Brush.triggerIsPressed then

        --Calculate which cell we are in
        cell = {
            x = quantize(Brush.position.x, Parameters.xzGrid),
            z = quantize(Brush.position.z, Parameters.xzGrid),
        }
        key = cell.x .. "," .. cell.z

        top = quantize(Brush.position.y, Parameters.yGrid)
        bottom = filledCells[key]

        if bottom==nil or bottom < top then
            filledCells[key] = top
            if bottom == nil then
                bottom = 0
            end
            path = cube(cell, bottom, top, Parameters.xzGrid)
            path:SampleByDistance(0.1)
            return path
        else
            return Path:New()
        end

    end

end

function quantize(val, size)
    return Math:Round(val / size) * size
end

function cube(cell, bottom, top, gridSize)

    distance = gridSize / 2
    x = cell.x
    z = cell.z

    points = Path:New()

    points:Insert(Transform:New(Vector3:New(-distance + x, top, distance + z), Rotation.zero))
    points:Insert(Transform:New(Vector3:New(distance + x, top, distance + z), Rotation.zero))
    points:Insert(Transform:New(Vector3:New(distance + x, bottom, distance + z), Rotation.zero))
    points:Insert(Transform:New(Vector3:New(-distance + x, bottom, distance + z), Rotation.zero))
    points:Insert(Transform:New(Vector3:New(-distance + x, top, distance + z), Rotation.zero))

    points:Insert(Transform:New(Vector3:New(distance + x, top, distance + z), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(Vector3:New(distance + x, top, -distance + z), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(Vector3:New(distance + x, bottom, -distance + z), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(Vector3:New(distance + x, bottom, distance + z), Rotation:New(0, 0, 90)))
    points:Insert(Transform:New(Vector3:New(distance + x, top, distance + z), Rotation:New(0, 0, 90)))

    points:Insert(Transform:New(Vector3:New(distance + x, top, -distance + z), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(Vector3:New(-distance + x, top, -distance + z), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(Vector3:New(-distance + x, bottom, -distance + z), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(Vector3:New(distance + x, bottom, -distance + z), Rotation:New(0, 0, 180)))
    points:Insert(Transform:New(Vector3:New(distance + x, top, -distance + z), Rotation:New(0, 0, 180)))

    points:Insert(Transform:New(Vector3:New(-distance + x, top, -distance + z), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(Vector3:New(-distance + x, top, distance + z), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(Vector3:New(-distance + x, bottom, distance + z), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(Vector3:New(-distance + x, bottom, -distance + z), Rotation:New(0, 0, -90)))
    points:Insert(Transform:New(Vector3:New(-distance + x, top, -distance + z), Rotation:New(0, 0, -90)))

    return points
end
