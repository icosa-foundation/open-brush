using UnityEngine;
using MoonSharp.Interpreter;
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

        // Quaternion

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Quaternion),
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
                return Quaternion.Euler(x, y, z);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Quaternion>(
            (script, quaternion) =>
            {
                DynValue x = DynValue.NewNumber(quaternion.eulerAngles.x);
                DynValue y = DynValue.NewNumber(quaternion.eulerAngles.y);
                DynValue z = DynValue.NewNumber(quaternion.eulerAngles.z);
                DynValue dynVal = DynValue.NewTable(script, new DynValue[] { });
                dynVal.Table.Set("x", x);
                dynVal.Table.Set("y", y);
                dynVal.Table.Set("z", z);
                return dynVal;
            }
        );

        // TrTransform

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(TrTransform),
            dynVal =>
            {
                Table table = dynVal.Table;
                Vector3 position, rotation;
                float scale;

                var firstKey = table.Keys.First();
                if (firstKey.Type == DataType.String)
                {
                    // Named properties
                    var t = table.Get("position");
                    var r = table.Get("rotation");
                    var s = table.Get("scale");
                    position = Equals(t, DynValue.Nil) ? Vector3.zero : t.ToObject<Vector3>();
                    rotation = Equals(r, DynValue.Nil) ? Vector3.zero : r.ToObject<Vector3>();
                    scale = Equals(s, DynValue.Nil) ? 1f : (float)s.Number;
                }
                else
                {
                    // Indexed properties
                    position = table.Get(1).ToObject<Vector3>();
                    rotation = table.Length > 1 ? table.Get(2).ToObject<Vector3>() : Vector3.zero;
                    scale = table.Length > 2 ? (float)table.Get(3).Number : 1f;
                }

                var tr = TrTransform.TRS(
                    position,
                    Quaternion.Euler(rotation),
                    scale
                );
                return tr;
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<TrTransform>(
            (script, tr) =>
            {
                DynValue dynVal = DynValue.NewTable(script, new DynValue[] { });
                dynVal.Table.Set("position", DynValue.FromObject(script, tr.translation));
                dynVal.Table.Set("rotation", DynValue.FromObject(script, tr.rotation.eulerAngles));
                dynVal.Table.Set("scale", DynValue.FromObject(script, tr.scale));
                return dynVal;
            }
        );

        // List<TrTransform>

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(List<TrTransform>),
            dynVal =>
            {
                return dynVal.Table.Values.Select(x => x.ToObject<TrTransform>()).ToList();
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<List<TrTransform>>(
            (script, list) => DynValue.NewTable(script,
                list.Select(el => DynValue.FromObject(script, el)).ToArray()
        ));

        // Color

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Color),
            dynVal =>
            {
                Table table = dynVal.Table;
                float r, g, b, a;
                if (table.Keys.First().Type == DataType.String)
                {
                    // Named properties
                    r = (float)table.Get("r").Number;
                    g = (float)table.Get("g").Number;
                    b = (float)table.Get("b").Number;
                    a = Equals(table.Get("a"), DynValue.Nil) ? 1 : (float)table.Get("a").Number;
                }
                else
                {
                    // Indexed properties
                    r = (float)table.Get(1).Number;
                    g = (float)table.Get(2).Number;
                    b = (float)table.Get(3).Number;
                    a = Equals(table.Get(4), DynValue.Nil) ? 1 : (float)table.Get(4).Number;
                }
                return new Color(r, g, b, a);
            }
        );

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Color>(
            (script, color) =>
            {
                DynValue r = DynValue.NewNumber(color.r);
                DynValue g = DynValue.NewNumber(color.g);
                DynValue b = DynValue.NewNumber(color.b);
                DynValue a = DynValue.NewNumber(color.a);
                DynValue dynVal = DynValue.NewTable(script, new DynValue[] { });
                dynVal.Table.Set("r", r);
                dynVal.Table.Set("g", g);
                dynVal.Table.Set("b", b);
                dynVal.Table.Set("a", a);
                return dynVal;
            }
        );

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(List<Color>),
            dynVal =>
            {
                return dynVal.Table.Values.Select(x => x.ToObject<Color>()).ToList();
            }
        );
    }
}