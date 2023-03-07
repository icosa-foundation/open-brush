Widgets = {
    speed={label="Speed", type="float", min=0, max=2000, default=1000},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetry.spin({0, speed, 0})
    end

    return {
        {
            position=symmetry.position,
            rotation={0, symmetry.rotation.y, 0}
        }
    }
end
