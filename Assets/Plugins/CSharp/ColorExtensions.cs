using UnityEngine;

public static class ColorExtensions
{
    public static Color FadeAlpha(this Color col, float t)
    {
        return new Color(col.r, col.g, col.b, col.a * t);
    }

}

public static class Colors
{
    public static readonly Color darkRed = new Color(0.5f, 0, 0, 1);
    public static readonly Color darkGreen = new Color(0, 0.5f, 0, 1);
    public static readonly Color darkBlue = new Color(0, 0, 0.5f, 1);
    public static readonly Color darkCyan = new Color(0, 0.5f, 0.5f, 1);
    public static readonly Color darkYellow = new Color(0.5f, 0.5f, 0, 1);
    public static readonly Color darkMagenta = new Color(0.5f, 0, 0.5f, 1);
    public static readonly Color orange = new Color(1, 0.5f, 0, 1);
    public static readonly Color lime = new Color(0.75f, 1, 0, 1);
    public static readonly Color mint = new Color(0, 1, 0.5f, 1);
    public static readonly Color teal = new Color(0, 1, 0.75f, 1);
    public static readonly Color skyblue = new Color(0, 0.75f, 1, 1);
    public static readonly Color darkskyblue = new Color(0, 0.5f, 1, 1);
    public static readonly Color purple = new Color(0.5f, 0, 1, 1);
    public static readonly Color lightpurple = new Color(0.75f, 0, 1, 1);
    public static readonly Color fuscia = new Color(1, 0, 0.75f, 1);
    public static readonly Color rose = new Color(1, 0, 0.05f, 1);
}