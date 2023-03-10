Settings = {
    description="Like spinning the mirror by hand but with precise control"
}

Widgets = {
    speed={label="Speed", type="float", min=0, max=2000, default=1000},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetry.spin({0, speed, 0})
    end

    return {
        {symmetry.position, rotation={0, symmetry.rotation.y, 0}}
    }
end
