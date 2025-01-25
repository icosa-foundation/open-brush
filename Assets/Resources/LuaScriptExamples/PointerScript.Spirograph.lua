Settings = {
    description="Draws a spirograph pattern around the pointer."
}

Parameters = {
    outerRadius={label="Outer Radius", type="float", min=0.1, max=5, default=2},
    innerRadius={label="Inner Radius", type="float", min=0.1, max=5, default=1},
    penOffset={label="Pen Offset", type="float", min=0.1, max=5, default=1},
    speed={label="Speed", type="float", min=0.1, max=10, default=2},
}

function Main()
    local t = App.time * Parameters.speed
    local outerAngle = t
    local innerAngle = t * Parameters.outerRadius / Parameters.innerRadius

    local x = (Parameters.outerRadius - Parameters.innerRadius) * Math:Cos(outerAngle) + Parameters.penOffset * Math:Cos(innerAngle)
    local y = (Parameters.outerRadius - Parameters.innerRadius) * Math:Sin(outerAngle) - Parameters.penOffset * Math:Sin(innerAngle)

    local position = Vector2:New(x, y):OnZ() -- Convert 2D to 3D on the Z plane
    return Transform:New(position)
end
