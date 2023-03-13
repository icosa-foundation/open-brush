Settings = {
    description="Draws a blocky landscape (best with the hull brush)",
    space="canvas",
}

Parameters = {
    xzSpacing ={ label="Horizontal Spacing", type="float", min=0.01, max=20, default=2},
    yGrid={label="Vertical Spacing", type="float", min=0.01, max=20, default=0.25},
}

function Start()
    filledCells = {}
end

function WhileTriggerPressed()

    cell = {
        x=Quantize(brush.position.x, xzGrid),
        z=Quantize(brush.position.z, xzGrid),
    }
    key = cell.x .. "," .. cell.z

    top = Quantize(brush.position.y, yGrid)
    bottom = filledCells[key]
    if bottom==nil or bottom < top then
        filledCells[key] = top
        if bottom == nil then
            bottom = 0
        end
        return Cube(cell, bottom, top, xzGrid)
    else
        return {}
    end
end

function Quantize(val, size)
    return unityMathf.round(val / size) * size
end

function Cube(cell, bottom, top, gridSize)

    distance = gridSize / 2
    x = cell.x
    z = cell.z

    points = {}

    table.insert(points, { { -distance + x, top, distance + z}, { 0, 0, 0}})
    table.insert(points, { { distance + x, top, distance + z}, { 0, 0, 0}})
    table.insert(points, { { distance + x, bottom, distance + z}, { 0, 0, 0}})
    table.insert(points, { { -distance + x, bottom, distance + z}, { 0, 0, 0}})
    table.insert(points, { { -distance + x, top, distance + z}, { 0, 0, 0}})

    table.insert(points, { { distance + x, top, distance + z}, { 0, 0, 90}})
    table.insert(points, { { distance + x, top, -distance + z}, { 0, 0, 90}})
    table.insert(points, { { distance + x, bottom, -distance + z}, { 0, 0, 90}})
    table.insert(points, { { distance + x, bottom, distance + z}, { 0, 0, 90}})
    table.insert(points, { { distance + x, top, distance + z}, { 0, 0, 90}})

    table.insert(points, { { distance + x, top, -distance + z}, { 0, 0, 180}})
    table.insert(points, { { -distance + x, top, -distance + z}, { 0, 0, 180}})
    table.insert(points, { { -distance + x, bottom, -distance + z}, { 0, 0, 180}})
    table.insert(points, { { distance + x, bottom, -distance + z}, { 0, 0, 180}})
    table.insert(points, { { distance + x, top, -distance + z}, { 0, 0, 180}})

    table.insert(points, { { -distance + x, top, -distance + z}, { 0, 0, 270}})
    table.insert(points, { { -distance + x, top, distance + z}, { 0, 0, 270}})
    table.insert(points, { { -distance + x, bottom, distance + z}, { 0, 0, 270}})
    table.insert(points, { { -distance + x, bottom, -distance + z}, { 0, 0, 270}})
    table.insert(points, { { -distance + x, top, -distance + z}, { 0, 0, 270}})

    return points
end
