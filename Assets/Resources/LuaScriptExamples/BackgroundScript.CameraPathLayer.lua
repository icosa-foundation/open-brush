Settings = {
    description="Simple layer animation example"
}

Parameters = {
    layerNumber={label="Layer Number", type="int", min=0, max=10, default=1},
    speed={label="Animation Speed", type="float", min=0.01, max=10, default=1},
}

function Main()
    transform = camerapath.sample(app.time * speed, true, true)
    --layers.setTransform(layerNumber, transform)
    layers.setPosition(layerNumber, transform.position)
end
