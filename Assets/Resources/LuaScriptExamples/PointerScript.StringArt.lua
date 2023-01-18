function WhileTriggerPressed()

    if brush.triggerIsPressedThisFrame then
        initialPos = {
            brush.position.x,
            brush.position.y,
            brush.position.z,
        }
    end

    if math.random() > .9 and brush.triggerIsPressed then
        currentPos = {
            brush.position.x,
            brush.position.y,
            brush.position.z,
        }
        draw.path({initialPos, currentPos})
    end

    --Do nothing to the actual pointer
    pos = {0, 0, 0}
    rot = {0, 0, 0}
    return {pos, rot}
end
