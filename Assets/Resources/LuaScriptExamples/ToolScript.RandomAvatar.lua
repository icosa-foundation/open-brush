Settings = {
    description="Calls an API to generate a random SVG icon using the Multiavatar library",
    previewType="quad"
}

function Start()
    requestNewAvatar()
end

function OnTriggerReleased()
    requestNewAvatar()
    if svg ~= nil then
        local paths = Svg:ParseDocument(svg, 0.1, true)
        paths:Normalize(2) -- Scale and center inside a 2x2 square
        return paths
    end
end

function requestNewAvatar()
    url = "https://bit.ly/multiavatar"
    WebRequest:Get(url, onGetAvatar)
end

function onGetAvatar(result)
    svg = result
end

