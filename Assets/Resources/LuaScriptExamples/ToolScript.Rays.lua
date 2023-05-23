Settings = {
    description="Draws lines from the position you start drawing to your current position",
    space="canvas"
}

Parameters = {
    spacing={label="Spacing", type="int", min=1, max=30, default=4}
}

function OnTriggerPressed()
    initialPos = Brush.position
end

function WhileTriggerPressed()

    --Only draw every n frames (where n is the "spacing" parameter)
    if App.frames % spacing == 0 then
        --A line from the start position to the current position
        path = Path:New({
            Transform:New(initialPos, Brush.rotation),
            Transform:New(Brush.position, Brush.rotation),
        })
        path:Resample(0.1) --Add more points as Open Brush will optimize the line away otherwise
        path:Draw() --Draw it manually. Paths are only drawn automatically when returned from OnTriggerReleased
    end
end
