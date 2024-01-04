Settings = {
    description="Linear copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=36, default=12},
    distance={label="Distance", type="float", min=0, max=20, default=5},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    if Brush.triggerPressedThisFrame then
        symmetryHueShift.generate(Parameters.copies, initialHsv)
    end

    pointers = Path:New()

    for i = 0, Parameters.copies - 1 do

        t = i / Parameters.copies

        --Shift the extra pointers
        position = Symmetry.brushOffset:Subtract(Parameters.distance * t, 0, 0)
        pointers:Insert(position)
    end
    return pointers
end

function End()
    -- TODO fix Brush.colorHsv = initialHsv
end
