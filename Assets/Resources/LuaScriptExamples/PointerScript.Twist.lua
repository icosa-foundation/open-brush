Settings = {
    description="Rotates your strokes without needing to dislocate your wrist"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=600, default=300},
}

function WhileTriggerPressed()
    --We only want to change the pointer orientation
    return {rotation={0, 0, app.time * speed}};
end
