using UnityEngine;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using TiltBrush;

/// <summary>
/// These are custom converters for Vector2 and Vector3 structs.
///
/// Add the following call to your code:
/// LuaCustomConverters.RegisterAll();
/// 
/// To create Vector2 in lua:
/// position = {1.0, 1.0}
/// 
/// To Vector3 in lua:
/// position = {1.0, 1.0, 1.0}
///
/// Original:
/// https://gist.github.com/marcinjackowiak/24e01314ce9956487515dcb328fa0877
/// </summary>
public static class LuaCustomConverters
{

    public static void RegisterAll()
    {

        // Vector 2

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector2),
            dynVal =>
            {
                Table table = dynVal.Table;
                float x, y;
                if (table.Keys.First().Type == DataType.String)
                {
                    x = (float)table.Get("x").Number;
                    y = (float)table.Get("y").Number;
                }
                else
                {
                    x = (float)table.Get(1).Number;
                    y = (float)table.Get(2).Number;
                }
                return new Vector2(x, y);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector2>(
            (script, vector) =>
            {
                DynValue x = DynValue.NewNumber(vector.x);
                DynValue y = DynValue.NewNumber(vector.y);
                DynValue dynVal = DynValue.NewTable(script, new DynValue[] { });
                dynVal.Table.Set("x", x);
                dynVal.Table.Set("y", y);
                return dynVal;
            }
        );

        // Vector3

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector3),
            dynVal =>
            {
                Table table = dynVal.Table;
                float x, y, z;
                if (table.Keys.First().Type == DataType.String)
                {
                    // Named properties
                    x = (float)table.Get("x").Number;
                    y = (float)table.Get("y").Number;
                    z = (float)table.Get("z").Number;
                }
                else
                {
                    // Indexed properties
                    x = (float)table.Get(1).Number;
                    y = (float)table.Get(2).Number;
                    z = (float)table.Get(3).Number;
                }
                return new Vector3(x, y, z);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector3>(
            (script, vector) =>
            {
                DynValue x = DynValue.NewNumber(vector.x);
                DynValue y = DynValue.NewNumber(vector.y);
                DynValue z = DynValue.NewNumber(vector.z);
                DynValue dynVal = DynValue.NewTable(script, new DynValue[] { });
                dynVal.Table.Set("x", x);
                dynVal.Table.Set("y", y);
                dynVal.Table.Set("z", z);
                return dynVal;
            }
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(TrTransform),
            dynVal =>
            {
                Table table = dynVal.Table;
                Vector3 position, rotation;
                float scale;

                if (table.Keys.First().Type == DataType.String)
                {
                    // Named properties
                    var t = table.Get("position");
                    var r = table.Get("rotation");
                    var s = table.Get("scale");
                    position = Equals(t, DynValue.Nil) ? t.ToObject<Vector3>() : Vector3.zero;
                    rotation = Equals(r, DynValue.Nil) ? r.ToObject<Vector3>() : Vector3.zero;
                    scale = Equals(s, DynValue.Nil) ? (float)s.Number : 1f;
                }
                else
                {
                    // Indexed properties
                    position = table.Get(1).ToObject<Vector3>();
                    rotation = table.Length > 2 ? table.Get(2).ToObject<Vector3>() : Vector3.zero;
                    scale = table.Length > 3 ? (float)table.Get(3).Number : 1f;
                }

                var tr = TrTransform.TRS(
                    position,
                    Quaternion.Euler(rotation),
                    scale
                );
                return tr;
            }
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(List<TrTransform>),
            dynVal =>
            {
                return dynVal.Table.Values.Select(x => x.ToObject<TrTransform>()).ToList();
            }
        );

    }

}