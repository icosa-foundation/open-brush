Settings = {
    description="Precise control over the symmetry spin rate"
}

Parameters = {
    speedX={label="Speed X", type="float", min=0, max=600, default=0},
    speedY={label="Speed Y", type="float", min=0, max=600, default=150},
    speedZ={label="Speed Z", type="float", min=0, max=600, default=0},
}

function Main()
    Symmetry:Spin(speedX, speedY, speedZ)
end

function End()
    Symmetry:Spin(0, 0, 0)
    Symmetry.rotation = Rotation.zero
end
