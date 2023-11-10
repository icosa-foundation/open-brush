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
            var uiManager = GetComponentInParent<AnimationUI_Manager>();
            uiManager.moveKeyFrame(moveRight);

        }
    }
} // namespace TiltBrush
