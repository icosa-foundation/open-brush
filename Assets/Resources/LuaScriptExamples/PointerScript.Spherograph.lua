Settings = {
    description="",
    space="canvas"
}

Parameters = {
    uScale={label="U Scaling", type="float", min=0.001, max=1, default=1},
    vScale={label="V Scaling", type="float", min=0.001, max=1, default=0.1},
    radius={label="Radius", type="float", min=0, max=20, default=5},
}

function OnTriggerPressed()
    initialPos = Brush.position
    return calc()
end

function WhileTriggerPressed()
    return calc()
end

function calc()
    u = Brush.distanceDrawn * uScale
    v = Brush.distanceDrawn * vScale
    p = Sphere(u,v)
    return Tranform:New(
        initialPos:Add(p),
        Rotation.lookRotation(Vector3:New(p.x, p.y, p.z), Vector3.right)
    )
end

function Sphere(u, v)
    return Vector3:New(
        Math:Cos(u) * Math:Cos(v),
        Math:Cos(u) * Math:Sin(v),
        Math:Sin(u)
    )
end

