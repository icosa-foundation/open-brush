Settings = {
    description="Like Many Around except the widget always moves to where you start drawing"
}

Widgets = {
    copies={label="Number of copies", type="int", min=0, max=36, default=4},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetry.setTransform(brush.position, brush.rotation)
    end

    -- Don't allow painting immediately otherwise you get stray lines
    brush.forcePaintingOff(brush.triggerIsPressedThisFrame)

    pointers = {}
    theta = 360.0 / copies

    for i = 0, copies - 1 do
        table.insert(pointers, {position={0, 0, 0}, rotation={0, i * theta, 0}})
    end

    return pointers
end
