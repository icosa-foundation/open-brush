Settings = {
    description="Like Laser Beam except you can steer the line while holding the trigger",
    space="canvas"
}


Widgets = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.1},
}

function OnTriggerPressed()

    --Store the brush transform at the point we press the trigger
    currentPos = brush.position
    currentRotation = brush.rotation
    return {currentPos, currentRotation}
end

function WhileTriggerPressed()

    --Similar to the LaserBeam PointerScript except we can change the direction during "flight"
    currentPos = {
        x = currentPos.x + (speed * -brush.direction.x),
        y = currentPos.y + (speed * -brush.direction.y),
        z = currentPos.z + (speed * -brush.direction.z),
    }
    return {currentPos, currentRotation}

end
