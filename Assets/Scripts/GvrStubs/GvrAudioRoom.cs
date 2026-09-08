using UnityEngine;

public class GvrAudioRoom : MonoBehaviour
{
    /// Material type that determines the acoustic properties of a room surface.
    public enum SurfaceMaterial
    {
        Transparent = 0,
        ///< Transparent
        AcousticCeilingTiles = 1,
        ///< Acoustic ceiling tiles
        BrickBare = 2,
        ///< Brick, bare
        BrickPainted = 3,
        ///< Brick, painted
        ConcreteBlockCoarse = 4,
        ///< Concrete block, coarse
        ConcreteBlockPainted = 5,
        ///< Concrete block, painted
        CurtainHeavy = 6,
        ///< Curtain, heavy
        FiberglassInsulation = 7,
        ///< Fiberglass insulation
        GlassThin = 8,
        ///< Glass, thin
        GlassThick = 9,
        ///< Glass, thick
        Grass = 10,
        ///< Grass
        LinoleumOnConcrete = 11,
        ///< Linoleum on concrete
        Marble = 12,
        ///< Marble
        Metal = 13,
        ///< Galvanized sheet metal
        ParquetOnConcrete = 14,
        ///< Parquet on concrete
        PlasterRough = 15,
        ///< Plaster, rough
        PlasterSmooth = 16,
        ///< Plaster, smooth
        PlywoodPanel = 17,
        ///< Plywood panel
        PolishedConcreteOrTile = 18,
        ///< Polished concrete or tile
        Sheetrock = 19,
        ///< Sheetrock
        WaterOrIceSurface = 20,
        ///< Water or ice surface
        WoodCeiling = 21,
        ///< Wood ceiling
        WoodPanel = 22 ///< Wood panel
    }

    /// Room surface material in negative x direction.
    public SurfaceMaterial leftWall = SurfaceMaterial.ConcreteBlockCoarse;

    /// Room surface material in positive x direction.
    public SurfaceMaterial rightWall = SurfaceMaterial.ConcreteBlockCoarse;

    /// Room surface material in negative y direction.
    public SurfaceMaterial floor = SurfaceMaterial.ParquetOnConcrete;

    /// Room surface material in positive y direction.
    public SurfaceMaterial ceiling = SurfaceMaterial.PlasterRough;

    /// Room surface material in negative z direction.
    public SurfaceMaterial backWall = SurfaceMaterial.ConcreteBlockCoarse;

    /// Room surface material in positive z direction.
    public SurfaceMaterial frontWall = SurfaceMaterial.ConcreteBlockCoarse;

    /// Reflectivity scalar for each surface of the room.
    public float reflectivity = 1.0f;

    /// Reverb gain modifier in decibels.
    public float reverbGainDb = 0.0f;

    /// Reverb brightness modifier.
    public float reverbBrightness = 0.0f;

    /// Reverb time modifier.
    public float reverbTime = 1.0f;

    /// Size of the room (normalized with respect to scale of the game object).
    public Vector3 size = Vector3.one;
}
