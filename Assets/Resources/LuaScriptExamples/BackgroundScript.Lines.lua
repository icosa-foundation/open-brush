Settings = {
    description="Draws random lines"
}

Parameters = {
    rate={label="Rate", type="int", min=1, max=10, default=10},
    radius={label="Radius", type="int", min=1, max=10, default=10},
 }

function Main()

    startPos = {
        math.random() * radius,
        math.random() * radius,
        math.random() * radius
    };

    endPos = {
        math.random() * radius,
        math.random() * radius,
        math.random() * radius
    };

    if app.frames % rate == 0 then
        draw.path({{startPos}, {endPos}})
    end
end
