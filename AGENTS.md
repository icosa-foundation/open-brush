Write a python script that will:

Scan I:\Unity Projects\open-brush-main\Assets\Resources\X\Brushes for Unity material files (.mat) and extract the uniforms and their values.

The output should be in the format of a JavaScript object with the material name as the key.  One for each material.

Paths to textures always take the same form:

    DuctTape-3ca16e2f-bdcd-4da2-8631-dcef342f40f1/DuctTape-3ca16e2f-bdcd-4da2-8631-dcef342f40f1-v10.0-MainTex.png

breaking this down:

    {BrushName}-{GUID}/{BrushName}-{GUID}-v{Version}-{Filename}.png

brushname is the same as the material name
GUID can just be a dummy value
Verson is always 10.0
Filename can just be a dummy value.

Example output:

"Muscle" : {
uniforms: {
    u_SceneLight_0_matrix: { value: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] },
    u_SceneLight_1_matrix: { value: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] },
    u_ambient_light_color: { value: new Vector4(0.3922, 0.3922, 0.3922, 1) },
    u_SceneLight_0_color: { value: new Vector4(0.7780, 0.8157, 0.9914, 1) },
    u_SceneLight_1_color: { value: new Vector4(0.4282, 0.4212, 0.3459, 1) },
    u_SpecColor: { value: new Vector3(0.5372549, 0.5372549, 0.5372549) },
    u_Shininess: { value: 0.414 },
    u_MainTex: { value: "DuctTape-3ca16e2f-bdcd-4da2-8631-dcef342f40f1/DuctTape-3ca16e2f-bdcd-4da2-8631-dcef342f40f1-v10.0-MainTex.png" },
    u_Cutoff: { value: 0.2 },
    u_fogColor: { value: new Vector3(0.0196, 0.0196, 0.0196) },
    u_fogDensity: { value: 0 },
    u_BumpMap: { value: "DuctTape-3ca16e2f-bdcd-4da2-8631-dcef342f40f1/DuctTape-3ca16e2f-bdcd-4da2-8631-dcef342f40f1-v10.0-BumpMap.png" },
    u_BumpMap_TexelSize: { value: new Vector4(0.0010, 0.0078, 1024, 128) },
}