Settings = {
    description="Draws random lines"
}

Parameters = {
    rate={label="Rate", type="int", min=1, max=10, default=10},
    range={label="Range", type="int", min=1, max=10, default=10},
}

function Main()

    startPoint = Transform:Position(
        Random.value * Parameters.range,
        Random.value * Parameters.range,
        Random.value * Parameters.range
    )

    endPoint = Transform:Position(
        Random.value * Parameters.range,
        Random.value * Parameters.range,
        Random.value * Parameters.range
    )

    if App.frames % Parameters.rate == 0 then
        Path:New({startPoint, endPoint}):Draw()
    end
end
