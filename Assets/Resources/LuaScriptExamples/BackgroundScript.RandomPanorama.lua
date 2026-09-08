-- Requires the following host allowlist entries, or EnablePluginWebRequests:
-- "PluginWebRequestRules": [
--   {"Host":"api.openverse.org", "Methods":["GET"], "FileTypes":["json"]},
--   {"Host":"upload.wikimedia.org", "Methods":["GET"], "FileTypes":["image"]}
-- ]

Settings = {
    description="Loads a random panorama from Wikipedia every time the script starts"
}

MAX_PAGE_ATTEMPTS = 3
pageAttempts = 0
startPage = 1

WIKIMEDIA_THUMBNAIL_WIDTHS = {3840, 1920, 1280, 960}

function getWikimediaThumbnailUrl(image)
    local lowerUrl = string.lower(image.url)
    local isJpeg = string.sub(lowerUrl, -4) == ".jpg" or
        string.sub(lowerUrl, -5) == ".jpeg"
    if not isJpeg then return nil end

    local thumbnailWidth = nil
    for i = 1, #WIKIMEDIA_THUMBNAIL_WIDTHS do
        if image.width >= WIKIMEDIA_THUMBNAIL_WIDTHS[i] then
            thumbnailWidth = WIKIMEDIA_THUMBNAIL_WIDTHS[i]
            break
        end
    end
    if thumbnailWidth == nil then return nil end

    local filename = string.match(image.url, "/([^/]+)$")
    if filename == nil then return nil end
    local thumbnailBase = string.gsub(
        image.url, "/wikipedia/commons/", "/wikipedia/commons/thumb/", 1)
    return thumbnailBase .. "/" .. thumbnailWidth .. "px-" .. filename
end

function onGetImageResults(result)
    local imageList = json.parse(result)["results"]
    local validImages = {}

    for i = 1, #imageList do
        local image = imageList[i]
        if image.width ~= nil and image.height ~= nil and image.height > 0 and
            type(image.url) == "string" then
            local aspectRatio = image.width / image.height
            local hasValidAspectRatio = aspectRatio >= 1.5 and aspectRatio <= 3
            local hasValidSize = image.width <= 10000 and image.height <= 5000
            local hasValidHost = string.sub(image.url, 1, 29) == "https://upload.wikimedia.org/"
            local thumbnailUrl = getWikimediaThumbnailUrl(image)

            if hasValidAspectRatio and hasValidSize and hasValidHost and
                thumbnailUrl ~= nil then
                image.thumbnailUrl = thumbnailUrl
                table.insert(validImages, image)
            end
        end
    end

    if #validImages == 0 then
        if pageAttempts < MAX_PAGE_ATTEMPTS then
            requestImageResults()
        else
            App:Error("No suitable panorama found after " .. pageAttempts .. " result pages")
        end
        return
    end

    local randomItem = validImages[math.random(#validImages)]
    local env = Sketch.environments:ByName("Black")
    Sketch.environments.current = env
    Sketch:ImportSkybox(randomItem.thumbnailUrl)
end

function requestImageResults()
    pageAttempts = pageAttempts + 1
    local page = ((startPage + pageAttempts - 2) % 20) + 1
    local url = "https://api.openverse.org/v1/images/?aspect_ratio=wide&license=cc0&q=360+panorama&size=large&source=wikimedia&page=" .. page
    WebRequest:Get(url, onGetImageResults)
end

function Start()
    startPage = math.random(20)
    requestImageResults()
end
