Settings = {
    description="The brush stroke wanders off in random directions while you hold the trigger",
    space="canvas"
}


Parameters = {
    speed={label="Speed", type="float", min=0.01, max=1, default=.25},
    framesPerPath={label="Frames Per Path", type="int", min=1, max=1000, default=200},
}

function OnTriggerPressed()
    -- Reset the path when the user starts painting
    frameCount = 0
    currentPos = Brush.position
    return Transform:New(currentPos, Brush.rotation)
end

function WhileTriggerPressed()

    if (frameCount == framesPerPath) then
        Brush:ForceNewStroke()
    end

    frameCount = frameCount + 1

    -- Reset the path when we reach the limit
    if (frameCount > framesPerPath) then
        frameCount = 0
        currentPos = Brush.position
    end

    -- Wandering path based on a noise field
    currentPos = currentPos:Add(
        speed * (-0.5 + Math.perlinNoise(currentPos.y, currentPos.z)),
        speed * (-0.5 + Math.perlinNoise(currentPos.x, currentPos.z)),
        speed * (-0.5 + Math.perlinNoise(currentPos.x, currentPos.y))
    )

    return Transform:New(currentPos, Brush.rotation)

end
