# Changes in FBX SDK C# Bindings

## [4.1.0] - 2021-07-06

CHANGES
* Update from prerelease to released package.

## [4.1.0-pre.3] - 2021-06-28

CHANGES
* Universal Mac binary supporting Apple M1 and OSX 10.13+.

## [4.1.0-pre.1] - 2021-04-30

NEW FEATURES
* Add support for Apple M1.
* Add binding to set a string on an FbxProperty.

## [4.0.1] - 2021-03-10

CHANGES
* Update from prerelease to released package.

## [4.0.0-pre.2] - 2021-01-08

CHANGES
* Updated documentation.
    * Updated minimum supported Unity version.
    * Removed IL2CPP backend not supported section.
    * Updated link to FBX SDK API documentation.
    * Moved API documentation to Scripting API landing page.
* Update Third Party Notices.md with new FBX License.
* Upgraded to FBX SDK 2020.2.

## [4.0.0-pre.1] - 2020-10-07

NEW FEATURES
* Binding for FbxMesh::GetPolygonVertexNormal(). Thank you to @julienkay for the addition.
* Bindings for FbxNurbsCurve. Thank you to @jeanblouin for the addition.

CHANGES
* Switched to using Unity code coverage to test unit test coverage.
* Made UnityFbxSdkNative dll and scripts Editor only by default. In order to use at runtime, 
  add the FBXSDK_RUNTIME define to Edit > Project Settings... > Player > Other Settings > Scripting Define Symbols.
* Update minimum supported Unity version from 2018.2 to 2018.4.

BUGFIXES
*  UnityFbxSdkNative dll is no longer included in builds, fixing an issue with shipping on the Mac App Store.

## [3.1.0-preview.2] - 2020-07-21

CHANGES
* ERRATA: The "Upgraded to FBX SDK 2020.0" entry in the previous version should have been "Upgraded to FBX SDK 2020.1".

## [3.1.0-preview.1] - 2020-07-17

CHANGES
* Upgraded to FBX SDK 2020.0

## [3.0.1-preview.1] - 2020-03-31

BUGFIXES
* Fix incorrect DLL path used when calling functions, giving DLL not found errors.

## [3.0.0-preview.1] - 2019-12-03

CHANGES
* Upgraded to FBX SDK 2020.0
* Added bindings for FbxAnimCurve::KeySetTangents and FbxAnimCurve::KeyGetTangents
* Added bindings for FbxAnimCurveKey methods to set and get tangent mode and data
* Added bindings for FbxAxisSystem::DeepConvertScene

BUGFIXES
* The FBX SDK C# Bindings package now supports the IL2CPP backend.

KNOW ISSUES
* For Linux support use Ubuntu 18.04 (Bionic Beaver). The FBX SDK C# Bindings package is not compatible with CentOS 7.

## [2.0.0-preview.3] - 2018-12-03

CHANGES
* Updated documentation

## [2.0.0-preview.2] - 2018-11-13

CHANGES
* Removed version number from documentation (already available in changelog)
* Added missing .meta files
* Corrected asmdef name and platform settings
* Corrected plugin .meta file platform settings
* Experimental Linux support

## [2.0.0-preview.1] - 2018-10-25

CHANGES
* Updated documentation to conform to package validation requirements

## [2.0.0-preview] - 2018-06-22

NEW FEATURES
* The C# Bindings package has been renamed to com.autodesk.fbx
* The Autodesk.Fbx assembly can now be used in standalone builds (runtime)
* Added support for physical camera attributes
* Added support for constraints: FbxConstraint, FbxConstraintParent, FbxConstraintAim, and related methods
* Updated to FBX SDK 2018.1

KNOWN ISSUES
* The FBX SDK C# Bindings package is not supported if you build using the IL2CPP backend.

## [1.3.0] - 2018-04-17
NOTES
* This is the last Asset Store version. It is also known as 1.3.0f1.

NEW FEATURES
* Added bindings for FbxAnimCurveFilterUnroll
* Added binding for FbxGlobalSettings SetTimeMode to set frame rate
* Exposed bindings to set FbxNode's transformation inherit type
* Added binding for FbxCamera's FieldOfView property
* Added FbxObject::GetScene
* Added bindings for FbxIOFileHeaderInfo. 
* Exposed mCreator and mFileVersion as read-only attributes.

FIXES
* Fix Universal Windows Platform build error caused by UnityFbxSdk.dll being set as compatible with any platform instead of editor only.
* Enforced FbxSdk DLL only works with Unity 2017.1+
