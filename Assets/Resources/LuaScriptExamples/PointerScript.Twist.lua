Settings = {
    description="Rotates your strokes without needing to dislocate your wrist"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=600, default=300},
}

function Main()

    --We only want to change the pointer orientation
    return Transform:New(
            Vector3.zero,
            Rotation:New(0, 0, App.time * speed)
    )

end
