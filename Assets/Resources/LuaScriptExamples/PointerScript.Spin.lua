Widgets = {
    speed={label="Speed", type="float", min=0, max=600, default=300},
}

function WhileTriggerPressed()
    return {rotation={0, 0, app.time * speed}};
end
