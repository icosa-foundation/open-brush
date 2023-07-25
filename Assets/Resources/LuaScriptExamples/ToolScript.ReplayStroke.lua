Settings = {
    previewType="cube"
}

function Main()
    if Brush.triggerReleasedThisFrame then
        stroke = Sketch.strokes.last
        if stroke == nil then
            App.Error("Please draw a stroke with the brush and then try again")
        else
            path = stroke.path
            path:Resample(0.1)
            return path
        end
    end
end
