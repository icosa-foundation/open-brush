Settings = {
    description="Like spinning the mirror by hand but with precise control"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=3000, default=1000},
    angleX={label="Angle X", type="float", min=-180, max=180, default=0},
    angleZ={label="Angle Z", type="float", min=-180, max=180, default=0},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetry.rotation = {angleX, 0, angleZ}
        symmetry.spin({0, speed, 0})
    end

    return {
        {symmetry.position, rotation=symmetry.rotation}
    }
end
