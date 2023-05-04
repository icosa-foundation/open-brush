Settings = {
    description="Draws lines from the position you start drawing to your current position",
    space="canvas"
}
Parameters = {spacing={label="Spacing", type="int", min=1, max=30, default=4}}

function OnTriggerPressed()
    initialPos = Brush.position
    currentPos = initialPos
end

function WhileTriggerPressed()

    --Only draw every n frames where n="spacing"

    if App.frames % spacing == 0 then

        currentPos = Brush.position

        --A line from the start position to the current position
        return Path:New({
            Transform:New(initialPos, Brush.rotation),
            Transform:New(currentPos, Brush.rotation),
        })
    end
end
