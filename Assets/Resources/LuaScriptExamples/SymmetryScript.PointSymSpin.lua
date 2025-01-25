Settings = {
    description="Point Symmetry Spin", space="widget"
}

Parameters = {
    symType={label="Symmetry Type", type=SymmetryPointType},
    symOrder={label="Symmetry Order", type="int", min=1, max=10, default=6},
    frequency={label="Frequency", type="float", min=0.01, max=10, default=5},
    size={label="Size", type="float", min=1, max=10, default=1},
}

symmetryHueShift = require "symmetryHueShift"

function Start()
    initialHsv = Brush.colorHsv
end

function Main()

    mySymSettings = SymmetrySettings:NewPointSymmetry(Parameters.symType, Parameters.symOrder)
    pointers = mySymSettings.matrices

    if Brush.triggerPressedThisFrame then
        symmetryHueShift.generate(pointers.count, initialHsv)
    end

    -- Rotate each matrix around the origin based on the current time
    tx = Math:Cos(App.time * Parameters.frequency) * Parameters.size
    ty = Math:Sin(App.time * Parameters.frequency) * Parameters.size
    for i = 0, pointers.count - 1 do
        pointers[i] = pointers[i] * Matrix:NewTranslation(Vector3:New(tx, ty, 0))
    end
    return pointers
end

function End()
    -- TODO fix Brush.colorHsv = initialHsv
end
