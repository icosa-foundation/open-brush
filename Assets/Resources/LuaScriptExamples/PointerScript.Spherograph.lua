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
    initialPos = brush.position
    return Calc()
end

function WhileTriggerPressed()
    return Calc()
end

function Calc()
    u = brush.distanceDrawn * uScale
    v = brush.distanceDrawn * vScale
    p = Sphere(u,v)
    return {
        position={p.x + initialPos.x, p.y + initialPos.y, p.z + initialPos.z,},
        rotation=unityQuaternion.lookRotation({p.x, p.y, p.z}, {1, 0, 0})
    }
end

function Sphere(u, v)
    return {
        x = math.cos(u) * math.cos(v),
        y = math.cos(u) * math.sin(v),
        z = math.sin(u),
    }
end

