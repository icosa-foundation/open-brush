Settings = {
    description="Rotates a layer around it's center"
}

Parameters = {
    layerNumber={label="Layer Number", type="int", min=0, max=10, default=1},
    speedX={label="X Rotation Speed", type="int", min=-10, max=10, default=0},
    speedY={label="Y Rotation Speed", type="int", min=-10, max=10, default=0},
    speedZ={label="Z Rotation Speed", type="int", min=-10, max=10, default=2},
}

function Start()
    originalRotation = layers.getRotation(layerNumber)
    angle = {x = 0, y = 0, z = 0}
    layers.centerPivot(layerNumber)
    -- layers.showPivot(layerNumber)
end

function Main()
    angle = {
        x = angle.x + speedX,
        y = angle.y + speedY,
        z = angle.z + speedZ
    }
    rotation = angle
    layers.setRotation(layerNumber, rotation)
end

function End()
    layers.setRotation(layerNumber, originalRotation)
end
