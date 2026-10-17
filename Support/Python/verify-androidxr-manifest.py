#!/usr/bin/env python3
"""Validate the final XML manifest dumped from an Android XR AAB by bundletool."""

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ANDROID = "{http://schemas.android.com/apk/res/android}"
REQUIRED_FEATURES = {
    "android.software.xr.api.openxr",
}


def validate(root):
    errors = []
    required = set()
    for feature in root.findall("uses-feature"):
        name = feature.get(f"{ANDROID}name", "<unnamed>")
        value = feature.get(f"{ANDROID}required", "true")
        if value not in {"true", "false"}:
            errors.append(f"Invalid required value for {name}: {value}")
        if value != "false":
            required.add(name)
            if name not in REQUIRED_FEATURES:
                errors.append(f"Unexpected required feature: {name}")
    for name in sorted(REQUIRED_FEATURES - required):
        errors.append(f"Missing required XR feature: {name}")
    for tag in ("uses-permission", "uses-permission-sdk-23"):
        for permission in root.findall(tag):
            if permission.get(f"{ANDROID}name") == "android.permission.MANAGE_EXTERNAL_STORAGE":
                errors.append(f"Forbidden all-files permission in {tag}")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    args = parser.parse_args()
    errors = validate(ET.parse(args.manifest).getroot())
    if errors:
        for error in errors:
            print(f"[OB-ANDROIDXR-MANIFEST] {error}", file=sys.stderr)
        return 1
    print("[OB-ANDROIDXR-MANIFEST] Only OpenXR required; no all-files permission.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
