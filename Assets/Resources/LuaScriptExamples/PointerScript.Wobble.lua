Settings = {
    description="Like Wiggle but uses a smooth noise function"
}

Parameters = {
    positionAmount={label="Position Amount", type="float", min=0.01, max=2, default=1},
    rotationAmount={label="Rotation Amount", type="float", min=0.01, max=360, default=0},
    frequency={label="Frequency", type="float", min=0.01, max=4, default=1}
}

function WhileTriggerPressed()
    noiseX = -0.5 + Math.perlinNoise(frequency * Brush.position.x - 100, frequency * Brush.position.z)
    noiseY = -0.5 + Math.perlinNoise(frequency * Brush.position.x, frequency * Brush.position.z)
    noiseZ = -0.5 + Math.perlinNoise(frequency * Brush.position.x + 100, frequency * Brush.position.z)
    position = Vector3:New(noiseX, noiseY, noiseZ):ScaleBy(positionAmount)
    rotation = Rotation:New(noiseX, noiseY, noiseZ):ScaleBy(rotationAmount)
    return Transform:New(position, rotation)
end
