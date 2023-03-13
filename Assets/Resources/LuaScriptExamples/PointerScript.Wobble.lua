Settings = {
    description="Like Wiggle but uses a smooth noise function"
}

Parameters = {
    positionAmount={label="Position Amount", type="float", min=0.01, max=2, default=1},
    rotationAmount={label="Rotation Amount", type="float", min=0.01, max=360, default=0},
    frequency={label="Frequency", type="float", min=0.01, max=4, default=1}
}

function WhileTriggerPressed()
    noiseX = -0.5 + unityMathf.perlinNoise(frequency * brush.position.x - 100, frequency * brush.position.z)
    noiseY = -0.5 + unityMathf.perlinNoise(frequency * brush.position.x, frequency * brush.position.z)
    noiseZ = -0.5 + unityMathf.perlinNoise(frequency * brush.position.x + 100, frequency * brush.position.z)
    return {
        position = {noiseX * positionAmount, noiseY * positionAmount, noiseZ * positionAmount},
        rotation =  {noiseX * rotationAmount, noiseY * rotationAmount, noiseZ * rotationAmount}
    };
end
