using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CommandTerminal
{
    public abstract class CommandCollectionBase : ScriptableObject
    {
        public const string AssetMenuName = "Command Terminal/";
        public abstract void RegisterCommands();
    }
}
