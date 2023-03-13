Settings = {
    description="Draws lines from the position you start drawing to your current position",
    space="canvas"
}
Parameters = {spacing={label="Spacing", type="int", min=1, max=30, default=4}}

function OnTriggerPressed()
    initialPos = {
        brush.position.x,
        brush.position.y,
        brush.position.z,
    }
    currentPos = initialPos
end

function WhileTriggerPressed()

    --Only draw every n frames where n="spacing"

    if app.frames % spacing == 0 then
        currentPos = {
            brush.position.x,
            brush.position.y,
            brush.position.z,
        }

        --A line from the start position to the current position
        return {
            {position=initialPos, rotation=brush.rotation},
            {position=currentPos, rotation=brush.rotation},
        }
    end
end
