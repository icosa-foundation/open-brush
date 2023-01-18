Widgets = {
    positionWiggle={label="Position Wiggle", type="float", min=0.01, max=2},
    rotationWiggle={label="Rotation Wiggle", type="float", min=0.01, max=10},
}

function WhileTriggerPressed()
    return {{
        (-0.5 + math.random()) * positionWiggle,
        (-0.5 + math.random()) * positionWiggle,
        (-0.5 + math.random()) * positionWiggle
    },{
        -0.5 + math.random() * rotationWiggle,
        -0.5 + math.random() * rotationWiggle,
        -0.5 + math.random() * rotationWiggle
    }};
end
