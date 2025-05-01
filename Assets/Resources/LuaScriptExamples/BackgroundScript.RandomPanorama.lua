Settings = {
    description="Loads a random panorama from Wikipedia every time the script starts"
}

function onGetImageResults(result)
    local imageList = json:parse(result)["results"]
    local randomItem = imageList[math.random(#imageList)]
    env = Sketch.environments:ByName("Black")
    Sketch.environments.current = env
    aspectRatio = randomItem.width / randomItem.height
    if aspectRatio < 1.5 or aspectRatio > 3  then
        App:Error("Skipping image with aspect ratio: " .. aspectRatio)
    elseif randomItem.width > 10000 or randomItem.height > 5000 then
        App:Error("Skipping huge image with size: " .. randomItem.width .. " by " .. randomItem.height)
    else
        Sketch:ImportSkybox(randomItem.url)
    end
end

function Start()
    page = math.random(20)
    url = "https://api.openverse.engineering/v1/images/?aspect_ratio=wide&format=json&licence=cc0&q=360+panorama&size=large&source=wikimedia&page=" .. page
    WebRequest:Get(url, onGetImageResults)
end
