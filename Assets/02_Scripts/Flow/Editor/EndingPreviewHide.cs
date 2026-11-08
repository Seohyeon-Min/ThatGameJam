using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;

// Outside Play Mode, while the Timeline window shows the ending's timeline (EndingFlight's), what the ending hides in play
// (EndingFlight's Hidden In Ending: the fog maze's animals) is not drawn, so previewing the ending looks as it plays. Only
// forceRenderingOff, which is not saved: nothing in the scene changes, and all of it shows again once the window shows something
// else, or on Play.
[InitializeOnLoad]
static class EndingPreviewHide
{
    static readonly List<Renderer> hidden = new();

    static EndingPreviewHide()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += _ => ShowAll();
        AssemblyReloadEvents.beforeAssemblyReload += ShowAll;
    }

    static void Update()
    {
        if (Application.isPlaying)
            return;
        var ending = Object.FindAnyObjectByType<EndingFlight>(FindObjectsInactive.Include);
        if (ending == null)
        {
            ShowAll();
            return;
        }
        var so = new SerializedObject(ending);
        var director = so.FindProperty("timeline").objectReferenceValue as PlayableDirector;
        bool previewing = director != null && TimelineEditor.inspectedDirector == director;
        if (!previewing)
        {
            ShowAll();
            return;
        }
        if (hidden.Count > 0)
            return;
        SerializedProperty list = so.FindProperty("hiddenInEnding");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    if (!r.forceRenderingOff)
                    {
                        r.forceRenderingOff = true;
                        hidden.Add(r);
                    }
        SceneView.RepaintAll();
    }

    static void ShowAll()
    {
        if (hidden.Count == 0)
            return;
        foreach (Renderer r in hidden)
            if (r != null)
                r.forceRenderingOff = false;
        hidden.Clear();
        SceneView.RepaintAll();
    }
}
