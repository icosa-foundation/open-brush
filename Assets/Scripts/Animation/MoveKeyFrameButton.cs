using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.FrameAnimation
{
    public class MoveKeyFrameButton : BaseButton
    {
        [SerializeField] private UnityEngine.Events.UnityEvent m_Action;

        public bool moveRight = true;

        protected override void OnButtonPressed()
        {
            // m_Action.Invoke();
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveFrameCommand(moveRight)
            );

        }
    }
} // namespace TiltBrush
