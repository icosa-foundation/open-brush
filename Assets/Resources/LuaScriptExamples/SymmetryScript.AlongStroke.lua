Settings = {space="pointer"}

function Start()
    print (Sketch.strokes)
    print (Sketch.strokes.last)
    stroke = Sketch.strokes.last
end

function Main()
    return stroke.path
end
