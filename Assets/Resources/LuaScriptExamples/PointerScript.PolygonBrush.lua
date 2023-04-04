Settings = {
    description="Draws a polygon with the brush"
}

Parameters = {
    points={label="Points", type="int", min=3, max=20, default=5},
    size={label="Size", type="float", min=0.01, max=5, default=1},
}

function WhileTriggerPressed()
    -- Calculate the angle and position of the brush based on app time, points, and size
    angle = app.time * 2 * math.pi
    point = math.floor(angle / (2 * math.pi / points))
    pointAngle = point * 2 * math.pi / points

    -- Calculate the coordinates of the current point and the next point
    x1 = size * math.cos(pointAngle)
    y1 = size * math.sin(pointAngle)
    x2 = size * math.cos(pointAngle + 2 * math.pi / points)
    y2 = size * math.sin(pointAngle + 2 * math.pi / points)

    -- Calculate the brush position based on the angle
    t = (angle - pointAngle) / (2 * math.pi / points)
    x = x1 + (x2 - x1) * t
    y = y1 + (y2 - y1) * t

    -- Set the brush position and rotation
    position = { x, y, 0 }
    rotation = { 0, 0, 0 }

    return { position, rotation }
end
