Settings = {
    description="Control your pointer with multiple waveforms"
}

Parameters = {
    xWave={label="X Waveform Type", type="int", min=0, max=15, default=0},
    xFrequency={label="X Frequency", type="float", min=0.01, max=4, default=1},
    yWave={label="Y Waveform Type", type="int", min=0, max=15, default=1},
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
    if waveType==0 then
        return time * frequency
    elseif waveType==1 then
        return Waveform:Cosine(time, frequency)
    elseif waveType==2 then
        return Waveform:Triangle(time, frequency)
    elseif waveType==3 then
        return Waveform:Sawtooth(time, frequency)
    elseif waveType==4 then
        return Waveform:Square(time, frequency)
    elseif waveType==5 then
        return Waveform:Pulse(time, frequency, 0.8)
    elseif waveType==6 then
        return Waveform:Pulse(time, frequency, 0.2)
    elseif waveType==7 then
        return Waveform:Exponent(time, frequency)
    elseif waveType==8 then
        return Waveform:Power(time, frequency, 2)
    elseif waveType==9 then
        return Waveform:Power(time, frequency, 0.5)
    elseif waveType==10 then
        return Waveform:Parabolic(time, frequency)
    elseif waveType==11 then
        return Waveform:ExponentialSawtooth(time, frequency, 2.0)
    elseif waveType==12 then
        return Waveform:PerlinNoise(time, frequency)
    elseif waveType==13 then
        return Waveform:WhiteNoise()
    elseif waveType==14 then
        return Waveform:BrownNoise(previous)
    elseif waveType==15 then
        return Waveform:BlueNoise(previous)
    else
        return 0
    end
end
