Settings = {
    description="Linear copies of your stroke with optional color shifts"
}

Parameters = {
    copies={label="Number of copies", type="int", min=1, max=36, default=12},
    distance={label="Distance", type="float", min=0, max=20, default=5},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = brush.colorHsv
end

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetryHueShift.generate(copies, initialHsv)
    end

    pointers = {}

    for i = 0, copies - 1 do

        t = i / copies

        --Shift the extra pointers
        pos = {{
           symmetry.brushOffset.x - distance * t,
           symmetry.brushOffset.y,
           symmetry.brushOffset.z
       }}
        table.insert(pointers, pos)
    end
    return pointers
end

function End()
    -- TODO fix brush.colorHsv = initialHsv
end
