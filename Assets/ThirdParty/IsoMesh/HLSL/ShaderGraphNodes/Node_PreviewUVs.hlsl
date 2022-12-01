#ifndef UV_PREVIEW_INCLUDED
#define UV_PREVIEW_INCLUDED

void UVPreview_float(in float2 UVs_Preview, in float2 UVs_Real, out float2 UVs)
{
#if SHADERGRAPH_PREVIEW
    UVs = UVs_Preview;
#else
    UVs = UVs_Real;
#endif
}

#endif // UV_PREVIEW_INCLUDED