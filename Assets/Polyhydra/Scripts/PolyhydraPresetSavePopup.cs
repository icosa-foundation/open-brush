using TiltBrush;
using UnityEngine;

public class PolyhydraPresetSavePopup : MonoBehaviour
{
    public void SavePreset(bool overwrite = false)
    {
        var popup = gameObject.GetComponent<OptionsPopUpWindow>();
        var parent = popup.GetParentPanel() as PolyhydraPanel;
        parent.HandleSavePreset(overwrite);
        popup.RequestClose();

    }
}
