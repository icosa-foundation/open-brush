using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A 3D model widget")]
    [MoonSharpUserData]
    public class ModelApiWrapper
    {

        public ModelWidget _ModelWidget;

        public ModelApiWrapper(ModelWidget widget)
        {
            _ModelWidget = widget;
        }

        [LuaDocsDescription(@"The index of the active Model Widget")]
        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_ModelWidget);

        public override string ToString()
        {
            return $"Model({_ModelWidget})";
        }

        [LuaDocsDescription("The layer the model is on")]
        public LayerApiWrapper layer
        {
            get => _ModelWidget != null ? new LayerApiWrapper(_ModelWidget.Canvas) : null;
            set => _ModelWidget.SetCanvas(value._CanvasScript);
        }

        [LuaDocsDescription("The group this model is part of")]
        public GroupApiWrapper group
        {
            get => _ModelWidget != null ? new GroupApiWrapper(_ModelWidget.Group, layer._CanvasScript) : null;
            set => _ModelWidget.Group = value._Group;
        }

        [LuaDocsDescription(@"The transformation of the Model Widget")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_ModelWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_ModelWidget.transform] = value;
            }
        }

        [LuaDocsDescription(@"The 3D position of the Model Widget")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription(@"The 3D orientation of the Model Widget")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }

        [LuaDocsDescription(@"The scale of the Model Widget")]
        public float scale
        {
            get => transform.scale;
            set => transform = TrTransform.TRS(transform.translation, transform.rotation, value);
        }

        private ModelPlaybackController Playback => _ModelWidget != null ? _ModelWidget.Playback :
            throw new ScriptRuntimeException("Model instance no longer exists");

        [LuaDocsDescription("Whether this instance's playback is initialized")]
        public bool initialized => Playback.Initialized;
        [LuaDocsDescription("Whether an IMM seek, chapter change or saved-state restore is pending")]
        public bool busy => Playback.Busy || Playback.IsRestoring;
        [LuaDocsDescription("The last asynchronous playback error, or nil")]
        public string playbackError => Playback.LastError;
        [LuaDocsDescription("Whether this instance is playing. Setting true also continues an IMM stop point.")]
        public bool playing { get => Playback.Playing; set { if (value) Play(); else Pause(); } }
        [LuaDocsDescription("Playback time in seconds; setting it seeks this instance")]
        public double time { get => Playback.Time; set => Playback.Seek(value); }
        [LuaDocsDescription("Playback duration in seconds")]
        public double length => Playback.Length;
        [LuaDocsDescription("Audio volume from 0 to 1; zero is muted")]
        public float volume { get => Playback.Volume; set => Playback.SetVolume(value); }
        [LuaDocsDescription("Whether this model contains audio")]
        public bool hasAudio => Playback.HasAudio;
        [LuaDocsDescription("Current IMM chapter, zero-based; -1 for other models")]
        public int chapter { get => Playback.Chapter; set => Playback.SetChapter(value); }
        [LuaDocsDescription("Number of IMM chapters")]
        public int chapterCount => Playback.ChapterCount;
        [LuaDocsDescription("Available glTF animation clip names")]
        public string[] clips => Playback.Clips;
        [LuaDocsDescription("Current glTF animation clip name")]
        public string clip { get => Playback.GetSaveState()?.Clip; set => Playback.SelectClip(value); }
        [LuaDocsDescription("Whether this glTF animation loops")]
        public bool loop { get => Playback.GetSaveState()?.Loop ?? false; set => Playback.SetLoop(value); }
        [LuaDocsDescription("Play or continue this instance")]
        public void Play() => Playback.Play();
        [LuaDocsDescription("Pause this instance")]
        public void Pause() => Playback.Pause();
        [LuaDocsDescription("Continue an IMM stop point or resume a paused model")]
        public void Continue() => Playback.Continue();
        [LuaDocsDescription("Seek to time in seconds; IMM operations finish asynchronously")]
        public void Seek(double seconds) => Playback.Seek(seconds);
        [LuaDocsDescription("List IMM viewpoint IDs, names, availability and active selection")]
        public ModelViewpointInfo[] viewpoints => Playback.GetViewpoints();
        [LuaDocsDescription("Select an eligible IMM viewpoint ID without moving or rescaling anything")]
        public void SelectViewpoint(int id) => Playback.SelectViewpoint(id);
        [LuaDocsDescription("Move once to an eligible IMM viewpoint ID, rescaling only this model around the viewpoint. Preserves scene scale and does not follow viewpoint animation.")]
        public void NavigateToViewpoint(int id) => Playback.NavigateToViewpoint(id);

        [LuaDocsDescription(@"Imports a new model from the MediaLibrary/Models folder")]
        [LuaDocsExample(@"Model:Import(""Andy.obj"")")]
        [LuaDocsParameter(@"filename", "The filename of the model to be imported")]
        [LuaDocsReturnValue(@"Returns the Model instance")]
        public static ModelApiWrapper Import(string filename) => new(ApiMethods.ImportModel(filename));

        [LuaDocsDescription(@"Adds this model to the current selection")]
        [LuaDocsExample(@"myModel:Select()")]
        public void Select() => ApiMethods.SelectWidget(_ModelWidget);

        [LuaDocsDescription("Removes the model from the current selection")]
        [LuaDocsExample("myModel:Deselect()")]
        public void Deselect() => ApiMethods.DeselectWidget(_ModelWidget);

        [LuaDocsDescription(@"Deletes this model")]
        [LuaDocsExample(@"myModel:Delete()")]
        public void Delete() => ApiMethods.DeleteWidget(_ModelWidget);
    }
}
