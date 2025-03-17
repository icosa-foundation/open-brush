Settings = {
    description="Multiple copies of your brush spaced between your left and right hand positions",
}

Parameters = {copies={label="Copies", type="int", min=1, max=32, default=12}}

function Start()
    Symmetry:ClearColors()
    Symmetry:AddColor(Color.red)
    Symmetry:AddColor(Color.green)
end

function Main()
    pointers = Path:New()
    for i = 0.0, Parameters.copies do
        position = Vector3:Lerp(Symmetry.brushOffset, Symmetry.wandOffset, i/Parameters.copies)
        pointers:Insert(position)
    end
    return pointers
end
