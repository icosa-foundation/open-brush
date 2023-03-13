Parameters = {
    speedY={label="Speed Y", type="float", min=0, max=2000, default=500},
    speedZ={label="Speed Z", type="float", min=0, max=2000, default=0},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetry.rotation = {0, 0, 0}
        symmetry.spin({0, speedY, speedZ})
    end

    if speedY > speedZ then
        return {
            {symmetry.position, rotation={0, symmetry.rotation.y, 0}},
        }
    else
        return {
            {symmetry.position, rotation={0, 0, symmetry.rotation.z}},
        }
    end
end
