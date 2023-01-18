Widgets = {
    speed={label="Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=.25},
}

function OnTriggerPressed()
    color.setRgb({
        math.random(), math.random(), math.random(),
    })
end

function WhileTriggerPressed()
    angle = app.time * speed
    r = brush.pressure * radius;
    pos = {math.sin(angle) * r, math.cos(angle) * r, 0}
    rot = {0, 0, 0}
    return {pos, rot}
end
