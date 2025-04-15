Settings = {
    description="The brush stroke wanders off in random directions while you hold the trigger",
    space="canvas"
}

Parameters = {
    speed={label="Speed", type="float", min=0.01, max=1, default=.25},
    framesPerPath={label="Frames Per Path", type="int", min=1, max=1000, default=200},
}

function Main()

    if Brush.triggerPressedThisFrame then

        -- Reset the path when the user starts painting
        frameCount = 0
        currentPos = Brush.position
        return Transform:New(currentPos, Brush.rotation)

    elseif Brush.triggerIsPressed then

        if frameCount == Parameters.framesPerPath then
            Brush:ForceNewStroke()
        end

        frameCount = frameCount + 1

        -- Reset the path when we reach the limit
        if frameCount > Parameters.framesPerPath then
            frameCount = 0
            currentPos = Brush.position
        end

        -- Wandering path based on a noise field
        currentPos = currentPos +
            Vector3:New(
                Parameters.speed * (-0.5 + Math:PerlinNoise(currentPos.y, currentPos.z)),
                Parameters.speed * (-0.5 + Math:PerlinNoise(currentPos.x, currentPos.z)),
                Parameters.speed * (-0.5 + Math:PerlinNoise(currentPos.x, currentPos.y))
            )

        return Transform:New(currentPos, Brush.rotation)

    end

end
