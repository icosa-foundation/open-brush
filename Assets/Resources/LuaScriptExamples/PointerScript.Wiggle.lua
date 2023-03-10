Settings = {
    description="Randomizes the brush position"
}

Widgets = {
    amount={label="Wiggle Amount", type="float", min=0, max=1, default=0.5},
}

function WhileTriggerPressed()
    return {{
        (-0.5 + math.random()) * amount / 5.0,
        (-0.5 + math.random()) * amount / 5.0,
        (-0.5 + math.random()) * amount / 5.0
    }};
end
