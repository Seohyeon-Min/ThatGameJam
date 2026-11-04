using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Stage 1 in Main: right after the first fallen log's BalanceBeamEnd (Point_74, z 18.1) the next route point was the leaning log's
// StopPoint (Point_75, z 23.0). Baby plays the segment after a BalanceBeamEnd as one Girl_Jump (Baby.JumpTo, 1.5 s), so she leapt
// all 4.9 m in one hop. This adds a landing point on the ground just past each beam end whose next point is farther than MaxHop:
// the jump becomes a short hop down off the log, and she walks the rest. Nothing else on the route changes.
// Run with Main open in Edit Mode:
// unity command script run --file AgentScripts/FixStage1LogLanding.cs --entry FixStage1LogLanding.Run
// Report only (changes nothing): --entry FixStage1LogLanding.Report
public static class FixStage1LogLanding
{
    const string ScenePath = "Assets/00_Scenes/Main.unity";
    const string StagePath = "02_WORLD/Stages/Stage1";
    // The other hops on Main's routes are 0.3 .. 1.2 m (onto the first log 0.41, onto the leaning log 1.16, off it 0.30).
    const float MaxHop = 1.2f;
    // How far past the beam end she lands, flat. Tried in order until the log no longer reaches there.
    static readonly float[] LandAhead = { 0.4f, 0.6f, 0.8f, 1.0f };

    public static string Run()
    {
        BabyRoute route = Stage1Route();
        var log = new StringBuilder();
        int added = 0;
        for (int i = 0; i < route.Count - 1; i++)
        {
            Transform end = route[i];
            if (!end.TryGetComponent(out BalanceBeamEnd _))
                continue;
            Transform next = route[i + 1];
            Vector3 flat = next.position - end.position;
            flat.y = 0f;
            if (flat.magnitude <= MaxHop)
                continue;

            Vector3 land = LandingPoint(end.position, flat.normalized);
            var point = new GameObject(end.name + "_Land");
            Undo.RegisterCreatedObjectUndo(point, "Fix Stage1 Log Landing");
            point.transform.SetParent(route.transform, false);
            point.transform.position = land;
            point.transform.SetSiblingIndex(i + 1);
            log.AppendLine($"{end.name} {Fmt(end.position)} -> new {point.name} {Fmt(land)}: hop {Hop(end.position, land):0.00} m, "
                + $"then walk {Hop(land, next.position):0.00} m to {next.name} {Fmt(next.position)} (was one {flat.magnitude:0.00} m hop).");
            added++;
            // The new point is a plain walk target; the one after it is the old next point.
            i++;
        }
        if (added == 0)
            return "No hop longer than " + MaxHop + " m after a BalanceBeamEnd in Stage1: nothing changed.\n" + Hops(route);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log + "Saved Main.\n" + Hops(route);
    }

    // Every jump segment on every route in Main, as Baby will play it. Changes nothing.
    public static string Report()
    {
        CheckScene();
        var log = new StringBuilder();
        foreach (BabyRoute route in Object.FindObjectsByType<BabyRoute>(FindObjectsSortMode.None))
            log.Append(route.transform.parent != null ? route.transform.parent.name : route.name).Append(": ").Append(Hops(route));
        return log.ToString();
    }

    static BabyRoute Stage1Route()
    {
        CheckScene();
        GameObject stage = GameObject.Find(StagePath);
        if (stage == null || !stage.TryGetComponent(out Stage component) || component.route == null)
            throw new System.InvalidOperationException($"No Stage with a route at {StagePath}.");
        return component.route;
    }

    static void CheckScene()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new System.InvalidOperationException("Open Main in Edit Mode.");
    }

    // On the ground just past the beam end, along the way to the next point. The log under the end point is skipped, so a landing
    // point is never put back on the log itself.
    static Vector3 LandingPoint(Vector3 end, Vector3 forward)
    {
        Physics.SyncTransforms();
        int ground = LayerMask.GetMask("Ground");
        // What she stands on at the end point (the log model, its stump included), if it is right under her feet.
        Transform logRoot = Physics.Raycast(end + Vector3.up * 0.5f, Vector3.down, out RaycastHit under, 2f, ground)
            && Mathf.Abs(under.point.y - end.y) < 0.15f
            ? UnderStage(under.collider.transform)
            : null;
        foreach (float ahead in LandAhead)
        {
            Vector3 land = end + forward * ahead;
            RaycastHit[] hits = Physics.RaycastAll(land + Vector3.up * 3f, Vector3.down, 6f, ground, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance)
                .ToArray();
            // The log still reaches this far: landing here would put her under or inside it. Try a little farther.
            if (logRoot != null && hits.Any(h => h.collider.transform.IsChildOf(logRoot)))
                continue;
            float? y = hits.Length > 0 ? hits[0].point.y : TerrainHeight(land);
            if (y == null)
                continue;
            if (y.Value > end.y + 0.05f)
                throw new System.InvalidOperationException($"Ground past {Fmt(end)} is higher than the beam end ({y.Value:0.00}). Place the landing point by hand.");
            land.y = y.Value;
            return land;
        }
        throw new System.InvalidOperationException($"No ground found within {LandAhead.Last()} m past {Fmt(end)}. Place the landing point by hand.");
    }

    // The object right under the stage that this belongs to (Stage1/FallenLog_Art for a collider of the first log), or null outside any stage.
    static Transform UnderStage(Transform t)
    {
        while (t.parent != null && t.parent.GetComponent<Stage>() == null)
            t = t.parent;
        return t.parent != null ? t : null;
    }

    static float? TerrainHeight(Vector3 at)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 local = at - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
                return terrain.SampleHeight(at) + terrain.transform.position.y;
        }
        return null;
    }

    // The segments Baby plays as Girl_Jump: right after a BalanceBeamStart, a BalanceBeamEnd, or a StopPoint whose blocker is a
    // LeaningLog (Baby.WakeAndWalk). Same rule as BabyRoute's gizmo.
    static string Hops(BabyRoute route)
    {
        var hops = Enumerable.Range(0, Mathf.Max(0, route.Count - 1))
            .Where(i => route[i].TryGetComponent(out BalanceBeamStart _) || route[i].TryGetComponent(out BalanceBeamEnd _)
                || (route[i].TryGetComponent(out StopPoint stop) && stop.Blocker is LeaningLog))
            .Select(i => $"{route[i].name}->{route[i + 1].name} {Hop(route[i].position, route[i + 1].position):0.00} m");
        string list = string.Join(", ", hops);
        return (list.Length > 0 ? "hops " + list : "no hops") + "\n";
    }

    static float Hop(Vector3 from, Vector3 to) => Vector3.Distance(from, to);

    static string Fmt(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
}
