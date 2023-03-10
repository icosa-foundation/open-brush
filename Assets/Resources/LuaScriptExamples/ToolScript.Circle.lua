Settings = {
    description="Draws a circle",
    previewType="quad"
}

function OnTriggerReleased()
    points = {}
    for i = 0, 360, 10 do
        angle = i * math.pi / 180
        position = { math.cos(angle), math.sin(angle), 0}
        rotation = { 0, 0, angle * 180 }
        table.insert(points, { position, rotation })
    end
    return points
end

