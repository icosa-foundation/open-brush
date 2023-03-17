Settings = {
    description="Draws random lines"
}

Parameters = {
    rate={label="Rate", type="int", min=1, max=10, default=10},
    range={label="Range", type="int", min=1, max=10, default=10},
 }

function Main()

    startPos = {
        math.random() * range,
        math.random() * range,
        math.random() * range
    };

    endPos = {
        math.random() * range,
        math.random() * range,
        math.random() * range
    };

    if app.frames % rate == 0 then
        draw.path({{startPos}, {endPos}})
    end
end
