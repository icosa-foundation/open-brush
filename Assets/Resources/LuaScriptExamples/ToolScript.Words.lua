Settings = {
    description="Draws words that follows your brush",
    space="canvas"
}

Parameters = {
    size={label="Size", type="float", min=0.01, max=1, default=0.25},
    spacing={label="Spacing", type="float", min=0.01, max=1, default=0.25},
    text={label="Text", type="text", default="Hello World"},
    useClipboard={label="Use Clipboard Text?", type="toggle", default=false},
}

function Main()

    if Brush.triggerPressedThisFrame then

        if Parameters.useClipboard then
            chosenText = App.clipboardText
        else
            chosenText = Parameters.text
        end

        chosenText = chosenText .. " "
        letterCount = 0
        distance = 0
        distanceLastFrame = 0

    elseif Brush.triggerIsPressed then
        distanceMovedThisFrame = Brush.distanceMoved - distanceLastFrame
        distanceLastFrame = Brush.distanceMoved
        distance = distance + distanceMovedThisFrame
        if distance > Parameters.spacing then
            letterCount = letterCount + 1
            letter = string.sub(chosenText, letterCount, letterCount)
            rot = Brush.rotation
            transform = Transform:New(Brush.position, rot, Parameters.size)
            paths = PathList:FromText(letter)
            paths:TransformBy(transform)
            paths:SampleByDistance(0.01)
            paths:Draw()
            letterCount = letterCount % string.len(chosenText)
            distance = 0
        end
    end
end
