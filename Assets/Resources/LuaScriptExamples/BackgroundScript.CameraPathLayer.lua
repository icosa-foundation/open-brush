Settings = {
    description="Simple layer animation example"
}

Parameters = {
    speed={label="Animation Speed", type="float", min=0.01, max=10, default=1},
}

function Start()
    layer = Layers.active
    cameraPath = CameraPath.active
end

function Main()
    transform = cameraPath:Sample(App.time * speed, true, true)
    layer.position = transform.position
end
