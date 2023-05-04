Settings = {
    description="Animates a layer"
}

Parameters = {
    speed={label="Animation Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function Main()
    angle = App.time * speed
    x = Math:Lerp(0, 1, App.time % 1)
    Layers.active.position = Vector3:New(x, 0, 0)
end
