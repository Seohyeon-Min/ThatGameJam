using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Web build size: caps the WebGL max size of textures that hold little detail for how small they appear on screen,
// and turns on animation compression for the animal FBXs that had it off. Only the WebGL platform override of each
// texture changes, so the source files and other platforms stay as they are. Running it again changes nothing.
// Run: unity command script run --file AgentScripts/ApplyWebImportSettings.cs --entry ApplyWebImportSettings.Run
public static class ApplyWebImportSettings
{
    static readonly (string path, int maxSize)[] Textures =
    {
        ("Assets/01_Arts/Models/Girl/Textures/NPC-Girl-Head_Clean.png", 512),
        ("Assets/01_Arts/Models/Girl/Textures/Neck_Clean.png", 64),
        ("Assets/01_Arts/Models/Girl/Textures/Arm_Mitten_L_Clean.png", 256),
        ("Assets/01_Arts/Models/Girl/Textures/Arm_Mitten_R_Clean.png", 256),
        ("Assets/01_Arts/Models/Girl/Textures/Legs_8sided_Clean.png", 256),
        ("Assets/01_Arts/Mesh/icy_tree/texture/Tube_6_color_1.png", 256),
        ("Assets/01_Arts/Mesh/icy_tree/texture/Tube_color.png", 256),
        ("Assets/01_Arts/Mesh/icy_tree/texture/Tube_9_color_1.png", 256),
        ("Assets/01_Arts/Mesh/cornelian_cherry/berries/Tube_color_4.png", 512),
        ("Assets/01_Arts/Mesh/cornelian_cherry/tree_base/UV_Sphere_2_color_6.png", 512),
        ("Assets/01_Arts/Mesh/thickTree/Tube_4_color.png", 512),
        ("Assets/01_Arts/Mesh/grass01/pPlane10_color_3.png", 512),
        ("Assets/01_Arts/Mesh/grass01/grass01_vertex/vertex_color_edit.png", 512),
        ("Assets/01_Arts/Mesh/flower01/Tube_2_color.png", 512),
        ("Assets/01_Arts/Mesh/flower02/Tube_2_color_1.png", 512),
        ("Assets/01_Arts/Mesh/flower03/Tube_11_color.png", 512),
        ("Assets/01_Arts/Texture/fox_temp.png", 512),
        // 1790x1012 is not a multiple of 4, so it cannot be block-compressed and stays RGBA32; a smaller cap is what shrinks it.
        ("Assets/01_Arts/Textures/UI/Encyclopedia/EncyclopediaBase.png", 1024),
    };

    static readonly string[] AnimatedModels =
    {
        "Assets/01_Arts/Models/Rabbits/Rabbit-Animated.fbx",
        "Assets/01_Arts/Models/Rabbits/BabyRabbit-Animated.fbx",
        "Assets/01_Arts/Models/Bird/Bird-Animated.fbx",
        "Assets/01_Arts/Models/Squirrel/Squirrel-Animated.fbx",
    };

    public static string Run()
    {
        var changed = new List<string>();
        foreach (var (path, maxSize) in Textures)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
            if (web.overridden && web.maxTextureSize == maxSize)
                continue;
            TextureImporterPlatformSettings source = importer.GetDefaultPlatformTextureSettings();
            web.overridden = true;
            web.maxTextureSize = maxSize;
            web.format = TextureImporterFormat.Automatic;
            web.textureCompression = source.textureCompression;
            web.crunchedCompression = source.crunchedCompression;
            web.compressionQuality = source.compressionQuality;
            web.resizeAlgorithm = source.resizeAlgorithm;
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
            changed.Add(System.IO.Path.GetFileName(path));
        }
        foreach (string path in AnimatedModels)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.animationCompression == ModelImporterAnimationCompression.Optimal)
                continue;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.SaveAndReimport();
            changed.Add(System.IO.Path.GetFileName(path));
        }
        return $"changed {changed.Count}: {string.Join(", ", changed)}";
    }
}
