Settings = {
    previewType="cube"
}

function Main()
    if Brush.triggerReleasedThisFrame then
        path = Sketch.strokes.last.path
        path:Resample(0.1)
        return path
    end
end
