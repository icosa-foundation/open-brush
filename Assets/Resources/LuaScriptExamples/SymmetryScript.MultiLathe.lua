Settings = {
    description="Autolathe but with multiple lathes"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=3000, default=200},
    angleX={label="Angle X", type="float", min=-180, max=180, default=0},
    angleZ={label="Angle Z", type="float", min=-180, max=180, default=0},
}

function Main()

    if Brush.triggerIsPressedThisFrame then
        Brush:ForceNewStroke()
        Symmetry.rotation = Rotation:New(angleX, 0, angleZ)
        Symmetry:Spin(0, speed, 0)
    end

    return Path:New({
        Symmetry.brushOffset:Scale(-1, 1, 1),
        Symmetry.brushOffset:Scale(-1, -1, 1)
    })
end
