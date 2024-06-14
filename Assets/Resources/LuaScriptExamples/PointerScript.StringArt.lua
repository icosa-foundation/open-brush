Settings = {
    description="As you draw, extra lines are added from the start of your stroke to the current position"
}

Parameters = {
    rate={label="Rate", type="int", min=1, max=10, default=10},
}

function Main()

    if Brush.triggerPressedThisFrame then

        initialPos = Brush.position
        currentPos = initialPos

    elseif Brush.triggerIsPressed then

        currentPos = Brush.position

        -- Only draw every N frames
        if App.frames % Parameters.rate == 0 then
            path = Path:New({
                Transform:New(initialPos, Brush.rotation),
                Transform:New(currentPos, Brush.rotation),
            })
            path:SampleByDistance(0.1)
            path:Draw()
        end

    end

end
