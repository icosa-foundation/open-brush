Settings = {
    description="Animates a layer"
}

function Main()
    x = Math:Lerp(0, 1, App.time % 1)
    Sketch.layers.active.position = Vector3:New(x, 0, 0)
end
