Settings = {
    description="Like spinning the mirror by hand but with precise control"
}

Parameters = {
    speedY={label="Speed Y", type="float", min=0, max=2000, default=200},
    speedZ={label="Speed Z", type="float", min=0, max=2000, default=100},
}

function Main()

    if Brush.triggerIsPressedThisFrame then
        Brush:ForceNewStroke()
        Symmetry.rotation = Rotation.zero
        Symmetry:Spin(0, speedY, speedZ)
    end

    position = Symmetry.brushOffset:Scale(-1, 1, 1)
    return Path:New({Transform:New(position)})
end
