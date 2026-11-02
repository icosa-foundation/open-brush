//
// Reaktion - An audio reactive animation toolkit for Unity.
//
// Copyright (C) 2013, 2014 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// UAC1002 reports this type's [Serializable] hierarchy as incomplete, because the
// GenericLink<T> and GenericLinkBase base classes lack the attribute while holding all
// the link state (_mode, _reference, _name, _forceUpdate).
//
// In practice Unity only requires [Serializable] on the concrete type and serializes
// inherited fields regardless. Confirmed by reading the serialized data rather than
// assuming: Assets/Prefabs/Pointer_Main.prefab stores
//     reaktor:
//       _mode: 3
//       _reference: {fileID: 0}
//       _name: SystemAudio
// so the link survives correctly and the analyzer is being over-strict here.
//
// A pragma is used rather than an .editorconfig entry because Unity compiles through
// Bee and does not pass the analyzer config through, so severity settings there have
// no effect.
#pragma warning disable UAC1002

using UnityEngine;
using System.Collections;

namespace Reaktion {

// A class used to reference an Injector from Reaktors.
[System.Serializable]
public class InjectorLink : GenericLink<InjectorBase>
{
    // Get a output dB level from the Injector.
    public float DbLevel {
        get {
            return linkedObject ? linkedObject.DbLevel : -1e12f;
        }
    }
}

} // namespace Reaktion
