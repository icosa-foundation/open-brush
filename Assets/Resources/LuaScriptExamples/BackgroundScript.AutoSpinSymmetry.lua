Settings = {
    description="Precise control over the symmetry spin rate"
}

Parameters = {
    speedX={label="Speed X", type="float", min=0, max=600, default=0},
    speedY={label="Speed Y", type="float", min=0, max=600, default=150},
    speedZ={label="Speed Z", type="float", min=0, max=600, default=0},
}

function Main()
    symmetry.spin({speedX, speedY, speedZ})
end

function End()
    symmetry.spin({0, 0, 0})
    symmetry.rotation = {0, 0, 0}
end
