#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// One-off (deletes itself): the rabbits are painted as characters, like the girl. Their baked watercolour materials take the girl's
// Painted As = Character (128, the character stencil bit) and her Self Shadow Skip (0.8).
static class OneOffRabbitCharacter
{
    const string Tag = "[OneOffRabbitCharacter] ";

    static readonly string[] Paths =
    {
        "Assets/01_Arts/Baked/Shared/BabyRabbit_a72dac69/Rabbit_White_watercolor.mat",
        "Assets/01_Arts/Baked/Shared/Rabbit_3df976d2/Rabbit_White_watercolor.mat",
    };

    [InitializeOnLoadMethod]
    static void Schedule() => EditorApplication.delayCall += Run;

    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        try
        {
            var girl = AssetDatabase.LoadAssetAtPath<Material>("Assets/01_Arts/Baked/Shared/Baby_085a6a0c/Dress_watercolor.mat");
            float character = girl.GetFloat("_Character");
            float skip = girl.GetFloat("_SelfShadowSkip");
            foreach (string path in Paths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                mat.SetFloat("_Character", character);
                mat.SetFloat("_SelfShadowSkip", skip);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            foreach (string path in Paths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                Debug.Log(Tag + path + ": Character " + mat.GetFloat("_Character") + ", SelfShadowSkip " + mat.GetFloat("_SelfShadowSkip"));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(Tag + "failed: " + e);
            return;
        }
        AssetDatabase.DeleteAsset("Assets/02_Scripts/Tools/OneOffRabbitCharacter.cs");
    }
}
#endif
