Settings = {
    previewType="quad"
}

Widgets = {
    sym={label="Symmetry", type="int", min=1, max=32, default=4},
    n1={label="n1", type="float", min=0.01, max=5, default=3.5},
    n2={label="n2", type="float", min=0.01, max=5, default=2.5},
    n3={label="n3", type="float", min=0.01, max=5, default=1.5},
}

function sign(number)
    return number > 0 and 1 or (number == 0 and 0 or -1)
end

function OnTriggerReleased()
    points = {}
    for i = 0.0, math.pi * 2, 0.01 do
        angle = sym * i / 4.0
        term1 = math.pow(math.abs(math.cos(angle)), n2)
        term2 = math.pow(math.abs(math.sin(angle)), n3)
        r = math.pow(term1 + term2, -1.0 / n1)
        x = math.cos(i) * r
        y = math.sin(i) * r
        position = { x, y, 0}
        rotation = { 0, 0, angle * 180}
        table.insert(points, { position, rotation })
    end
    return points
end
