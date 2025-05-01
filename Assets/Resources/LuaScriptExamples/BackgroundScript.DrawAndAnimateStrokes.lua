Settings = {description="Example stroke animation"}

Parameters = {
    speed={label="Speed", type="float", min=1, max=10, default=4},
    copies={label="Copies", type="int", min=1, max=96, default=36},
    hueShift={label="Hue Shift", type="float", min=0, max=1, default=0.5},
}

function Start()
    angle = 360 / Parameters.copies
    Brush.type = "TubeHighlighter"
    Sketch.mainLayer.allowStrokeAnimation = true
    for i = 0, Parameters.copies - 1, 1 do
        path = Path2d:Polygon(24):OnZ()
        path:TranslateBy(Vector3:New(4, 0, 0))
        path:RotateBy(Rotation:New(0, i * angle, 0))
        path:TranslateBy(Vector3:New(0, 12, 10))
        Brush.colorRgb = Color:HsvToRgb(i/Parameters.copies * Parameters.hueShift, 1, 0.5)
        path:Draw()
    end
    strokes = Sketch.mainLayer.strokes
end

function Main()
    for i = 0, Parameters.copies - 1, 1 do
        value = (Math:Sin(App.time * Parameters.speed + (i * 0.2)) + 1) / 4
        strokes[i]:SetShaderClipping(value, value + 0.5)
    end
end
