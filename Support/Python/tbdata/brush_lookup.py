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
# mypy: no-strict-optional

import os
import re
from typing import cast, Dict, Iterator, List, Tuple, NewType
from collections import defaultdict

Guid = NewType("Guid", str)


class BrushLookup:
    """Helper for doing name <-> guid conversions for brushes."""

    def iter_standard_brush_guids(self) -> Iterator[Guid]:
        """Yields all the standard (non-experimental) brush guids"""
        with open(os.path.join(self.dir, "Assets/Manifest.asset")) as inf:
            data = inf.read().replace("\r", "")
        brush_chunk = re.search(r"Brushes:\n(  -.*\n)*", data).group(0)
        for match in re.finditer(r"guid: ([0-9a-f]{32})", brush_chunk):
            yield cast(Guid, match.group(1))

    @staticmethod
    def iter_brush_guid_and_name(tilt_brush_dir: str) -> Iterator[Tuple[Guid, str]]:
        """Yields (guid, name) tuples."""
        for brush_dir in ("Assets/Resources/Brushes", "Assets/Resources/X/Brushes"):
            for r, _, fs in os.walk(os.path.join(tilt_brush_dir, brush_dir)):
                for f in fs:
                    if f.lower().endswith(".asset"):
                        fullf = os.path.join(r, f)
                        with open(fullf) as inf:
                            data = inf.read()
                        guid = cast(
                            Guid, re.search("m_storage: (.*)$", data, re.M).group(1)
                        )
                        # name = re.search('m_Name: (.*)$', data, re.M).group(1)
                        name = f[:-6]
                        yield guid, name

    _instances: Dict[str, "BrushLookup"] = {}

    @classmethod
    def get(cls, tilt_brush_dir=None) -> "BrushLookup":
        if tilt_brush_dir is None:
            tilt_brush_dir = os.path.normpath(
                os.path.join(os.path.abspath(__file__), "../../../..")
            )

        try:
            return cls._instances[tilt_brush_dir]
        except KeyError:
            pass
        val = cls._instances[tilt_brush_dir] = BrushLookup(tilt_brush_dir)
        return val

    def __init__(self, tilt_brush_dir: str):
        self.dir = tilt_brush_dir
        self.initialized = True
        self.guid_to_name = dict(self.iter_brush_guid_and_name(self.dir))
        self.standard_brushes = set(self.iter_standard_brush_guids())
        name_to_guids: Dict[str, List[Guid]] = defaultdict(list)
        for guid, name in self.guid_to_name.items():
            name_to_guids[name].append(guid)
        self.name_to_guids = dict(name_to_guids)

    def get_unique_guid(self, name: str) -> Guid:
        lst = self.name_to_guids[name]
        if len(lst) == 1:
            return lst[0]
        raise LookupError("%s refers to multiple brushes" % name)
