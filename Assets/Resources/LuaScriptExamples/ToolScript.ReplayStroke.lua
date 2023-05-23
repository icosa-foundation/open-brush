Settings = {
    previewType="cube"
}

function OnTriggerReleased()
    path = Sketch.strokes.last.path
    path:Resample(0.1)
    return path
end
