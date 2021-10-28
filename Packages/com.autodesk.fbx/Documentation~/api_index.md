# API documentation

See the [Autodesk® FBX® SDK API documentation](https://help.autodesk.com/view/FBX/2020/ENU/?guid=FBX_Developer_Help_cpp_ref_annotated_html).

The bindings are in the `Autodesk.Fbx` namespace:

```
using Autodesk.Fbx;
using UnityEditor;
using UnityEngine;

public class HelloFbx {
  [MenuItem("Fbx/Hello")]
  public static void Hello() {
    using(var manager = FbxManager.Create()) {
      Debug.LogFormat("FBX SDK is version {0}", FbxManager.GetVersion());
    }
  }
}
```
