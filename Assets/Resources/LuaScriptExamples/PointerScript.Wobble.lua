Settings = {
    description="Like Wiggle but uses a smooth noise function"
}

Parameters = {
    positionAmount={label="Position Amount", type="float", min=0.01, max=2, default=1},
    rotationAmount={label="Rotation Amount", type="float", min=0.01, max=360, default=0},
    frequency={label="Frequency", type="float", min=0.01, max=4, default=1}
}

function Main()

    -- Shorthand parameters for convenience
    local f = Parameters.frequency
    local p = Parameters.positionAmount
    local r = Parameters.rotationAmount

    noiseX = -0.5 + Math:PerlinNoise(f * Brush.position.x - 100, f * Brush.position.z)
    noiseY = -0.5 + Math:PerlinNoise(f * Brush.position.x, f * Brush.position.z)
    noiseZ = -0.5 + Math:PerlinNoise(f * Brush.position.x + 100, f * Brush.position.z)

    position = Vector3:New(noiseX * p, noiseY * p, noiseZ * p)
    rotation = Rotation:New(noiseX * r, noiseY * r, noiseZ * r)

    return Transform:New(position, rotation)

end
