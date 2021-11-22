// Copyright 2020 The Tilt Brush Authors
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{

    public class GroupManager
    {
        public const uint kIdSketchGroupTagNone = 0;

        private UInt32 m_nextUnusedCookie;
        private Dictionary<UInt32, SketchGroupTag> m_idToGroup;

        public GroupManager()
        {
            ResetGroups();
        }

        public SketchGroupTag NewUnusedGroup()
        {
            UInt32 cookie = m_nextUnusedCookie++;
            return new SketchGroupTag(cookie);
        }

        /// Converts a serialized id to a SketchGroupTag.
        /// This is the inverse of what GroupIdMapping does.
        public SketchGroupTag GetGroupFromId(UInt32 id)
        {
            if (id == kIdSketchGroupTagNone)
            {
                return SketchGroupTag.None;
            }
            else if (m_idToGroup.TryGetValue(id, out SketchGroupTag tag))
            {
                return tag;
            }
            if (id >= m_nextUnusedCookie)
            {
                m_nextUnusedCookie = id + 1;
            }
            return m_idToGroup[id] = new SketchGroupTag(id);
        }

        /// This should be called when the strokes have been cleared.
        public void ResetGroups()
        {
            m_nextUnusedCookie = 1;
            m_idToGroup = new Dictionary<UInt32, SketchGroupTag>();
        }
        
        public static void MoveStrokesToNewGroups(List<Stroke> strokes, Dictionary<int, int> oldGroupToNewGroup)
        {
            foreach (var stroke in strokes ?? new List<Stroke>())
            {
                if (stroke.Group != SketchGroupTag.None)
                {
                    if (!oldGroupToNewGroup.ContainsKey(stroke.Group.GetHashCode()))
                    {
                        var newGroup = App.GroupManager.NewUnusedGroup();
                        oldGroupToNewGroup.Add(stroke.Group.GetHashCode(), newGroup.GetHashCode());
                    }
                    stroke.Group = new SketchGroupTag((uint)oldGroupToNewGroup[stroke.Group.GetHashCode()]);
                }
            }
        }
        
        public static void MoveWidgetsToNewGroups(List<GrabWidget> widgets, Dictionary<int, int> oldGroupToNewGroup)
        {
            foreach (var widget in widgets ?? new List<GrabWidget>())
            {
                if (widget.Group != SketchGroupTag.None)
                {
                    if (!oldGroupToNewGroup.ContainsKey(widget.Group.GetHashCode()))
                    {
                        var newGroup = App.GroupManager.NewUnusedGroup();
                        oldGroupToNewGroup.Add(widget.Group.GetHashCode(), newGroup.GetHashCode());
                    }
                    widget.Group = new SketchGroupTag((uint)oldGroupToNewGroup[widget.Group.GetHashCode()]);
                }
            }
        }
        
        public static void UpdateWidgetJsonToNewGroups(SketchMetadata jsonData, Dictionary<int, int> oldGroupToNewGroup)
        {
            foreach (TiltVideo video in jsonData.Videos ?? Array.Empty<TiltVideo>())
            {
                if (video.GroupId != 0)
                {
                    int currentGroup = (int)video.GroupId;
                    int newGroup = currentGroup;
                    if (!oldGroupToNewGroup.ContainsKey(currentGroup))
                    {
                        newGroup = App.GroupManager.NewUnusedGroup().GetHashCode();
                        oldGroupToNewGroup.Add(currentGroup, newGroup);
                    }
                    video.GroupId = (uint)newGroup.GetHashCode();
                }
            }
            
            foreach (TiltImages75 image in jsonData.ImageIndex ?? Array.Empty<TiltImages75>())
            {
                uint[] newGroupIds = (uint[]) image.GroupIds.Clone();
                for (var i = 0; i < image.GroupIds.Length; i++)
                {
                    int currentGroupId = (int)image.GroupIds[i];
                    if (!oldGroupToNewGroup.ContainsKey(currentGroupId))
                    {
                        var newGroup = App.GroupManager.NewUnusedGroup();
                        oldGroupToNewGroup.Add(currentGroupId, newGroup.GetHashCode());
                        newGroupIds[i] = (uint)newGroup.GetHashCode();
                    }
                }
                image.GroupIds = newGroupIds;
            }

            foreach (Guides guide in jsonData.GuideIndex ?? Array.Empty<Guides>())
            {
                foreach (var state in guide.States)
                {
                    if (state.GroupId != 0)
                    {
                        int currentGroup = (int)state.GroupId;
                        int newGroup = currentGroup;
                        if (!oldGroupToNewGroup.ContainsKey(currentGroup))
                        {
                            newGroup = App.GroupManager.NewUnusedGroup().GetHashCode();
                            oldGroupToNewGroup.Add(currentGroup, newGroup);
                        }
                        state.GroupId = (uint)newGroup.GetHashCode();
                    }
                }
            }
            
            foreach (TiltModels75 model in jsonData.ModelIndex ?? Array.Empty<TiltModels75>())
            {
                uint[] newGroupIds = (uint[]) model.GroupIds.Clone();
                for (var i = 0; i < model.GroupIds.Length; i++)
                {
                    int currentGroupId = (int)model.GroupIds[i];
                    if (!oldGroupToNewGroup.ContainsKey(currentGroupId))
                    {
                        var newGroup = App.GroupManager.NewUnusedGroup();
                        oldGroupToNewGroup.Add(currentGroupId, newGroup.GetHashCode());
                        newGroupIds[i] = (uint)newGroup.GetHashCode();
                    }
                }
                model.GroupIds = newGroupIds;
            }
            
        }

        public static void MoveToNewGroups(List<Stroke> strokes, List<GrabWidget> widgets)
        {
            var oldGroupToNewGroup = new Dictionary<int, int>();
            MoveStrokesToNewGroups(strokes, oldGroupToNewGroup);
            MoveWidgetsToNewGroups(widgets, oldGroupToNewGroup);
        }
    }

    /// A plain old int, with a wrapper type for type-safety
    public struct SketchGroupTag : IEquatable<SketchGroupTag>
    {
        static public SketchGroupTag None = new SketchGroupTag(0);

        // An opaque value
        private readonly UInt32 cookie;

        // Only for use by GroupManager
        public SketchGroupTag(UInt32 cookie)
        {
            this.cookie = cookie;
        }

        public override bool Equals(object obj)
        {
            if (obj is SketchGroupTag)
            {
                return this.Equals((SketchGroupTag)obj);
            }
            return false;
        }

        public bool Equals(SketchGroupTag tag)
        {
            return (this.cookie == tag.cookie);
        }

        public override int GetHashCode()
        {
            return (int)this.cookie;
        }

        public static bool operator ==(SketchGroupTag lhs, SketchGroupTag rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(SketchGroupTag lhs, SketchGroupTag rhs)
        {
            return !(lhs.Equals(rhs));
        }

        // C# will "helpfully" allow == and != null comparisons (for subtle and surprising reasons)
        // Make sure these generate compile warnings.

        [Obsolete]
        public static bool operator ==(SketchGroupTag lhs, String rhs)
        {
            throw new InvalidOperationException();
        }

        [Obsolete]
        public static bool operator !=(SketchGroupTag lhs, String rhs)
        {
            throw new InvalidOperationException();
        }
    }

    /// This generates a mapping from groups to consecutive ids.
    public class GroupIdMapping
    {
        private Dictionary<SketchGroupTag, uint> m_GroupToConsecutiveIdMapping =
            new Dictionary<SketchGroupTag, uint>();
        private uint m_NextId = GroupManager.kIdSketchGroupTagNone + 1;
        public uint GetId(SketchGroupTag group)
        {
            if (group == SketchGroupTag.None)
            {
                return GroupManager.kIdSketchGroupTagNone;
            }
            if (!m_GroupToConsecutiveIdMapping.TryGetValue(group, out uint id))
            {
                m_GroupToConsecutiveIdMapping[group] = id = m_NextId++;
            }
            return id;
        }
    }
    

} // namespace TiltBrush
