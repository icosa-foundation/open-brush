Settings = {
    description="Autolathe but with multiple lathes"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=3000, default=200},
    angleX={label="Angle X", type="float", min=-180, max=180, default=0},
    angleZ={label="Angle Z", type="float", min=-180, max=180, default=0},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        brush.forceNewStroke()
        symmetry.rotation = {angleX, 0, angleZ}
        symmetry.spin({0, speed, 0})
    end

    return {
        {
            position = {-symmetry.brushOffset.x, symmetry.brushOffset.y, symmetry.brushOffset.z},
        },
        {
            position = {-symmetry.brushOffset.x, -symmetry.brushOffset.y, symmetry.brushOffset.z},
        },
    }
end
