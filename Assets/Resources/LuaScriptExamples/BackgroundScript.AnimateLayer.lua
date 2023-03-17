Settings = {
    description="Animates a layer"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=.25},
}

function Main()
    angle = app.time * speed
    position = {
        x = math.sin(angle) * radius,
        y = math.cos(angle) * radius,
        z = 0
    }
    rotation = {0, 0, 0}
    layer.setPosition(1, position)
end
