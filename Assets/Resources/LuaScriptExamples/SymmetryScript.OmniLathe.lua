Settings = {
    description="Like spinning the mirror by hand but with precise control"
}

Parameters = {
    speedY={label="Speed Y", type="float", min=0, max=2000, default=200},
    speedZ={label="Speed Z", type="float", min=0, max=2000, default=100},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        brush.forceNewStroke()
        symmetry.rotation = {0, 0, 0}
        symmetry.spin({0, speedY, speedZ})
    end

    return {
        { position = { -symmetry.brushOffset.x, symmetry.brushOffset.y, symmetry.brushOffset.z } },
    }
end
