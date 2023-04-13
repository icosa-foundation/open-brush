Settings = {
    description="Draws words that follows your brush. Tries to access the clipboard so try copying in some text.",
    space="canvas"
}

Parameters = {
    size={label="Size", type="float", min=0.01, max=1, default=0.25},
    spacing={label="Spacing", type="float", min=0.01, max=1, default=0.25},
}

function OnTriggerPressed()
    text = app.clipboardText;
    if text == nil or string.len(text) == 0 then
        text = "Hello World"
    end
    text = text .. " "
    letterCount = 0
    distance = 0
    distanceLastFrame = 0
end

function WhileTriggerPressed()
    distanceMovedThisFrame = brush.distanceMoved - distanceLastFrame;
    distanceLastFrame = brush.distanceMoved
    distance = distance + distanceMovedThisFrame
    if distance > spacing then
        letterCount = letterCount + 1
        letter = string.sub(text, letterCount, letterCount)
        rot = brush.rotation
        transform = { position = brush.position, rotation=rot, scale=size }
        draw.text(letter, transform)
        letterCount = letterCount % string.len(text)
        distance = 0
    end
end
