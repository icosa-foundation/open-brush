Settings = {
    description="Cycles through all layers, fading one out as the next one fades in"
}

Parameters = {
    speed={label="Speed", type="float", min=0, max=1, default=0.01},
}

function Start()
    if Sketch.layers.count < 2 then return end
    currentLayerNumber = 0
    nextLayerNumber = 1
    fadeProgress = 0
    for i = 0, Sketch.layers.count - 1 do
        Sketch.layers[i].allowStrokeAnimation = true
        Sketch.layers[i]:SetShaderFloat("_Opacity", 0)
    end
    Sketch.layers[0]:SetShaderFloat("_Opacity", 1)
end

function Main()
    if Sketch.layers.count < 2 then return end
    layerOut = Sketch.layers[currentLayerNumber]
    layerIn = Sketch.layers[nextLayerNumber]
    layerOut:SetShaderFloat("_Opacity", 1 - fadeProgress)
    layerIn:SetShaderFloat("_Opacity", fadeProgress)
    fadeProgress = fadeProgress + Parameters.speed
    if fadeProgress >= 1 then
        fadeProgress = 0
        currentLayerNumber = (currentLayerNumber + 1) % Sketch.layers.count
        nextLayerNumber = (nextLayerNumber + 1) % Sketch.layers.count
    end
end
