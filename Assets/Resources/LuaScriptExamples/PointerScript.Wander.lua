Settings = {
    space="canvas"
}

Widgets = {
    speed={label="Speed", type="float", min=0.01, max=2, default=.5},
    framesPerPath={label="Frames Per Path", type="int", min=1, max=1000, default=200},
}

function OnTriggerPressed()
    -- Reset the path when the user starts painting
    frameCount = 0
    currentPos = brush.position
    return {currentPos, brush.rotation}
end

function WhileTriggerPressed()

    -- Ensure we're not painting when we reset the path
    if (frameCount == framesPerPath) then
        brush.forcePaintingOff(true)
    elseif (frameCount == 0) then
        brush.forcePaintingOff(false)
    end

    frameCount = frameCount + 1

    -- Reset the path when we reach the limit
    if (frameCount > framesPerPath) then
        frameCount = 0
        currentPos = brush.position
    end

    -- Wandering path based on a noise field
    currentPos = {
        x = currentPos.x + (speed * (-0.5 + unityMathf.perlinNoise(currentPos.y, currentPos.z))),
        y = currentPos.y + (speed * (-0.5 + unityMathf.perlinNoise(currentPos.x, currentPos.z))),
        z = currentPos.z + (speed * (-0.5 + unityMathf.perlinNoise(currentPos.x, currentPos.y))),
    }

    return {currentPos, brush.rotation}

end
