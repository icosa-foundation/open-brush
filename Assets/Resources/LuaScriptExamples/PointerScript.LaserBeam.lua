Settings = {
    space="canvas"
}

Widgets = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
}

function OnTriggerPressed()

    --Store the brush transform at the point we press the trigger
    direction = brush.direction
    currentPos = brush.position
    currentRotation = brush.rotation

    return {currentPos, currentRotation}
end

function WhileTriggerPressed()

    -- Move the pointer in the direction we were facing when we pressed the trigger
    currentPos = {
        x = currentPos.x + (speed * -direction.x),
        y = currentPos.y + (speed * -direction.y),
        z = currentPos.z + (speed * -direction.z),
    }

    return {currentPos, currentRotation}

end
