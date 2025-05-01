Settings = {
    description="Like spinning the mirror by hand but with precise control"
}

Parameters = {
    speedY={label="Speed Y", type="float", min=0, max=2000, default=200},
    speedZ={label="Speed Z", type="float", min=0, max=2000, default=100},
}

function Main()

    if Brush.triggerPressedThisFrame then
        Brush:ForceNewStroke()
        Symmetry.current.rotation = Rotation.zero
        Symmetry.current.spin = Vector3:New(0, Parameters.speedY, Parameters.speedZ)
    end

    position = Symmetry.brushOffset:ScaleBy(-1, 1, 1)
    return Path:New({Transform:New(position)})
end
