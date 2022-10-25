using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Animation
{
    public class AddKeyframeButton : BaseButton
    {
        [SerializeField] private UnityEngine.Events.UnityEvent m_Action;

        protected override void OnButtonPressed()
        {
            // m_Action.Invoke();
            var uiManager = GetComponentInParent<AnimationUI_Manager>();
            uiManager.addKeyframe();
            
        }
    }
} // namespace TiltBrush
