Settings = {
    space="widget"
}

Widgets = {
    copies={label="Number of copies", type="int", min=0, max=36, default=4},
    hueShift={label="Hue Shift", type="float", min=0, max=1, default=0}
}

function Main()
    pointers = {}
    Colors = {}
    theta = 360.0 / copies
    for i = 0, copies - 1 do
        table.insert(pointers, {position={0, 0, 0}, rotation={0, i * theta, 0}})
        if hueShift > 0 then
            newColor = brush.lastColorPicked;
            table.insert(Colors, color.newColor)
        end
    end
    return pointers
end
