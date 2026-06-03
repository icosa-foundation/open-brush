using UnityEngine;

namespace TiltBrush
{
    /// Represents the sketchbook panel used by the Markov pen feature.
    /// Stores the currently active sketchbook panel instance.
    /// Tracks whether the sketchbook panel is currently open.
    public class MarkovPenSketchbookPanel : BasePanel
    {
        public static MarkovPenSketchbookPanel Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        /// Initialises the sketchbook panel instance.
        /// Calls the base panel setup and stores this panel as the global instance.
        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        /// Opens the sketchbook panel.
        /// Calls the base enable behaviour and marks the sketchbook panel as open.
        /// Activates the 2D drawing mode state for the Markov pen feature.
        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();
            IsOpen = true;
        }

        /// Closes the sketchbook panel.
        /// Calls the base disable behaviour and marks the sketchbook panel as closed.
        /// Deactivates the 2D drawing mode state for the Markov pen feature.
        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();
            IsOpen = false;
        }
    }
}
