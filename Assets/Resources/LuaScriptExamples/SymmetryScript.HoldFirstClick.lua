Settings = {
    description="Like Many Around except the widget always moves to where you start drawing"
}

Parameters = {
    copies={label="Number of copies", type="int", min=0, max=36, default=4},
}

function Main()

    if not Brush.triggerIsPressed then
        Symmetry.current.transform = Transform:New(Brush.position, Brush.rotation)
    end

    pointers = Path:New()
    theta = 360.0 / Parameters.copies

    for i = 0, Parameters.copies - 1 do
        pointers:Insert(Transform:New(Symmetry.brushOffset, Rotation:New(0, i * theta, 0)))
    end

    return pointers
end
