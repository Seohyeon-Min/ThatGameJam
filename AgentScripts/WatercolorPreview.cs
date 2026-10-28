using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Renders a test scene with each watercolor option to PNG files outside Assets/, and times them, for review.
// Opens the scene additively and never saves it, so whatever scene is already open stays untouched.
// Every entry point restores what it changed (renderer feature state, camera post-processing, materials).
//
// effect:   Off / Blur / BlurDepth / Classic / Anisotropic
// quality:  Kuwahara preset (Low / Medium / High); ignored by the other effects
// material: Lit (the scene as authored) / Watercolor (Lit materials temporarily swapped for ThatGameJam/WatercolorLit)
//
// Run (each step separately):
//   unity command script run --file AgentScripts/WatercolorPreview.cs --entry WatercolorPreview.Setup --args '["Assets/00_Scenes/Tommy_Test.unity"]'
//   unity command script run --file AgentScripts/WatercolorPreview.cs --entry WatercolorPreview.Render --args '["<out.png>", "Blur", "High", "Watercolor", 1920, 1080]'
//   unity command script run --file AgentScripts/WatercolorPreview.cs --entry WatercolorPreview.Profile --args '[1920, 1080, 20]'
//   unity command script run --file AgentScripts/WatercolorPreview.cs --entry WatercolorPreview.Teardown
public static class WatercolorPreview
{
    const string VolumeName = "__WatercolorPreviewVolume";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";

    public static string Setup(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.isLoaded)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        if (GameObject.Find(VolumeName) == null)
        {
            var go = new GameObject(VolumeName) { hideFlags = HideFlags.DontSave };
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            profile.Add<WatercolorKuwahara>(true);
            profile.Add<WatercolorBlur>(true);
            volume.sharedProfile = profile;
            SceneManager.MoveGameObjectToScene(go, scene);
            AddDetail(scene);
        }
        return $"opened {scene.path}, camera: {(FindCamera(scene) != null ? FindCamera(scene).name : "none")}";
    }

    public static string Render(string outPath, string effect, string quality, string material, int width, int height)
    {
        using var session = new Session(width, height);
        if (session.camera == null)
            return "no camera in the preview scene";
        session.Apply(effect, quality, material);
        session.RenderOnce();

        var previousActive = RenderTexture.active;
        RenderTexture.active = session.rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = previousActive;
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return $"wrote {outPath} ({effect}, {quality}, {material})";
    }

    // Times camera renders with the GPU drained after each one, so the difference to the first config is the added cost.
    // GPU recorders need frame boundaries, which edit-mode camera.Render() calls do not produce, so wall time is used.
    // Configs are interleaved over several rounds and the fastest round is kept, which filters out editor hitches and
    // GPU clock changes that otherwise swamp a sub-millisecond difference.
    public static string Profile(int width, int height, int iterations)
    {
        var configs = new (string effect, string quality, string material)[]
        {
            ("Off", "High", "Lit"),
            ("Blur", "High", "Lit"),
            ("BlurDepth", "High", "Lit"),
            ("Anisotropic", "Low", "Lit"),
            ("Anisotropic", "Medium", "Lit"),
            ("Anisotropic", "High", "Lit"),
            ("Off", "High", "Watercolor"),
            ("Blur", "High", "Watercolor"),
        };

        using var session = new Session(width, height);
        const int rounds = 7;
        var best = new double[configs.Length];
        for (int c = 0; c < configs.Length; c++)
            best[c] = double.MaxValue;
        for (int round = 0; round < rounds; round++)
        {
            for (int c = 0; c < configs.Length; c++)
            {
                session.Apply(configs[c].effect, configs[c].quality, configs[c].material);
                for (int i = 0; i < 3; i++)
                    session.RenderOnce();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                    session.RenderOnce();
                best[c] = System.Math.Min(best[c], watch.Elapsed.TotalMilliseconds / iterations);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{width}x{height}, best of {rounds} rounds x {iterations} renders");
        for (int c = 0; c < configs.Length; c++)
            sb.AppendLine($"{configs[c].effect,-12}{configs[c].quality,-8}{configs[c].material,-11} frame {best[c]:F2} ms, added {best[c] - best[0]:F2} ms");
        return sb.ToString();
    }

    public static string Teardown()
    {
        var go = GameObject.Find(VolumeName);
        if (go != null)
        {
            Object.DestroyImmediate(go.GetComponent<Volume>().sharedProfile);
            Object.DestroyImmediate(go);
        }
        string closed = "";
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.path != SceneManager.GetActiveScene().path)
            {
                closed += s.path + " ";
                EditorSceneManager.CloseScene(s, true);
            }
        }
        return $"closed {closed}";
    }

    // Holds everything a render temporarily changes and puts it back on Dispose.
    sealed class Session : System.IDisposable
    {
        public readonly Camera camera;
        public readonly RenderTexture rt;
        readonly RenderTexture sync;
        readonly WatercolorKuwahara kuwahara;
        readonly WatercolorBlur blur;
        readonly WatercolorKuwaharaFeature kuwaharaFeature;
        readonly WatercolorQualityPreset previousQuality;
        readonly bool previousKuwaharaActive;
        readonly UniversalAdditionalCameraData cameraData;
        readonly bool previousPost;
        readonly RenderTexture previousTarget;
        readonly List<(Renderer renderer, Material[] original, Material[] watercolor)> swaps = new();
        readonly List<Material> createdMaterials = new();
        bool swapped;

        public Session(int width, int height)
        {
            for (int i = 0; i < SceneManager.sceneCount && camera == null; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.path != SceneManager.GetActiveScene().path)
                    camera = FindCamera(s);
            }
            if (camera == null)
                return;

            var profile = GameObject.Find(VolumeName).GetComponent<Volume>().sharedProfile;
            kuwahara = profile.components.Find(c => c is WatercolorKuwahara) as WatercolorKuwahara;
            blur = profile.components.Find(c => c is WatercolorBlur) as WatercolorBlur;

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            foreach (var f in data.rendererFeatures)
                if (f is WatercolorKuwaharaFeature w)
                    kuwaharaFeature = w;
            previousQuality = kuwaharaFeature.settings.quality;
            previousKuwaharaActive = kuwaharaFeature.isActive;

            cameraData = camera.GetUniversalAdditionalCameraData();
            previousPost = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            sync = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32);
            previousTarget = camera.targetTexture;
            camera.targetTexture = rt;

            BuildWatercolorMaterials(camera.gameObject.scene);
        }

        public void Apply(string effect, string quality, string material)
        {
            bool isKuwahara = effect == "Classic" || effect == "Anisotropic";
            kuwahara.enableFilter.value = isKuwahara;
            kuwahara.filterMode.value = effect == "Classic" ? WatercolorFilterMode.Classic : WatercolorFilterMode.Anisotropic;
            kuwaharaFeature.SetActive(isKuwahara);
            kuwaharaFeature.settings.quality = (WatercolorQualityPreset)System.Enum.Parse(typeof(WatercolorQualityPreset), quality);

            blur.enableBlur.value = effect == "Blur" || effect == "BlurDepth";
            blur.depthAware.value = effect == "BlurDepth";

            bool watercolor = material == "Watercolor";
            if (watercolor != swapped)
            {
                foreach (var (renderer, original, replacement) in swaps)
                    renderer.sharedMaterials = watercolor ? replacement : original;
                swapped = watercolor;
            }
        }

        // Waits for the GPU through a 1x1 copy. Reading back the full target, even a 1x1 region of it, makes D3D12
        // allocate a full-size staging buffer per request that is only freed at frame end; ~1300 of them in one call
        // exhausted memory and crashed the editor (device removed) on 2026-10-27.
        public void RenderOnce()
        {
            camera.Render();
            Graphics.Blit(rt, sync);
            AsyncGPUReadback.Request(sync).WaitForCompletion();
        }

        // Copies colour and texture from each URP Lit / Simple Lit material into a WatercolorLit material.
        void BuildWatercolorMaterials(Scene scene)
        {
            var shader = Shader.Find("ThatGameJam/WatercolorLit");
            var cache = new Dictionary<Material, Material>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var original = renderer.sharedMaterials;
                    var replacement = new Material[original.Length];
                    bool any = false;
                    for (int i = 0; i < original.Length; i++)
                    {
                        var m = original[i];
                        replacement[i] = m;
                        if (m == null || !(m.shader.name.EndsWith("/Lit") || m.shader.name.EndsWith("/Simple Lit")))
                            continue;
                        if (!cache.TryGetValue(m, out var w))
                        {
                            w = new Material(shader) { hideFlags = HideFlags.DontSave, enableInstancing = m.enableInstancing };
                            w.SetColor("_BaseColor", m.GetColor("_BaseColor"));
                            w.SetTexture("_BaseMap", m.GetTexture("_BaseMap"));
                            w.SetTextureScale("_BaseMap", m.GetTextureScale("_BaseMap"));
                            cache[m] = w;
                            createdMaterials.Add(w);
                        }
                        replacement[i] = w;
                        any = true;
                    }
                    if (any)
                        swaps.Add((renderer, original, replacement));
                }
            }
        }

        public void Dispose()
        {
            if (camera == null)
                return;
            foreach (var (renderer, original, _) in swaps)
                renderer.sharedMaterials = original;
            foreach (var m in createdMaterials)
                Object.DestroyImmediate(m);

            camera.targetTexture = previousTarget;
            RenderTexture.active = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            sync.Release();
            Object.DestroyImmediate(sync);
            cameraData.renderPostProcessing = previousPost;
            kuwaharaFeature.settings.quality = previousQuality;
            kuwaharaFeature.SetActive(previousKuwaharaActive);
            kuwahara.enableFilter.value = false;
            blur.enableBlur.value = false;
        }
    }

    // The test scenes are mostly smooth primitives, which an image filter barely changes.
    // Add temporary high-frequency content (foliage cards, noisy ground, flowers) so the effects have something to work on.
    static void AddDetail(Scene scene)
    {
        var root = new GameObject(VolumeName + "_Detail") { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(root, scene);
        var random = new System.Random(7);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.hideFlags = HideFlags.DontSave;
        ground.transform.SetParent(root.transform);
        ground.transform.position = new Vector3(0f, -0.09f, 10f);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var groundTex = new Texture2D(256, 256) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.4f;
                groundTex.SetPixel(x, y, Color.Lerp(new Color(0.35f, 0.5f, 0.2f), new Color(0.75f, 0.7f, 0.45f), n));
            }
        groundTex.Apply();
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
        groundMat.SetTexture("_BaseMap", groundTex);
        groundMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var leafMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/01_Arts/Materials/BillboardLeaf.mat");
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>())
        {
            if (renderer.gameObject.scene != scene || renderer.bounds.center.y < 2.5f || leafMat == null)
                continue;
            Bounds b = renderer.bounds;
            for (int i = 0; i < 40; i++)
            {
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Quad);
                leaf.hideFlags = HideFlags.DontSave;
                Object.DestroyImmediate(leaf.GetComponent<Collider>());
                leaf.transform.SetParent(root.transform);
                leaf.transform.position = b.center + Random3(Range) * b.extents.x * 1.1f;
                leaf.transform.localScale = Vector3.one * Range(0.5f, 1f);
                leaf.GetComponent<Renderer>().sharedMaterial = leafMat;
            }
        }

        var flowerMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave, enableInstancing = true };
        var colors = new[] { new Color(0.9f, 0.3f, 0.3f), new Color(0.95f, 0.85f, 0.3f), new Color(0.6f, 0.4f, 0.9f), Color.white };
        for (int i = 0; i < 250; i++)
        {
            var flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flower.hideFlags = HideFlags.DontSave;
            Object.DestroyImmediate(flower.GetComponent<Collider>());
            flower.transform.SetParent(root.transform);
            float side = random.Next(2) == 0 ? -1f : 1f;
            flower.transform.position = new Vector3(side * Range(1.8f, 9f), 0f, Range(-2f, 22f));
            flower.transform.localScale = Vector3.one * Range(0.12f, 0.3f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colors[random.Next(colors.Length)]);
            var r = flower.GetComponent<Renderer>();
            r.sharedMaterial = flowerMat;
            r.SetPropertyBlock(block);
        }
    }

    static Vector3 Random3(System.Func<float, float, float> range)
    {
        Vector3 v;
        do v = new Vector3(range(-1f, 1f), range(-1f, 1f), range(-1f, 1f));
        while (v.sqrMagnitude > 1f);
        return v;
    }

    static Camera FindCamera(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var camera = root.GetComponentInChildren<Camera>();
            if (camera != null)
                return camera;
        }
        return null;
    }
}
