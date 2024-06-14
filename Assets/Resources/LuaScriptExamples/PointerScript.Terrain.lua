Settings = {
    description="Your brush strokes are constrained to a hilly landscape with configurable height and scale",
    space="canvas"
}


Parameters = {
    scale={label="Scale", type="float", min=0.01, max=2, default=0.2},
    height={label="Height", type="float", min=0.01, max=20, default=2},
    offset={label="Offset", type="float", min=0, max=10, default=10},
}

function Main()

    -- Don't allow painting immediately otherwise you get stray lines
    Brush:ForcePaintingOff(Brush.triggerPressedThisFrame)

    return Transform:New(
        Vector3:New(
            Brush.position.x,
            getHeight(Brush.position.x, Brush.position.z),
            Brush.position.z
        ),
        Rotation.down
    )
end

function getHeight(x, y)
    noise = Math:PerlinNoise(x * Parameters.scale, y * Parameters.scale)
    return noise * Parameters.height + Parameters.offset
end
