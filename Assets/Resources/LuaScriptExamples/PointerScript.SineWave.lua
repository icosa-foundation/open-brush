Widgets = {
    frequency={label="Frequency", type="float", min=0.1, max=2, default=1},
    amplitude={label="Amplitude", type="float", min=0.1, max=1, default=0.5},
}

function WhileTriggerPressed()
    pos = {0, waveform.sine(brush.distanceMoved, frequency) * amplitude * 0.2, 0}
    rot = brush.rotation
    return {pos, rot}
end
