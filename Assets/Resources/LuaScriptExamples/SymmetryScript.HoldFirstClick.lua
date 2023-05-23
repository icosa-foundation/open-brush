Settings = {
    description="Like Many Around except the widget always moves to where you start drawing"
}

Parameters = {
    copies={label="Number of copies", type="int", min=0, max=36, default=4},
}

function Main()

    if Brush.triggerIsPressedThisFrame then
        Symmetry.transform = Transform:New(Brush.position, Brush.rotation)
    end

    -- Don't allow painting immediately otherwise you get stray lines
    Brush:ForcePaintingOff(Brush.triggerIsPressedThisFrame)

    pointers = Path:New()
    theta = 360.0 / copies

    for i = 0, copies - 1 do
        pointers:Insert(Transform:New(Symmetry.brushOffset, Rotation:New(0, i * theta, 0)))
    end

    return pointers
end
