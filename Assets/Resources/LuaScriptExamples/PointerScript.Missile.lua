Settings = {
    space="canvas"
}

Widgets = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
}

function OnTriggerPressed()
    currentPos = brush.position
    currentRotation = brush.rotation
    return {currentPos, currentRotation}
end

function WhileTriggerPressed()

    currentPos = {
        x = currentPos.x + (speed * -brush.direction.x),
        y = currentPos.y + (speed * -brush.direction.y),
        z = currentPos.z + (speed * -brush.direction.z),
    }
    return {currentPos, currentRotation}

end
