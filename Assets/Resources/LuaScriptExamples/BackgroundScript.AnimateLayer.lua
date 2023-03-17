Settings = {
    description="Animates a layer"
}

Parameters = {
    layerNumber={label="Layer Number", type="int", min=0, max=10, default=0},
    speed={label="Animation Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function Main()
    --Parameters.layerNumber.max = layers.count
    angle = app.time * speed
    position = {
        x = math.sin(angle) * radius,
        y = math.cos(angle) * radius,
        z = 0
    }
    rotation = {0, 0, 0}
    --if layerNumber < layer.count then
        layers.setPosition(layerNumber, position)
    --end
end
