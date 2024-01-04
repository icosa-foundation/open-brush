Settings = {previewType="quad"}

Parameters = {
    spacing={label="Point Spacing", type="float", min=0.1, max=1, default=0.1},
}

function Main()
    if Brush.triggerReleasedThisFrame then
        heartSvg = "M213.588,120.982L115,213.445l-98.588-92.463C-6.537,96.466-5.26,57.99,19.248,35.047l2.227-2.083 c24.51-22.942,62.984-21.674,85.934,2.842L115,43.709l7.592-7.903c22.949-24.516,61.424-25.784,85.936-2.842l2.227,2.083 C235.26,57.99,236.537,96.466,213.588,120.982z"
        local stroke = Svg:ParsePathString(heartSvg)
        stroke:Normalize(2) -- Scale and center inside a 2x2 square
        stroke:SampleByDistance(Parameters.spacing) -- Make the stroke evenly spaced
        return stroke
    end
end
