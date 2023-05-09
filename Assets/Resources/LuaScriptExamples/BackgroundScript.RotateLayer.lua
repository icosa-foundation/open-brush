Settings = {
    description="Rotates a layer around it's center"
}

Parameters = {
    speedX={label="X Rotation Speed", type="int", min=-10, max=10, default=0},
    speedY={label="Y Rotation Speed", type="int", min=-10, max=10, default=0},
    speedZ={label="Z Rotation Speed", type="int", min=-10, max=10, default=2},
}

function Start()
    originalRotation = layer:GetRotation(layerNumber)
    rotation = Rotation.zero
    layer = Sketch.layers.active
    layer:CenterPivot(layerNumber)
    layer:ShowPivot(layerNumber)
end

function Main()
    rotation = Rotation:New(
            rotation.x + speedX,
            rotation.y + speedY,
            rotation.z + speedZ
    )
    layer.rotation = rotation
end

function End()
    layer.rotation = originalRotation
end
