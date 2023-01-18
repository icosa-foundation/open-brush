Settings = {
    space="canvas"
}

Widgets = {
    scale={label="Scale", type="float", min=0.01, max=2, default=0.2},
    height={label="Height", type="float", min=0.01, max=20, default=2},
    offset={label="Offset", type="float", min=0, max=10, default=10},
}

function WhileTriggerPressed()
    return {
        position = {
            brush.position.x,
            GetHeight(brush.position.x, brush.position.z),
            brush.position.z
        },
        rotation = {-90, 0, 0}
    };
end

function GetHeight(x, y)
    return Mathf.PerlinNoise(x * scale, y * scale) * height + offset;
end
