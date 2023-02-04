#!/usr/bin/env python

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

"""One-off hacky script that goes through brushes to find the shaders they use,
and does a bit of introspection to find brushes that generate double-sided
geometry and also use double-sided (ie, non-culling) shaders.

Also useful as sample code for working with the refgraph."""

import collections
import os
import re
import sys

# Add ../Python to sys.path
sys.path.append(
    os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Python")
)

import unitybuild.refgraph  # noqa: E402 pylint: disable=import-error,wrong-import-position

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")


def dfs_iter(graph, guid):
    """graph: networkx.DiGraph
    guid: node name"""
    seen = set()
    q = collections.deque()
    q.append(guid)
    while q:
        elt = q.pop()
        seen.add(elt)
        yield elt
        q.extend(succ for succ in graph.successors_iter(elt) if succ not in seen)


def shaders_for_brush(rg, g_brush):
    """rg: unitybuild.refgraph.ReferenceGraph
    g_brush: node (brush guid)
    yields nodes for shaders."""
    for g in dfs_iter(rg.g, g_brush):
        try:
            n = rg.guid_to_name[g]
        except KeyError:
            continue
        if n.lower().endswith(".shader"):
            yield g


def cullmodes_for_brush(rg, g_brush):
    """rg: unitybuild.refgraph.ReferenceGraph
    g_brush: node (brush guid)
    Returns list of culling modes used by shaders for that brush."""
    modes = set()
    for g_shader in shaders_for_brush(rg, g_brush):
        for mode in cullmodes_for_shader(rg.guid_to_name[g_shader]):
            modes.add(mode)
    return sorted(modes, key=lambda m: m.lower)


def cullmodes_for_shader(shader, memo=None):
    """shader: name of shader asset
    Returns list of culling modes used by the shader."""
    # This is to replace a risky default value, but it looks like this needed a parameter from the caller, or, failing that, a global. FIXME
    if memo is None:
        memo = {}
    try:
        return memo[shader]
    except KeyError:
        pass
    with open(os.path.join(BASE, shader)) as f:
        txt = f.read()
    culls = [m.group(1) for m in re.finditer(r"cull\s+(\w+)", txt, re.I | re.M)]
    memo[shader] = culls
    return culls


def is_brush_doublesided(rg, g_brush):
    """rg: unitybuild.refgraph.ReferenceGraph
    g_brush: node (brush guid)
    Returns True if brush generates doublesided geometry."""
    filename = rg.guid_to_name[g_brush]
    with open(os.path.join(BASE, filename)) as f:
        txt = f.read()
    return int(re.search(r"m_RenderBackfaces: (.)", txt).group(1))


def main():
    rg = unitybuild.refgraph.ReferenceGraph(BASE)
    g2n = rg.guid_to_name

    def is_brush(guid):
        try:
            name = g2n[guid]
        except KeyError:
            return False
        return re.search(r"Brush.*asset$", name) is not None

    brushes = [node for node in rg.g.nodes_iter() if is_brush(node)]
    for g_brush in sorted(brushes, key=g2n.get):
        culls = cullmodes_for_brush(rg, g_brush)
        if len(culls) > 0 and is_brush_doublesided(rg, g_brush):
            print("Brush %s\n is double-sided but has cull %s" % (g2n[g_brush], culls))


if __name__ == "__main__":
    main()
