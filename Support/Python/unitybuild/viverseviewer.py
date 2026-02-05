# Copyright 2020 The Tilt Brush Authors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""
Utility to generate ViverseViewer.bytes from ViverseViewer source directory
"""

import zipfile
from pathlib import Path


def generate_viverseviewer_bytes(project_root):
    """
    Zip ViverseViewer/ directory to Assets/Resources/ViverseViewer.bytes

    Args:
        project_root: Path to project root directory

    Raises:
        FileNotFoundError: If ViverseViewer source directory doesn't exist
        Exception: If zip creation fails
    """
    project_root = Path(project_root).resolve()
    viewer_src = project_root / "Support" / "ViverseViewer"
    viewer_dest = project_root / "Assets" / "Resources" / "ViverseViewer.bytes"

    # Validate source exists
    if not viewer_src.exists():
        raise FileNotFoundError(
            f"ViverseViewer source directory not found: {viewer_src}\n"
            "This directory must exist and contain the ViverseViewer files."
        )

    if not viewer_src.is_dir():
        raise ValueError(f"ViverseViewer path is not a directory: {viewer_src}")

    # Ensure Resources directory exists
    viewer_dest.parent.mkdir(parents=True, exist_ok=True)

    # Create zip
    print(f"Generating {viewer_dest} from {viewer_src}")

    try:
        with zipfile.ZipFile(viewer_dest, "w", zipfile.ZIP_DEFLATED) as zipf:
            for file_path in viewer_src.rglob("*"):
                if file_path.is_file():
                    # Get path relative to ViverseViewer/ directory
                    arcname = file_path.relative_to(viewer_src)
                    # Normalize to forward slashes for cross-platform compatibility
                    arcname = str(arcname).replace("\\", "/")
                    zipf.write(file_path, arcname)

        file_size_mb = viewer_dest.stat().st_size / (1024 * 1024)
        print(f"Successfully generated ViverseViewer.bytes ({file_size_mb:.2f} MB)")

    except Exception as e:
        # Clean up partial file if creation failed
        if viewer_dest.exists():
            viewer_dest.unlink()
        raise Exception(f"Failed to generate ViverseViewer.bytes: {e}") from e


if __name__ == "__main__":
    # Allow running directly for testing
    import sys

    root = sys.argv[1] if len(sys.argv) > 1 else "."
    generate_viverseviewer_bytes(root)
