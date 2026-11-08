using UnityEngine;

// WatercolorManager's generated textures, baked into assets by AgentScripts/BakeWatercolorTextures.cs (ADR 0039).
// The pencil strokes depend on five look settings; the bake is used only while a look has the values it was baked with.
public class WatercolorTextures : ScriptableObject
{
    public const string ResourceName = "WatercolorTextures";

    public Texture2D floorPattern;
    public Texture2D brushDabs;
    public Texture2D pencilStrokes;
    public float strokeWidthVariation, strokeDensity, strokeLengthPercent, strokeAlignment, strokeAngleJitter;

    public (float, float, float, float, float) PencilKey =>
        (strokeWidthVariation, strokeDensity, strokeLengthPercent, strokeAlignment, strokeAngleJitter);
}
