Settings = {
    previewType="quad"
}

function OnTriggerReleased()
    points = {}
    for i = 0, 360, 10 do
        angle = i * math.pi / 180
        pos = {math.cos(angle), math.sin(angle), 0}
        rot = { 0, 0, angle * 180 }
        table.insert(points, {pos, rot})
    end
    return points
end

