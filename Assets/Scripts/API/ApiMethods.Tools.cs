namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("tool.sketchsurface", "Activates the SketchSurface")]
        public static void ActivateSketchSurface()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SketchSurface);
        }

        [ApiEndpoint("tool.selection", "Activates the Selection Tool")]
        public static void ActivateSelection()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.Selection);
        }

        [ApiEndpoint("tool.colorpicker", "Activates the Color Picker")]
        public static void ActivateColorPicker()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ColorPicker);
        }

        [ApiEndpoint("tool.brushpicker", "Activates the Brush Picker")]
        public static void ActivateBrushPicker()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.BrushPicker);
        }

        [ApiEndpoint("tool.brushandcolorpicker", "Activates the Brush And Color Picker")]
        public static void ActivateBrushAndColorPicker()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.BrushAndColorPicker);
        }

        [ApiEndpoint("tool.sketchorigin", "Activates the SketchOrigin Tool")]
        public static void ActivateSketchOrigin()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SketchOrigin);
        }

        [ApiEndpoint("tool.autogif", "Activates the AutoGif Tool")]
        public static void ActivateAutoGif()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.AutoGif);
        }

        [ApiEndpoint("tool.canvas", "Activates the Canvas Tool")]
        public static void ActivateCanvasTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CanvasTool);
        }

        [ApiEndpoint("tool.transform", "Activates the Transform Tool")]
        public static void ActivateTransformTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.TransformTool);
        }

        [ApiEndpoint("tool.stamp", "Activates the Stamp Tool")]
        public static void ActivateStampTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.StampTool);
        }

        [ApiEndpoint("tool.freepaint", "Activates the FreePaint Tool")]
        public static void ActivateFreePaintTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FreePaintTool);
        }

        [ApiEndpoint("tool.eraser", "Activates the Eraser Tool")]
        public static void ActivateEraserTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.EraserTool);
        }

        [ApiEndpoint("tool.screenshot", "Activates the Screenshot Tool")]
        public static void ActivateScreenshotTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ScreenshotTool);
        }

        [ApiEndpoint("tool.dropper", "Activates the Dropper Tool")]
        public static void ActivateDropperTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.DropperTool);
        }

        [ApiEndpoint("tool.saveicon", "Activates the SaveIcon Tool")]
        public static void ActivateSaveIconTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SaveIconTool);
        }

        [ApiEndpoint("tool.threedofviewing", "Activates the ThreeDofViewing Tool")]
        public static void ActivateThreeDofViewingTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ThreeDofViewingTool);
        }

        [ApiEndpoint("tool.multicam", "Activates the MultiCam Tool")]
        public static void ActivateMultiCamTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.MultiCamTool);
        }

        [ApiEndpoint("tool.teleport", "Activates the Teleport Tool")]
        public static void ActivateTeleportTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.TeleportTool);
        }

        [ApiEndpoint("tool.repaint", "Activates the Repaint Tool")]
        public static void ActivateRepaintTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.RepaintTool);
        }

        [ApiEndpoint("tool.recolor", "Activates the Recolor Tool")]
        public static void ActivateRecolorTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.RecolorTool);
        }

        [ApiEndpoint("tool.rebrush", "Activates the Rebrush Tool")]
        public static void ActivateRebrushTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.RebrushTool);
        }

        [ApiEndpoint("tool.selection", "Activates the Selection Tool")]
        public static void ActivateSelectionTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.SelectionTool);
        }

        [ApiEndpoint("tool.pin", "Activates the Pin Tool")]
        public static void ActivatePinTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.PinTool);
        }

        [ApiEndpoint("tool.camerapath", "Activates the CameraPath Tool")]
        public static void ActivateCameraPathTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CameraPathTool);
        }

        [ApiEndpoint("tool.fly", "Activates the Fly Tool")]
        public static void ActivateFlyTool()
        {
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
        }
    }
}
