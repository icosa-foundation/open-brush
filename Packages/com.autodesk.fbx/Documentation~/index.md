# About Autodesk® FBX® SDK for Unity

The C# Autodesk® FBX® SDK package provides access to a subset of the Autodesk® FBX® SDK from Unity C# scripts.

The Autodesk® FBX® SDK is a C++ software development platform and API toolkit that is free and easy-to-use. It allows application and content vendors to transfer existing content into the FBX format with minimal effort.

> **Note:** The C# Autodesk® FBX® SDK exposes only [a subset of the full API](../api/index.html). That subset enables exporter tools, such as the [FBX Exporter](https://docs.unity3d.com/Packages/com.unity.formats.fbx@latest) package. Unity does not recommend to use the C# Autodesk® FBX® SDK package for FBX importing. See [Known issues and limitations](#known-issues-and-limitations) for more information.

## Contents

The Autodesk® FBX® SDK for Unity package contains:

* C# bindings
* Compiled binaries for MacOS, Windows, and Ubuntu that include the FBX SDK

## Requirements

The Autodesk® FBX® SDK for Unity package is compatible with the following versions of the Unity Editor:

* 2018.4 and later (recommended)

## Installation

Unity automatically installs the Autodesk® FBX® SDK as a dependency of the [FBX Exporter](https://docs.unity3d.com/Packages/com.unity.formats.fbx@latest) package.

> **Note:** The Package Manager UI does not allow you to discover it, but you can install it without installing the FBX Exporter. In that case, you need to [add it to your package manifest](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest).

## Including the package in a build

By default, Unity does not include this package in builds, you can only use it in the Editor. However, it is possible to use this package at runtime on some specific platforms.
> **Note:** You can currently use the package in Windows/OSX/Linux standalone builds only.

To include the Autodesk® FBX® SDK for Unity package in your build:
1. In the Unity Editor main menu, select **Edit > Project Settings**.
2. In **Player** properties, expand the **Other Settings** section.
3. Under **Configuration**, in the **Scripting Define Symbols** field, add `FBXSDK_RUNTIME`.

## Known issues and limitations

#### Limited import capabilities

In this version of the package, you cannot downcast SDK C# objects, which limits the use of the bindings for an importer.

For example, if the FBX SDK declares in C++ that it returns an `FbxDeformer`, you can safely cast the deformer to a skin deformer on the C++ side if you happen to know it is an `FbxSkinDeformer`. However, on the C# side, this is not permitted.

#### Unexpected crashes following invalid operations

While there are guards against some common errors, you might make Unity crash if you write C# code that directs the FBX SDK to perform invalid operations.

For example, if you have an `FbxProperty` in C# and you delete the `FbxNode` that contains the property, the use of `FbxProperty` may produce an undefined behavior. This might even make the Unity Editor crash. Make sure to read the Editor log if you encounter unexpected crashes when you write FBX SDK C# code.

#### Linux not supported

Linux support is currently experimental on this package. Unity does not provide support for it.

#### Linux requires libstdc++ 6.0.28+

On Linux, libstdc++ 6.0.28 is required to be installed in order to use the package.
