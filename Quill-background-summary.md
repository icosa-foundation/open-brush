# Quill background color → Open Brush

Summary
- Quill files (SharpQuill sequences) include background/clear color metadata in the sequence object produced by SQ.QuillSequenceReader or the IMM compatibility reader (ImmStrokeReader.SharpQuillCompat).
- Open Brush stores scene clear/ambient colors in Environment.RenderSettingsLite (Environment.cs) — notably m_ClearColor — and applies them via SceneSettings: RegisterCamera assigns rCamera.backgroundColor = m_DesiredEnvironment.m_RenderSettings.m_ClearColor and the SceneSettings update/transition logic writes m_InterimValues.m_ClearColor to all registered cameras' backgroundColor.

Current behavior
- Quill.Load (Assets/Scripts/Quill.cs) parses the SharpQuill sequence and imports strokes, picture layers, and audio, and writes picture layers into the media library, but it does NOT map the sequence background/clear color into Environment.RenderSettingsLite or call SceneSettings to apply it.

How to apply Quill background color in Open Brush
- The importer would need to read the background/clear color from the parsed Sequence (from SQ.QuillSequenceReader.Read or the IMM adapter) and then call SceneSettings (e.g., create/update an Environment.RenderSettingsLite and call SceneSettings.SetDesiredPreset or directly set RenderSettings/camera.backgroundColor) to make the color visible in the scene.

Relevant files
- Assets/Scripts/Quill.cs — reads SharpQuill sequence and imports layers/strokes/images (does not set clear color).
- Assets/Scripts/Environment.cs — Environment.RenderSettingsLite (m_ClearColor, ambient, skybox cubemap, etc.).
- Assets/Scripts/SceneSettings.cs — applies environment values to RenderSettings and per-camera backgroundColor, and handles skybox/gradient/custom skybox loading.

Notes
- Background color is distinct from background images/skyboxes: Quill picture layers are imported as images (media library) and can be used as skyboxes via SceneSettings.LoadCustomSkybox, but the scalar clear color must be explicitly transferred from the Quill sequence to Open Brushs environment settings if desired.
