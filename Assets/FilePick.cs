using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FilePick : MonoBehaviour
{
    void Awake()
    {
#if UNITY_ANDROID
        using( AndroidJavaClass ajc = new AndroidJavaClass( "com.yasirkula.unity.NativeFilePicker" ) )
            ajc.SetStatic<bool>( "UseDefaultFilePickerApp", true );
        using( AndroidJavaClass ajc = new AndroidJavaClass( "com.yasirkula.unity.NativeFilePickerPickFragment" ) )
            ajc.SetStatic<bool>( "showProgressbar", false );
#endif
    }
    // Start is called before the first frame update
    void Start()
    {
        NativeFilePicker.PickFile((path) => {
            Debug.Log(path);
        }, new string[] { "*/*"} );
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
