Settings = {
    description="Like spinning the mirror by hand but with precise control"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=3000, default=200},
    angleX={label="Angle X", type="float", min=-180, max=180, default=0},
    angleZ={label="Angle Z", type="float", min=-180, max=180, default=0},
}

function Main()

    if Brush.triggerPressedThisFrame then
        Brush:ForceNewStroke()
        Symmetry.rotation = Rotation:New(angleX, 0, angleZ)
        Symmetry:Spin(0, speed, 0)
    end

    local position = Transform:New(Symmetry.brushOffset:ScaleBy(-1, 1, 1))
    return Path:New({position})
end
