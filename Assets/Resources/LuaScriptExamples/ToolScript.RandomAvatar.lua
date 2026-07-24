-- Requires the following host allowlist entry, or EnablePluginWebRequests:
-- "PluginWebRequestRules": [
--   {"Host":"avatars.openbrush.app", "Methods":["GET"], "FileTypes":["image"]}
-- ]

Settings = {
    description="Calls an API to generate a random SVG icon using the Multiavatar library",
    previewType="quad"
}

function Start()
    requestNewAvatar()
end

function Main()
    if Brush.triggerReleasedThisFrame then
        requestNewAvatar()
        if svg ~= nil then
            local paths = Svg:ParseDocument(svg, 0.1, true)
            paths:Normalize(2) -- Scale and center inside a 2x2 square
            return paths
        end
    end
end

function requestNewAvatar()
    url = "https://avatars.openbrush.app/avatar.svg"
    WebRequest:Get(url, onGetAvatar)
end

function onGetAvatar(result)
    svg = result
end

