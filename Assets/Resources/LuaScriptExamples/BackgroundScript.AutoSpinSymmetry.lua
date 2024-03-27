Settings = {
    description="Precise control over the symmetry spin rate"
}

Parameters = {
    speedX={label="Speed X", type="float", min=0, max=600, default=0},
    speedY={label="Speed Y", type="float", min=0, max=600, default=150},
    speedZ={label="Speed Z", type="float", min=0, max=600, default=0},
}

function Main()
    Symmetry.current.spin = Vector3:New(Parameters.speedX, Parameters.speedY, Parameters.speedZ)
end

function End()
    Symmetry.current.spin = Vector3:New(0, 0, 0)
    Symmetry.current.rotation = Rotation.zero
end
