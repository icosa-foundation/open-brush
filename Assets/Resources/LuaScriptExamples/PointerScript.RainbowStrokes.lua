Settings = {
    description=""
}

Parameters = {
    rate={label="Rate", type="int", min=0, max=100, default=30},
    hueShiftFrequency={label="Hue Shift Frequency", type="float", min=0.1, max=6, default=1},
    hueShiftAmount={label="Hue Shift Amount", type="float", min=0, max=1, default=.5}
}

function Start()
    initialHsv = brush.colorHsv
end

function WhileTriggerPressed()
    if app.frames % rate == 0 then
        newHue = waveform.triangle(app.time, hueShiftFrequency) * hueShiftAmount
        newColor = unityColor.hsvToRgb(initialHsv.x + newHue, initialHsv.y, initialHsv.z)
        brush.colorRgb = newColor
        brush.forceNewStroke()
    end
end
