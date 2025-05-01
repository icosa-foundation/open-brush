using System.Linq;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("Modifies grouped items")]
    [MoonSharpUserData]
    public class GroupApiWrapper
    {
        public SketchGroupTag _Group;
        public CanvasScript _Layer;

        public GroupApiWrapper(SketchGroupTag strokeGroupTag, CanvasScript layer)
        {
            _Group = strokeGroupTag;
            _Layer = layer;
        }

        public GroupApiWrapper()
        {
            _Group = App.GroupManager.NewUnusedGroup();
            _Layer = App.ActiveCanvas;
        }

        public GroupApiWrapper(SketchGroupTag group)
        {
            _Group = group;
            _Layer = App.ActiveCanvas;
        }

        [LuaDocsDescription("Creates and returns a new empty group")]
        [LuaDocsExample(@"myGroup = Group:New()")]
        [LuaDocsReturnValue("The new group")]
        public static GroupApiWrapper New()
        {
            return new GroupApiWrapper();
        }

        [LuaDocsDescription("Adds an image to this group moving it to the group's layer if necessary")]
        [LuaDocsParameter("image", "The image to add")]
        [LuaDocsExample(@"myGroup:Add(myImage)")]
        public void Add(ImageApiWrapper image)
        {
            _Add(image._ImageWidget);
        }

        [LuaDocsDescription("Adds a video to this group moving it to the group's layer if necessary")]
        [LuaDocsParameter("video", "The video to add")]
        [LuaDocsExample(@"myGroup:Add(myVideo)")]
        public void Add(VideoApiWrapper video)
        {
            _Add(video._VideoWidget);
        }

        [LuaDocsDescription("Adds a model to this group moving it to the group's layer if necessary")]
        [LuaDocsParameter("model", "The model to add")]
        [LuaDocsExample(@"myGroup:Add(myModel)")]
        public void Add(ModelApiWrapper model)
        {
            _Add(model._ModelWidget);
        }

        [LuaDocsDescription("Adds a guide to this group moving it to the group's layer if necessary")]
        [LuaDocsParameter("guide", "The guide to add")]
        [LuaDocsExample(@"myGroup:Add(myGuide)")]
        public void Add(GuideApiWrapper guide)
        {
            _Add(guide._StencilWidget);
        }

        [LuaDocsDescription("Adds an image to this group moving it to the group's layer if necessary")]
        [LuaDocsParameter("cameraPath", "The CameraPath to add")]
        [LuaDocsExample(@"myGroup:Add(myCameraPath)")]
        public void Add(CameraPathApiWrapper cameraPath)
        {
            _Add(cameraPath._CameraPathWidget);
        }

        private void _Add(GrabWidget widget)
        {
            if (widget.Canvas != _Layer) widget.SetCanvas(_Layer);
            widget.Group = _Group;
        }

        [LuaDocsDescription("Adds a stroke to this group moving it to the group's layer if necessary")]
        [LuaDocsParameter("stroke", "The Stroke to add")]
        [LuaDocsExample(@"myGroup:Add(mystroke)")]
        public void Add(StrokeApiWrapper stroke)
        {
            if (stroke._Stroke.Canvas != _Layer)
            {
                stroke._Stroke.SetParentKeepWorldPosition(_Layer);
            }
            stroke._Stroke.Group = _Group;
        }

        [LuaDocsDescription("All the images in this group")]
        public ImageListApiWrapper images =>
            new(new LayerApiWrapper(_Layer).images._Images.Where(w => w.Group == _Group).ToList());

        [LuaDocsDescription("All the videos in this group")]
        public VideoListApiWrapper videos =>
            new(new LayerApiWrapper(_Layer).videos._Videos.Where(w => w.Group == _Group).ToList());

        [LuaDocsDescription("All the models in this group")]
        public ModelListApiWrapper models =>
            new(new LayerApiWrapper(_Layer).models._Models.Where(w => w.Group == _Group).ToList());

        [LuaDocsDescription("All the guides in this group")]
        public GuideListApiWrapper guides =>
            new(new LayerApiWrapper(_Layer).guides._Guides.Where(w => w.Group == _Group).ToList());

        [LuaDocsDescription("All the camera paths in this group")]
        public CameraPathListApiWrapper cameraPaths =>
            new(new LayerApiWrapper(_Layer).cameraPaths._CameraPaths.Where(w => w.Group == _Group).ToList());
    }
}

