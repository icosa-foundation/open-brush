Settings = {
    description="Randomizes the brush position"
}

Parameters = {
    amount={label="Wiggle Amount", type="float", min=0, max=1, default=0.25},
}

function WhileTriggerPressed()
    return Random.onUnitSphere:Multiply(amount * 0.1)
end
