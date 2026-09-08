Settings = {
    description="Control your pointer with multiple waveforms"
}

waveformTypes = {
    "Linear",
    "Cosine",
    "Triangle",
    "Sawtooth",
    "Square",
    "Pulse 80%",
    "Pulse 20%",
    "Exponent",
    "Power 2",
    "Power 0.5",
    "Parabolic",
    "Exponential Sawtooth",
    "Perlin Noise",
    "White Noise",
    "Brown Noise",
    "Blue Noise",
}

Parameters = {
    xWave={label="X Waveform", type="list", items=waveformTypes, default="Linear"},
    xFrequency={label="X Frequency", type="float", min=0.01, max=4, default=1},
    yWave={label="Y Waveform", type="list", items=waveformTypes, default="Cosine"},
    yFrequency={label="Y Frequency", type="float", min=0.01, max=4, default=1},
    yPhase={label="Y Phase", type="float", min=0.01, max=1, default=0.5},
    scale={label="Scale", type="float", min=0.01, max=2, default=1},
}

function Main()

    if Brush.triggerPressedThisFrame then

        Brush:ForcePaintingOff(false)
        xPrevious = 0
        yPrevious = 0

    elseif Brush.triggerIsPressed then

        local t = Brush.timeSincePressed;
        x = sampleWave(Parameters.xWave, t, Parameters.xFrequency, xPrevious)
        y = sampleWave(Parameters.yWave, t + (Parameters.yPhase * 0.5), Parameters.yFrequency, yPrevious)
        xPrevious = x
        yPrevious = y
        position = Vector3:New(-x * Parameters.scale, y * Parameters.scale, 0)
        return Transform:New(position)

    end

end

function sampleWave(waveType, time, frequency, previous)
    if waveType=="Linear" then
        return time * frequency
    elseif waveType=="Cosine" then
        return Waveform:Cosine(time, frequency)
    elseif waveType=="Triangle" then
        return Waveform:Triangle(time, frequency)
    elseif waveType=="Sawtooth" then
        return Waveform:Sawtooth(time, frequency)
    elseif waveType=="Square" then
        return Waveform:Square(time, frequency)
    elseif waveType=="Pulse 80%" then
        return Waveform:Pulse(time, frequency, 0.8)
    elseif waveType=="Pulse 20%" then
        return Waveform:Pulse(time, frequency, 0.2)
    elseif waveType=="Exponent" then
        return Waveform:Exponent(time, frequency)
    elseif waveType=="Power 2" then
        return Waveform:Power(time, frequency, 2)
    elseif waveType=="Power 0.5" then
        return Waveform:Power(time, frequency, 0.5)
    elseif waveType=="Parabolic" then
        return Waveform:Parabolic(time, frequency)
    elseif waveType=="Exponential Sawtooth" then
        return Waveform:ExponentialSawtooth(time, frequency, 2.0)
    elseif waveType=="Perlin Noise" then
        return Waveform:PerlinNoise(time, frequency)
    elseif waveType=="White Noise" then
        return Waveform:WhiteNoise()
    elseif waveType=="Brown Noise" then
        return Waveform:BrownNoise(previous)
    elseif waveType=="Blue Noise" then
        return Waveform:BlueNoise(previous)
    else
        return 0
    end
end
