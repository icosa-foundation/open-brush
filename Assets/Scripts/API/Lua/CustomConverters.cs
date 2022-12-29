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
                float x = (float)((Double)table["x"]);
                float y = (float)((Double)table["y"]);
                return new Vector2(x, y);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector2>(
            (script, vector) =>
            {
                DynValue x = DynValue.NewNumber((double)vector.x);
                DynValue y = DynValue.NewNumber((double)vector.y);
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
                if (table.Keys.First().String == "x")
                {
                    x = (float)(Double)table["x"];
                    y = (float)(Double)table["y"];
                    z = (float)(Double)table["z"];
                }
                else
                {
                    x = (float)(Double)table[1];
                    y = (float)(Double)table[2];
                    z = (float)(Double)table[3];
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
                if (table.Keys.First().String == "x")
                {
                    position = table.Get("position").ToObject<Vector3>();
                    rotation = table.Get("rotation").ToObject<Vector3>();
                    if (table.Length > 3)
                    {
                        scale = (float)table.Get("rotation").Number;
                    }
                    else
                    {
                        scale = 1f;
                    }
                }
                else
                {
                    position = table.Get(1).ToObject<Vector3>();
                    rotation = table.Get(2).ToObject<Vector3>();
                    if (table.Length > 3)
                    {
                        scale = (float)table.Get(3).Number;
                    }
                    else
                    {
                        scale = 1f;
                    }
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