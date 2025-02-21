Settings = {
    description="Like a superellipse but capable of drawing concave shapes as well",
    previewType="quad"
}

Parameters = {
    sym={label="Symmetry", type="int", min=1, max=32, default=4},
    n1={label="n1", type="float", min=0.01, max=5, default=1},
    n2={label="n2", type="float", min=0.01, max=5, default=3.5},
    n3={label="n3", type="float", min=0.01, max=5, default=3.5},
}


function Main()
    if Brush.triggerReleasedThisFrame then
    points = Path:New()
        for i = 0.0, Math.pi * 2, 0.01 do
            angle = Parameters.sym * i / 4.0
            term1 = Math:Pow(Math:Abs(Math:Cos(angle)), Parameters.n2)
            term2 = Math:Pow(Math:Abs(Math:Sin(angle)), Parameters.n3)
            r = Math:Pow(term1 + term2, -1.0 / Parameters.n1)
            x = Math:Cos(i) * r
            y = Math:Sin(i) * r
            position = Vector3:New(x, y, 0)
            rotation = Rotation:New(0, 0, angle * 180)
            points:Insert(Transform:New(position, rotation))
        end
        return points
    end
end
