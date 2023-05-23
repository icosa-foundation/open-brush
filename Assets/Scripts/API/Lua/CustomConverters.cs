using System;
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

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Vector2),
            dynVal => ((Vector2ApiWrapper)dynVal.ToObject())._Vector2);

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector2>((script, vector) => UserData.Create(
            new Vector2ApiWrapper(vector)));


        // Vector3

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Vector3),
            dynVal =>
            {
                try
                {
                    return ((Vector3ApiWrapper)dynVal.ToObject())._Vector3;
                }
                catch (InvalidCastException e)
                {
                    // Also accept Vector2 in place of Vector3
                    var pos2D = (Vector2ApiWrapper)dynVal.ToObject();
                    return TrTransform.T(pos2D._Vector2);
                }
            });

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector3>((script, vector) => UserData.Create(
            new Vector3ApiWrapper(vector)));

        // Quaternion

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Quaternion),
            dynVal => ((RotationApiWrapper)dynVal.ToObject())._Quaternion);

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Quaternion>((script, quaternion) => UserData.Create(
            new RotationApiWrapper(quaternion)));

        // TrTransform

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(TrTransform),
            dynVal =>
            {
                try
                {
                    var tr = (TransformApiWrapper)dynVal.ToObject();
                    return tr._TrTransform;
                }
                catch (InvalidCastException e)
                {
                    try
                    {
                        // If it wasn't a TrTransform then hopefully it was a Vector3 as shorthand
                        var pos = (Vector3ApiWrapper)dynVal.ToObject();
                        return TrTransform.T(pos._Vector3);
                    }
                    catch (InvalidCastException e2)
                    {
                        // Finally - try Vector2
                        var pos = (Vector2ApiWrapper)dynVal.ToObject();
                        return TrTransform.T(pos._Vector2);
                    }
                }
            });

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<TrTransform>((script, trTransform) => UserData.Create(
            new TransformApiWrapper(trTransform)));

        // Color

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.UserData, typeof(Color),
            dynVal => ((ColorApiWrapper)dynVal.ToObject())._Color);

        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Color>((script, color) => UserData.Create(
            new ColorApiWrapper(color)));

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(List<Color>),
            dynVal =>
            {
                return dynVal.Table.Values.Select(x => x.ToObject<Color>()).ToList();
            }
        );
    }
}