// Copyright 2021 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System.Collections.Generic;

namespace TiltBrush
{
    public partial class FreePaintTool
    {
        [SerializeField] private List<Transform> m_brushGhosts;

        public class BrushGhost
        {
            public Transform transform;
            public Renderer renderer;

            public BrushGhost(Transform transform)
            {
                this.transform = transform;
                renderer = transform.GetComponent<Renderer>();
            }

            private bool _enabled;
            public bool enabled
            {
                get
                {
                    return _enabled;
                }
                set
                {
                    _enabled = value;
                    renderer.enabled = _enabled;
                }
            }

            public enum PathModeID
            {
                Orbit,
                Trail
            }

            public PathModeID pathMode;
            public TrTransform transformTr;
            public TrTransform goalTr;
            public Quaternion tilt;

            private TrTransform Orbit(float radius)
            {

                TrTransform result = new TrTransform();

                Vector3 spindleAxis = transformTr.rotation * Vector3.up;

                Quaternion spindleRotation = Quaternion.AngleAxis(lerpT * -360, spindleAxis);
                Quaternion radialLookRot = transformTr.rotation;

                Vector3 radialOffset = spindleRotation * radialLookRot * Vector3.forward;
                result.translation = transformTr.translation + radialOffset * radius;

                Quaternion pointerRotation = spindleRotation * radialLookRot * tilt;

                result.rotation = pointerRotation;

                result.scale = transformTr.scale;

                return result;
            }

            private TrTransform Trail()
            {

                return LazyLerp(transformTr, goalTr, lerpT, m_LazyInputTangentMode);
            }

            private float _lerpT;
            public float lerpT
            {
                get
                {
                    return _lerpT;
                }
                set
                {
                    if (value == 1)
                        _lerpT = value;
                    else
                    {
                        _lerpT = value % 1;
                        if (_lerpT < 0)
                            _lerpT += 1;
                    }
                    Update();

                }
            }
            private float _radius;
            public float radius
            {
                get
                {
                    return _radius;
                }
                set
                {
                    _radius = value;
                    Update();
                }
            }


            private void Update()
            {
                TrTransform result;
                switch (pathMode)
                {
                    case PathModeID.Orbit:
                        result = Orbit(radius);

                        result.ToTransform(transform);

                        break;
                    case PathModeID.Trail:
                        result = Trail();

                        result.ToTransform(transform);

                        break;
                    default:
                        break;
                }
            }
        }

        private List<BrushGhost> _brushGhosts;
        private void InitBrushGhosts()
        {
            if (_brushGhosts != null)
                return;

            _brushGhosts = new List<BrushGhost>();

            for (int i = 0; i < m_brushGhosts.Count; i++)
            {
                _brushGhosts.Add(new BrushGhost(m_brushGhosts[i]));
            }
        }

        private void BeginBrushGhosts(BrushGhost.PathModeID pathMode)
        {
            for (int i = 0; i < _brushGhosts.Count; i++)
            {
                _brushGhosts[i].enabled = true;
                _brushGhosts[i].pathMode = pathMode;
            }
        }

        private void EndBrushGhosts()
        {
            for (int i = 0; i < _brushGhosts.Count; i++)
            {
                _brushGhosts[i].enabled = false;
            }
        }

        private float _brushGhostOrbitalRadius;
        private float BrushGhostOrbitalRadius
        {
            get
            {
                return _brushGhostOrbitalRadius;
            }
            set
            {
                if (_brushGhostOrbitalRadius != value)
                {
                    _brushGhostOrbitalRadius = value;
                    for (int i = 0; i < _brushGhosts.Count; i++)
                    {
                        _brushGhosts[i].radius = _brushGhostOrbitalRadius;
                    }
                }

            }
        }

        private TrTransform _brushGhostTransform;
        private TrTransform BrushGhostTransform
        {
            get
            {
                return _brushGhostTransform;
            }
            set
            {
                if (_brushGhostTransform != value)
                {
                    _brushGhostTransform = value;
                    for (int i = 0; i < _brushGhosts.Count; i++)
                    {
                        _brushGhosts[i].transformTr = _brushGhostTransform;
                    }
                }

            }
        }

        private TrTransform _brushGhostGoal;
        private TrTransform BrushGhostGoal
        {
            get
            {
                return _brushGhostGoal;
            }
            set
            {
                if (_brushGhostGoal != value)
                {
                    _brushGhostGoal = value;
                    for (int i = 0; i < _brushGhosts.Count; i++)
                    {
                        _brushGhosts[i].goalTr = _brushGhostGoal;
                    }
                }

            }
        }

        private Quaternion _brushGhostTilt;
        private Quaternion BrushGhostTilt
        {
            get
            {
                return _brushGhostTilt;
            }
            set
            {
                if (_brushGhostTilt != value)
                {
                    _brushGhostTilt = value;
                    for (int i = 0; i < _brushGhosts.Count; i++)
                    {
                        _brushGhosts[i].tilt = _brushGhostTilt;
                    }
                }

            }
        }

        private float _brushGhostLerpT;
        private float BrushGhostLerpT
        {
            get
            {
                return _brushGhostLerpT;
            }
            set
            {
                if (_brushGhostLerpT != value)
                {
                    _brushGhostLerpT = value;

                    switch (_brushGhosts[0].pathMode)
                    {
                        case BrushGhost.PathModeID.Orbit:
                            {
                                float incr = 1f / _brushGhosts.Count;

                                for (int i = 0; i < _brushGhosts.Count; i++)
                                {
                                    _brushGhosts[i].lerpT = i * incr + _brushGhostLerpT;
                                }

                            }
                            break;
                        case BrushGhost.PathModeID.Trail:
                            {
                                // reserve last brushGhost for 1.0 to show final position
                                float incr = 1f / (_brushGhosts.Count - 1);

                                for (int i = 0; i < _brushGhosts.Count - 1; i++)
                                {
                                    _brushGhosts[i].lerpT = i * incr + _brushGhostLerpT;
                                }
                                _brushGhosts[_brushGhosts.Count - 1].lerpT = 1;
                            }

                            break;
                        default:
                            break;
                    }

                }

            }
        }

    }
}
