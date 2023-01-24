Settings = {
    space="canvas"
}

function OnTriggerPressed()
    initialPos = {
        brush.position.x,
        brush.position.y,
        brush.position.z,
    }
    currentPos = initialPos
end

function WhileTriggerPressed()

    if app.frames % 4 == 0 then
        currentPos = {
            brush.position.x,
            brush.position.y,
            brush.position.z,
        }
        return {
            {position=initialPos, rotation=brush.rotation},
            {position=currentPos, rotation=brush.rotation},
        }
    end
end
