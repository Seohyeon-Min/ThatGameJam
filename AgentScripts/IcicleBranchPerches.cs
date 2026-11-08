
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// The icicle xylophone: its eight icicles hang from two branches. This replaces the eight perches (one per icicle) with two,
// one per branch, so the bird pecks every icicle under a branch from one spot. Each branch's perch lists its icicles' colliders
// in Clicked Through (a click on one sends the bird there), and each icicle's Perch becomes its branch.
// The new perch point takes the pose of the old per-icicle perch point nearest the middle of that branch's old points.
// unity command script run --file AgentScripts/IcicleBranchPerches.cs --entry IcicleBranchPerches.Run
public static class IcicleBranchPerches
{
    const string PrefabPath = "Assets/03_Prefabs/EasterEggs/IcicleXylophone.prefab";
    // Under the model's branch "Tube" (x 0 .. 2.7) and "Tube 6" (x -2.7 .. 0.2).
    static readonly (string name, string[] icicles)[] Branches =
    {
        ("Branch_Right", new[] { "Cylinder", "Cylinder 1", "Cylinder 2", "Cylinder 3" }),
        ("Branch_Left", new[] { "Cylinder 4", "Cylinder 5", "Cylinder 6", "Cylinder 7" }),
    };

    public static string Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var log = new StringBuilder();
        Transform holder = root.transform.Find("icy tree/Cylinder 8");
        var xylophone = root.GetComponent<IcicleXylophone>();
        var icicles = new SerializedObject(xylophone).FindProperty("icicles");
        var so = icicles.serializedObject;

        foreach (var (name, names) in Branches)
        {
            Collider[] bodies = names.Select(n => holder.Find(n).GetComponent<Collider>()).ToArray();
            Transform[] oldPoints = bodies.Select(b => b.GetComponent<Perchable>().PerchPoint).ToArray();
            Vector3 middle = oldPoints.Aggregate(Vector3.zero, (sum, p) => sum + p.position) / oldPoints.Length;
            Transform pose = oldPoints.OrderBy(p => (p.position - middle).sqrMagnitude).First();

            var branch = new GameObject(name);
            branch.layer = holder.gameObject.layer;
            branch.transform.SetParent(holder, false);
            var point = new GameObject("PerchPoint").transform;
            point.SetParent(branch.transform, false);
            point.SetPositionAndRotation(pose.position, pose.rotation);

            var perch = branch.AddComponent<Perchable>();
            var p = new SerializedObject(perch);
            p.FindProperty("perchPoint").objectReferenceValue = point;
            p.FindProperty("alwaysExempt").boolValue = true;
            p.FindProperty("alignBird").boolValue = true;
            p.FindProperty("faceForward").boolValue = true;
            var through = p.FindProperty("clickedThrough");
            through.arraySize = bodies.Length;
            for (int i = 0; i < bodies.Length; i++)
                through.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            p.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < icicles.arraySize; i++)
            {
                var entry = icicles.GetArrayElementAtIndex(i);
                if (bodies.Contains(entry.FindPropertyRelative("body").objectReferenceValue as Collider))
                    entry.FindPropertyRelative("perch").objectReferenceValue = perch;
            }
            log.AppendLine($"{name}: perch point {point.position.ToString("F3")} (from {pose.name} of {pose.parent?.name}), icicles {string.Join(", ", names)}");

            foreach (Collider body in bodies)
            {
                var old = body.GetComponent<Perchable>();
                Object.DestroyImmediate(old.PerchPoint.gameObject);
                Object.DestroyImmediate(old);
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return log.ToString();
    }
}