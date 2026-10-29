Settings = {
    description = "Alternate warm and cool tints of the incoming drawing color"
}

local useWarmTint = false

function Start()
    useWarmTint = false
end

function Main()
    useWarmTint = not useWarmTint
    local tint
    if useWarmTint then
        tint = Color:New(1, 0.35, 0.15)
    else
        tint = Color:New(0.15, 0.45, 1)
    end
    return Color:Lerp(_currentColor, tint, 0.35)
end
