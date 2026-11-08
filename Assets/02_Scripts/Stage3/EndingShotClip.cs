using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// One shot of the ending on its Ending Shot track (EndingShotTrack): it starts that shot as the playhead reaches it.
[System.Serializable]
public class EndingShotClip : PlayableAsset, ITimelineClipAsset
{
    public EndingFlight.Shot shot;
    [Tooltip("The watercolor look (and its Volume Profile) put on as this shot starts, staying until another shot puts on its own. Empty: the look before it stays. In Play Mode only, not in the Timeline's preview.")]
    public WatercolorLook look;
    [Tooltip("Frames over which the look (its watercolor values and Volume Profile) blends in from the one before, at the Timeline's frame rate (60 a second). 0: it changes at once.")]
    [Min(0)] public int blendFrames;
    [Tooltip("Optional: the look the shot starts in. Set, the shot goes into it (over Blend Frames, from the one before) and then blends on from it to Look. Empty: straight to Look, as before.")]
    public WatercolorLook startLook;
    [Tooltip("Frames over which Start Look blends to Look, from when Start Look is fully in. 0: over the rest of the shot (the clip's length).")]
    [Min(0)] public int startToEndFrames;

    public ClipCaps clipCaps => ClipCaps.None;

    // The director is on EndingFlight's object.
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EndingShotBehaviour>.Create(graph);
        EndingShotBehaviour behaviour = playable.GetBehaviour();
        behaviour.shot = shot;
        behaviour.look = look;
        behaviour.startLook = startLook;
        behaviour.flight = owner != null ? owner.GetComponent<EndingFlight>() : null;
        var director = owner != null ? owner.GetComponent<PlayableDirector>() : null;
        double frameRate = director != null && director.playableAsset is TimelineAsset timeline ? timeline.editorSettings.frameRate : 60.0;
        behaviour.blendSeconds = (float)(blendFrames / frameRate);
        behaviour.startToEndSeconds = (float)(startToEndFrames / frameRate);
        return playable;
    }
}

public class EndingShotBehaviour : PlayableBehaviour
{
    public EndingFlight.Shot shot;
    public WatercolorLook look;
    public float blendSeconds;
    public WatercolorLook startLook;
    // 0: the rest of the clip.
    public float startToEndSeconds;
    public EndingFlight flight;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (!Application.isPlaying || flight == null)
            return;
        float span = startToEndSeconds > 0f ? startToEndSeconds : Mathf.Max(0.01f, (float)playable.GetDuration() - blendSeconds);
        flight.BeginShot(shot, look, blendSeconds, startLook, span);
    }
}
