Settings = {
    previewType="quad"
}

Widgets = {
    n={label="n", type="float", min=0.01, max=5, default=3.5},
}

function sign(number)
    return number > 0 and 1 or (number == 0 and 0 or -1)
end

function OnTriggerReleased()
    points = {}
    for i = 0, 360, 10 do
        angle = i * math.pi / 180
        x = math.pow(math.abs(math.cos(angle)), 2/ distance) * sign(math.cos(angle))
        y = math.pow(math.abs(math.sin(angle)), 2/ distance) * sign(math.sin(angle))
        position = { x, y, 0}
        rotation = { 0, 0, angle * 180}
        table.insert(points, { position, rotation })
    end
    return points
end
