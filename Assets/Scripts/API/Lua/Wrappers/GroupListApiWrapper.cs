using System.Collections.Generic;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("A list of Groups")]
    [MoonSharpUserData]
    public class GroupListApiWrapper
    {
        [MoonSharpHidden]
        public List<GroupApiWrapper> _Groups;

        [LuaDocsDescription("Returns the last layer")]
        public GroupApiWrapper last => _Groups[^1];

        public GroupListApiWrapper()
        {
            _Groups = new List<GroupApiWrapper>();
        }

        public GroupListApiWrapper(List<GroupApiWrapper> groups)
        {
            _Groups = groups;
        }

        [LuaDocsDescription("Returns the group at the given index")]
        public GroupApiWrapper this[int index] => _Groups[index];

        [LuaDocsDescription("The number of layers")]
        public int count => _Groups?.Count ?? 0;
    }
}

