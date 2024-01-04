Settings = {
    description="Like Many Around except the widget always moves to where you start drawing"
}

Parameters = {
    copies={label="Number of copies", type="int", min=0, max=36, default=4},
}

function Main()

    if Brush.triggerPressedThisFrame then
        Symmetry.current.position = Brush.position
        Symmetry.current.rotation = Brush.rotation
    end

    -- Don't allow painting immediately otherwise you get stray lines
    Brush:ForcePaintingOff(Brush.triggerPressedThisFrame)

    pointers = Path:New()
    theta = 360.0 / Parameters.copies

    for i = 0, Parameters.copies - 1 do
        pointers:Insert(Transform:New(Symmetry.brushOffset, Rotation:New(0, i * theta, 0)))
    end

    return pointers
end
