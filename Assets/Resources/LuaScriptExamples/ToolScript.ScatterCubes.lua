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

    brush.colorRgb ={math.random(), math.random(), math.random()}
    brush.type = "ShinyHull"
    brush.size = 0.1
    origin = brush.position

    if (math.random() < amount) then
        return drawCube({
        x = origin.x + (math.random() * spread * 2) - spread,
        y = origin.y + (math.random() * spread * 2) - spread,
        z = origin.z + (math.random() * spread * 2) - spread
    }, math.random() * maxSize)
    end
end

function drawCube(center, size)

    points = {}

    -- front face
    table.insert(points, {{-size + center.x, size + center.y, size + center.z}}) -- top left
    table.insert(points, {{size + center.x, size + center.y, size + center.z}}) -- top right
    table.insert(points, {{size + center.x, -size + center.y, size + center.z}}) -- bottom right
    table.insert(points, {{-size + center.x, -size + center.y, size + center.z}}) -- bottom left
    table.insert(points, {{-size + center.x, size + center.y, size + center.z}}) -- top left

    -- back face
    table.insert(points, {{size + center.x, size + center.y, -size + center.z}}) -- top back
    table.insert(points, {{-size + center.x, size + center.y, -size + center.z}}) -- top left
    table.insert(points, {{-size + center.x, -size + center.y, -size + center.z}}) -- bottom left
    table.insert(points, {{size + center.x, -size + center.y, -size + center.z}}) -- bottom back
    table.insert(points, {{size + center.x, size + center.y, -size + center.z}}) -- top back

    return points
end
