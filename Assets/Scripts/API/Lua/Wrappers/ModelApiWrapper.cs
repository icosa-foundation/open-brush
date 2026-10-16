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

        [LuaDocsDescription("Whether this instance's playback is initialized")]
        public bool initialized => _ModelWidget.Playback.Initialized;
        [LuaDocsDescription("Whether an IMM seek, chapter change or saved-state restore is pending")]
        public bool busy => _ModelWidget.Playback.Busy || _ModelWidget.Playback.IsRestoring;
        [LuaDocsDescription("The last asynchronous playback error, or nil")]
        public string playbackError => _ModelWidget.Playback.LastError;
        [LuaDocsDescription("Whether this instance is playing. Setting true also continues an IMM stop point.")]
        public bool playing { get => _ModelWidget.Playback.Playing; set { if (value) Play(); else Pause(); } }
        [LuaDocsDescription("Playback time in seconds; setting it seeks this instance")]
        public double time { get => _ModelWidget.Playback.Time; set => _ModelWidget.Playback.Seek(value); }
        [LuaDocsDescription("Playback duration in seconds")]
        public double length => _ModelWidget.Playback.Length;
        [LuaDocsDescription("Audio volume from 0 to 1; zero is muted")]
        public float volume { get => _ModelWidget.Playback.Volume; set => _ModelWidget.Playback.SetVolume(value); }
        [LuaDocsDescription("Whether this model contains audio")]
        public bool hasAudio => _ModelWidget.Playback.HasAudio;
        [LuaDocsDescription("Current IMM chapter, zero-based; -1 for other models")]
        public int chapter { get => _ModelWidget.Playback.Chapter; set => _ModelWidget.Playback.SetChapter(value); }
        [LuaDocsDescription("Number of IMM chapters")]
        public int chapterCount => _ModelWidget.Playback.ChapterCount;
        [LuaDocsDescription("Available glTF animation clip names")]
        public string[] clips => _ModelWidget.Playback.Clips;
        [LuaDocsDescription("Current glTF animation clip name")]
        public string clip { get => _ModelWidget.Playback.GetSaveState()?.Clip; set => _ModelWidget.Playback.SelectClip(value); }
        [LuaDocsDescription("Whether this glTF animation loops")]
        public bool loop { get => _ModelWidget.Playback.GetSaveState()?.Loop ?? false; set => _ModelWidget.Playback.SetLoop(value); }
        [LuaDocsDescription("Play or continue this instance")]
        public void Play() => _ModelWidget.Playback.Play();
        [LuaDocsDescription("Pause this instance")]
        public void Pause() => _ModelWidget.Playback.Pause();
        [LuaDocsDescription("Continue an IMM stop point or resume a paused model")]
        public void Continue() => _ModelWidget.Playback.Continue();
        [LuaDocsDescription("Seek to time in seconds; IMM operations finish asynchronously")]
        public void Seek(double seconds) => _ModelWidget.Playback.Seek(seconds);
        [LuaDocsDescription("List IMM viewpoint IDs, names, availability and active selection")]
        public ModelViewpointInfo[] viewpoints => _ModelWidget.Playback.GetViewpoints();
        [LuaDocsDescription("Explicitly move the user to an eligible IMM viewpoint ID")]
        public void NavigateToViewpoint(int id) => _ModelWidget.Playback.NavigateToViewpoint(id);

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
