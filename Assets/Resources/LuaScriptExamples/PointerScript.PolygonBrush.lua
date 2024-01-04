Settings = {
    description="Draws a polygon with the brush"
}

Parameters = {
    points={label="Points", type="int", min=3, max=20, default=5},
    size={label="Size", type="float", min=0.01, max=5, default=1},
}

function Main()

    -- Calculate the angle and position of the brush based on app time, points, and size
    angle = App.time * 2 * Math.pi
    point = Math:Floor(angle / (2 * Math.pi / Parameters.points))
    pointAngle = point * 2 * Math.pi / Parameters.points

    -- Calculate the coordinates of the current point and the next point
    x1 = Parameters.size * Math:Cos(pointAngle)
    y1 = Parameters.size * Math:Sin(pointAngle)
    x2 = Parameters.size * Math:Cos(pointAngle + 2 * Math.pi / Parameters.points)
    y2 = Parameters.size * Math:Sin(pointAngle + 2 * Math.pi / Parameters.points)

    -- Calculate the brush position based on the angle
    t = (angle - pointAngle) / (2 * Math.pi / Parameters.points)
    x = x1 + (x2 - x1) * t
    y = y1 + (y2 - y1) * t

    -- Set the brush position and rotation
    position = Vector3:New(x, y, 0)
    return Transform:New(position)

end
