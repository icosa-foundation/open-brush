Settings = {
    description="The brush oscillates between your left and right hand positions",
    space="canvas"
}


Parameters = {
    freq={label="Frequency", type="float", min=0, max=50, default=40},
    amp={label="Amplitude", type="float", min=0, max=5, default=0.5},
}

function Main()

    mix = (Math:Sin(App.time * freq) + 1) * amp

    position = Vector3.LerpUnclamped(Brush.position, Wand.position, mix)
    rotation = Rotation:Lerp(Brush.rotation, Wand.rotation, mix)

    return Transform:New(position, rotation)

end
