Settings = {
    description="Rotates a layer around it's center"
}

Parameters = {
    layerNumber={label="Layer Number", type="int", min=0, max=10, default=1},
    speedX={label="X Rotation Speed", type="float", min=0.01, max=20, default=0},
    speedY={label="Y Rotation Speed", type="float", min=0.01, max=20, default=0},
    speedZ={label="Z Rotation Speed", type="float", min=0.01, max=20, default=6},
    radius={label="Radius", type="float", min=0.01, max=5, default=1},
}

function Start()
    layers.centerPivot(layerNumber)
    angle = {x = 0, y = 0, z = 0}
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
