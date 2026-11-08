using System.Linq;
using UnityEditor;
using UnityEngine;

// Bakes WatercolorManager's generated textures (floor pattern, brush dabs, pencil strokes) into
// Assets/04_Data/Resources/WatercolorTextures.asset, so the game loads them instead of drawing them (ADR 0039).
// The pencil strokes are baked with the looks' stroke settings; every WatercolorLook must share them.
// Rerun after changing those settings or the drawing code. Delete the asset to go back to drawing them at load.
// Run: unity command script run --file AgentScripts/BakeWatercolorTextures.cs --entry BakeWatercolorTextures.Run
public static class BakeWatercolorTextures
{
    const string AssetPath = "Assets/04_Data/Resources/WatercolorTextures.asset";

    public static string Run()
    {
        WatercolorLook[] looks = AssetDatabase.FindAssets("t:WatercolorLook")
            .Select(guid => AssetDatabase.LoadAssetAtPath<WatercolorLook>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        var keys = looks.Select(look => WatercolorManager.PencilKey(look.settings)).Distinct().ToArray();
        if (keys.Length != 1)
            return $"stopped: the {looks.Length} looks have {keys.Length} different pencil stroke settings";
        WatercolorManager.Settings settings = looks[0].settings;

        AssetDatabase.DeleteAsset(AssetPath);
        var baked = ScriptableObject.CreateInstance<WatercolorTextures>();
        baked.strokeWidthVariation = settings.strokeWidthVariation;
        baked.strokeDensity = settings.strokeDensity;
        baked.strokeLengthPercent = settings.strokeLengthPercent;
        baked.strokeAlignment = settings.strokeAlignment;
        baked.strokeAngleJitter = settings.strokeAngleJitter;
        AssetDatabase.CreateAsset(baked, AssetPath);
        baked.floorPattern = Add(WatercolorManager.MakeFloorPatternTexture(true), baked);
        baked.brushDabs = Add(WatercolorManager.MakeBrushDabsTexture(true), baked);
        baked.pencilStrokes = Add(WatercolorManager.MakePencilStrokesTexture(settings), baked);
        EditorUtility.SetDirty(baked);
        AssetDatabase.SaveAssets();
        return $"ok: {AssetPath}, pencil {keys[0]}, from {looks.Length} looks";
    }

    // Saved with its pixels; the game drops its memory copy once it is on the GPU.
    static Texture2D Add(Texture2D texture, Object asset)
    {
        texture.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(texture, asset);
        var serialized = new SerializedObject(texture);
        serialized.FindProperty("m_IsReadable").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return texture;
    }
}
