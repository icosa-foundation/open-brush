#!/usr/bin/env python3
"""Check provider libraries and the final XML manifest of an Android APK/AAB."""

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZipFile

ANDROID = "{http://schemas.android.com/apk/res/android}"
PREFIX = "[OB_ANDROID_BUILD_SPLIT]"
# Audited from ARCore 6.6.2's UnityARCore, ARPresto and arcore_client AARs.
# Re-audit this inventory when upgrading the provider.
ARCORE_LIBRARIES = {
    "libUnityARCore.so",
    "libarpresto_api.so",
    "libarcore_sdk_c.so",
    "libarcore_sdk_jni.so",
}
HEADSET_FEATURES = {
    "android.software.xr.api.openxr",
    "android.software.xr.api.spatial",
    "android.hardware.vr.headtracking",
    "android.hardware.xr.input.hand_tracking",
    "oculus.software.handtracking",
    "com.oculus.feature.PASSTHROUGH",
}
HEADSET_METADATA = {
    "pvr.app.type", "handtracking", "controller", "com.oculus.supportedDevices",
    "com.samsung.android.vr.application.mode", "com.oculus.vr.focusaware",
}
HEADSET_PERMISSIONS = {
    "android.permission.HAND_TRACKING", "com.oculus.permission.HAND_TRACKING",
    "com.oculus.permission.USE_SCENE", "com.picovr.permission.HAND_TRACKING",
}


def validate(archive, root, handheld):
    errors = []
    with ZipFile(archive) as package:
        entries = package.namelist()
    # APK: lib/<abi>/*.so; AAB: base/lib/<abi>/*.so.
    libraries = {Path(name).name for name in entries if "/lib/" in f"/{name}" and name.endswith(".so")}
    arcore = libraries & ARCORE_LIBRARIES
    openxr = {name for name in libraries if "openxr" in name.lower()}
    print(f"{PREFIX} ARCore libraries: {', '.join(sorted(arcore)) or '(none)'}")
    print(f"{PREFIX} OpenXR libraries: {', '.join(sorted(openxr)) or '(none)'}")

    features = {node.get(f"{ANDROID}name"): node for node in root.findall("uses-feature")}
    metadata = {node.get(f"{ANDROID}name"): node.get(f"{ANDROID}value")
                for node in root.findall(".//meta-data")}
    relevant = HEADSET_FEATURES | {"android.hardware.camera.ar", "com.google.ar.core.depth"}
    print(f"{PREFIX} Relevant features: " + (", ".join(
        f"{name}:required={features[name].get(f'{ANDROID}required', 'true')}"
        for name in sorted(features.keys() & relevant)) or "(none)"))

    if handheld:
        missing = ARCORE_LIBRARIES - arcore
        if missing:
            errors.append(f"Missing ARCore libraries: {', '.join(sorted(missing))}")
        if openxr:
            errors.append("Handheld Viewer includes OpenXR native libraries")
        if metadata.get("com.google.ar.core") != "optional":
            errors.append("Handheld Viewer must declare ARCore optional")
        for name in sorted(features.keys() & HEADSET_FEATURES):
            errors.append(f"Handheld Viewer includes headset feature: {name}")
        for name in ("android.hardware.camera.ar", "com.google.ar.core.depth"):
            if name in features and features[name].get(f"{ANDROID}required", "true") != "false":
                errors.append(f"Handheld Viewer requires optional AR capability: {name}")
        for name in sorted(metadata.keys() & HEADSET_METADATA):
            errors.append(f"Handheld Viewer includes headset metadata: {name}")
        permissions = {node.get(f"{ANDROID}name") for tag in ("uses-permission", "uses-permission-sdk-23")
                       for node in root.findall(tag)}
        for name in sorted(permissions & HEADSET_PERMISSIONS):
            errors.append(f"Handheld Viewer includes headset permission: {name}")
        if "android.permission.CAMERA" not in permissions:
            errors.append("Handheld Viewer is missing its AR camera permission")
        for node in root.findall(".//category"):
            if node.get(f"{ANDROID}name") in {
                "com.oculus.intent.category.VR", "org.khronos.openxr.intent.category.IMMERSIVE_HMD"
            }:
                errors.append("Handheld Viewer includes a headset launcher category")
        launchers = [activity for activity in root.findall("application/activity")
                     if activity.get(f"{ANDROID}enabled", "true") != "false"
                     and any(
                         any(n.get(f"{ANDROID}name") == "android.intent.action.MAIN" for n in intent.findall("action"))
                         and any(n.get(f"{ANDROID}name") == "android.intent.category.LAUNCHER" for n in intent.findall("category"))
                         for intent in activity.findall("intent-filter"))]
        if not launchers:
            errors.append("Handheld Viewer has no enabled MAIN/LAUNCHER activity")
    else:
        if arcore:
            errors.append("Headset package includes ARCore native libraries")
        if "libUnityOpenXR.so" not in libraries:
            errors.append("Headset package is missing the Unity OpenXR native library")
        if "com.google.ar.core" in metadata or "android.hardware.camera.ar" in features:
            errors.append("Headset manifest includes phone ARCore declarations")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("manifest", type=Path, help="Final manifest decoded to XML")
    parser.add_argument("--mode", required=True, choices=("handheld", "headset"))
    args = parser.parse_args()
    errors = validate(args.archive, ET.parse(args.manifest).getroot(), args.mode == "handheld")
    for error in errors:
        print(f"{PREFIX} {error}", file=sys.stderr)
    if errors:
        return 1
    print(f"{PREFIX} {args.mode} provider packaging passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
