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
        Symmetry.current.rotation = Rotation:New(Parameters.angleX, 0, Parameters.angleZ)
        Symmetry.current.spin = Vector3:New(0, Parameters.speed, 0)
    end

    local position = Transform:New(Symmetry.brushOffset:ScaleBy(-1, 1, 1))
    return Path:New({position})
end
