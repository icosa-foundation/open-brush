Settings = {
    description="Control your pointer with multiple waveforms"
}

Parameters = {
    xWave={label="X Waveform Type", type="int", min=0, max=15, default=0},
    xFrequency={label="X Frequency", type="float", min=0.01, max=4, default=1},
    yWave={label="Y Waveform Type", type="int", min=0, max=15, default=1},
    yFrequency={label="Y Frequency", type="float", min=0.01, max=4, default=1},
    yPhase={label="Y Phase", type="float", min=0.01, max=1, default=0.5},
    radius={label="Radius", type="float", min=0.01, max=2, default=1},
}

function OnTriggerPressed()
    brush.forcePaintingOff(false)
    xPrevious = 0
    yPrevious = 0
    return Calc(0)
end

function WhileTriggerPressed()
    return Calc(brush.timeSincePressed)
end

function Calc(t)

    x = sampleWave(xWave, t, xFrequency, xPrevious)
    xPrevious = x
    y = sampleWave(yWave, t + (yPhase * 0.5), yFrequency, yPrevious)
    yPrevious = y

    position = {
        x = -x * radius,
        y = y * radius,
        z = 0
    }
    return {position, {0,0,0}}
end

function sampleWave(waveType, time, frequency, previous)
    if waveType==0 then
        return time * frequency
    elseif waveType==1 then
        return waveform.cosine(time, frequency)
    elseif waveType==2 then
        return waveform.triangle(time, frequency)
    elseif waveType==3 then
        return waveform.sawtooth(time, frequency)
    elseif waveType==4 then
        return waveform.square(time, frequency)
    elseif waveType==5 then
        return waveform.pulse(time, frequency, 0.8)
    elseif waveType==6 then
        return waveform.pulse(time, frequency, 0.2)
    elseif waveType==7 then
        return waveform.exponent(time, frequency)
    elseif waveType==8 then
        return waveform.power(time, frequency, 2)
    elseif waveType==9 then
        return waveform.power(time, frequency, 0.5)
    elseif waveType==10 then
        return waveform.parabolic(time, frequency)
    elseif waveType==11 then
        return waveform.exponentialSawtooth(time, frequency, 2.0)
    elseif waveType==12 then
        return waveform.perlinNoise(time, frequency)
    elseif waveType==13 then
        return waveform.whiteNoise()
    elseif waveType==14 then
        return waveform.brownNoise(previous)
    elseif waveType==15 then
        return waveform.blueNoise(previous)
    else
        return 0
    end
end
