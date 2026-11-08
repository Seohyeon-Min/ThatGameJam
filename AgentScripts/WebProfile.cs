using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Profiling;
using UnityEditorInternal;

// Web build stall measurement. Build() makes a Development WebGL build that connects to this Editor's
// Profiler. Start() records every frame the player sends: Builds/profile/frames.csv (one row per frame)
// and Builds/profile/spikes.txt (the sample tree of frames over spikeMs). Stop() ends recording.
// Each script run compiles a new assembly, so the running recorder is reached through AppDomain data.
// Run: unity command script run --file AgentScripts/WebProfile.cs --entry WebProfile.Build | Start | Stop
public static class WebProfile
{
    const float spikeMs = 50f;
    const float treeMinMs = 2f;
    const string dir = "Builds/profile";
    static int next = -1;
    static StreamWriter frames, spikes;

    public static string Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/00_Scenes/Main.unity" },
            locationPathName = "Builds/WebGLDev",
            target = BuildTarget.WebGL,
            options = BuildOptions.Development | BuildOptions.ConnectWithProfiler,
        };
        // The development FMOD library logs a warning on every lock in a web build; that flood makes the Profiler drop the player.
        FMODUnity.Settings fmod = FMODUnity.Settings.Instance;
        FMOD.DEBUG_FLAGS logging = fmod.LoggingLevel;
        fmod.LoggingLevel = FMOD.DEBUG_FLAGS.ERROR;
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            fmod.LoggingLevel = logging;
        }
        var s = report.summary;
        return $"Result={s.result} errors={s.totalErrors} size={s.totalSize} time={s.totalTime}";
    }

    public static string Start()
    {
        Stop();
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("HHmmss");
        frames = new StreamWriter($"{dir}/frames_{stamp}.csv") { AutoFlush = true };
        spikes = new StreamWriter($"{dir}/spikes_{stamp}.txt") { AutoFlush = true };
        frames.WriteLine("frame,startSec,ms");
        EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        next = -1;
        ProfilerDriver.NewProfilerFrameRecorded += OnFrame;
        System.AppDomain.CurrentDomain.SetData(stopKey, (System.Action)StopThis);
        return $"recording to {dir}/*_{stamp}, connected={ProfilerDriver.GetConnectionIdentifier(ProfilerDriver.connectedProfiler)}";
    }

    const string stopKey = "WebProfile.Stop";

    public static string Stop()
    {
        (System.AppDomain.CurrentDomain.GetData(stopKey) as System.Action)?.Invoke();
        System.AppDomain.CurrentDomain.SetData(stopKey, null);
        return "stopped";
    }

    static void StopThis()
    {
        ProfilerDriver.NewProfilerFrameRecorded -= OnFrame;
        frames?.Dispose();
        spikes?.Dispose();
        frames = spikes = null;
    }

    // A frame's data can still be arriving when it is announced, so read two frames behind.
    static void OnFrame(int connectionId, int newFrame)
    {
        if (next < 0)
            next = newFrame;
        for (; next <= newFrame - 2; next++)
            Record(next);
    }

    static void Record(int frame)
    {
        if (frame < ProfilerDriver.firstFrameIndex)
            return;
        using (RawFrameDataView raw = ProfilerDriver.GetRawFrameDataView(frame, 0))
        {
            if (!raw.valid)
                return;
            frames.WriteLine($"{frame},{raw.frameStartTimeNs / 1e9:F3},{raw.frameTimeMs:F2}");
            if (raw.frameTimeMs < spikeMs)
                return;
            spikes.WriteLine($"=== frame {frame} t={raw.frameStartTimeNs / 1e9:F3}s {raw.frameTimeMs:F1} ms");
        }
        using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
            HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
        {
            var sb = new StringBuilder();
            Walk(view, view.GetRootItemID(), 0, sb);
            spikes.Write(sb.ToString());
        }
    }

    static void Walk(HierarchyFrameDataView view, int id, int depth, StringBuilder sb)
    {
        var children = new List<int>();
        view.GetItemChildren(id, children);
        foreach (int child in children)
        {
            float total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
            if (total < treeMinMs)
                continue;
            float self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
            float gc = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory);
            sb.Append(' ', depth * 2).Append($"{view.GetItemName(child)}  total {total:F1}  self {self:F1}");
            if (gc > 0)
                sb.Append($"  gc {gc / 1024f:F0}KB");
            sb.AppendLine();
            if (depth < 12)
                Walk(view, child, depth + 1, sb);
        }
    }
}
