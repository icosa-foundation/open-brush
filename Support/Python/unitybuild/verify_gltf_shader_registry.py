"""Reject Android builds missing the shader-name entries used by UnityGLTF.

The failing builds contained the compiled shaders and valid material references,
but their ScriptMapper name index omitted both shaders, making Shader.Find fail.
Adding only those entries to a test APK restored UnityGLTF imports on the device.
The Editor prebuild hook now registers the shaders explicitly; check its actual
serialized output because successful compilation or Editor lookup alone cannot
prove runtime name lookup will work. Keep this regression check while the reason
normal Shader Graph registration was missing remains unknown. This checks lookup
metadata, not rendered appearance or complete shader variant coverage.

Requires UnityPy==1.25.3. Reads the built APK/AAB without modifying it.
"""

import argparse
from pathlib import Path
import zipfile

import UnityPy


PREFIX = "[OB_GLTF_SHADER_REGISTRY_20261007]"
REQUIRED_NAMES = ("UnityGLTF/PBRGraph", "UnityGLTF/UnlitGraph")


def verify(artifact: Path) -> None:
    root = "base/assets/bin/Data" if artifact.suffix.lower() == ".aab" else "assets/bin/Data"
    with zipfile.ZipFile(artifact) as archive:
        stem = f"{root}/globalgamemanagers"
        if stem in archive.namelist():
            data = archive.read(stem)
        else:
            parts = sorted(
                (name for name in archive.namelist() if name.startswith(f"{stem}.split")),
                key=lambda name: int(name.rsplit(".split", 1)[1]),
            )
            if not parts:
                raise ValueError(f"Missing {stem}")
            if [int(name.rsplit(".split", 1)[1]) for name in parts] != list(range(len(parts))):
                raise ValueError(f"Incomplete split metadata for {stem}")
            data = b"".join(archive.read(name) for name in parts)

    environment = UnityPy.Environment()
    environment.load_file(data, name="globalgamemanagers")
    mappers = [obj for obj in environment.objects if obj.type.name == "ScriptMapper"]
    if len(mappers) != 1:
        raise ValueError(f"Expected one ScriptMapper, found {len(mappers)}")
    entries = mappers[0].parse_as_dict()["m_Shaders"]["m_ObjectToName"]
    for required in REQUIRED_NAMES:
        matches = [pointer for pointer, name in entries if name == required]
        if len(matches) != 1 or matches[0]["m_PathID"] == 0:
            raise ValueError(f"Missing or ambiguous shader-name entry: {required}")
        pointer = matches[0]
        print(f"{PREFIX} shader={required}, fileID={pointer['m_FileID']}, pathID={pointer['m_PathID']}")
    print(f"{PREFIX} PASS artifact={artifact.name}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifact", type=Path, help="APK/AAB, or directory containing Android builds")
    args = parser.parse_args()
    artifacts = (
        sorted(path for path in args.artifact.rglob("*") if path.suffix.lower() in (".apk", ".aab"))
        if args.artifact.is_dir() else [args.artifact]
    )
    if not artifacts:
        parser.exit(1, f"{PREFIX} FAIL no APK/AAB found in {args.artifact}\n")
    try:
        for artifact in artifacts:
            verify(artifact)
    except Exception as error:
        parser.exit(1, f"{PREFIX} FAIL {type(error).__name__}: {error}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
