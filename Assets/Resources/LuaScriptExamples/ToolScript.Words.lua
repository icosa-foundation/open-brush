Settings = {
    description="Draws words that follows your brush. Tries to access the clipboard so try copying in some text.",
    space="canvas"
}

Parameters = {
    size={label="Size", type="float", min=0.01, max=1, default=0.25},
    spacing={label="Spacing", type="float", min=0.01, max=1, default=0.25},
}

function Main()

    if Brush.triggerPressedThisFrame then

        text = App.clipboardText
        if text == nil or string.len(text) == 0 then
            text = "Hello World"
        end
        text = text .. " "
        letterCount = 0
        distance = 0
        distanceLastFrame = 0

    elseif Brush.triggerIsPressed then
        distanceMovedThisFrame = Brush.distanceMoved - distanceLastFrame
        distanceLastFrame = Brush.distanceMoved
        distance = distance + distanceMovedThisFrame
        if distance > Parameters.spacing then
            letterCount = letterCount + 1
            letter = string.sub(text, letterCount, letterCount)
            rot = Brush.rotation
            transform = Transform:New(Brush.position, rot, Parameters.size)
            paths = PathList:FromText(letter)
            paths:TransformBy(transform)
            paths:SampleByDistance(0.01)
            paths:Draw()
            letterCount = letterCount % string.len(text)
            distance = 0
        end
    end
end
