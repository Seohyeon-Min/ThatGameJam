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

    public ClipCaps clipCaps => ClipCaps.None;

    // The director is on EndingFlight's object.
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EndingShotBehaviour>.Create(graph);
        EndingShotBehaviour behaviour = playable.GetBehaviour();
        behaviour.shot = shot;
        behaviour.look = look;
        behaviour.flight = owner != null ? owner.GetComponent<EndingFlight>() : null;
        var director = owner != null ? owner.GetComponent<PlayableDirector>() : null;
        double frameRate = director != null && director.playableAsset is TimelineAsset timeline ? timeline.editorSettings.frameRate : 60.0;
        behaviour.blendSeconds = (float)(blendFrames / frameRate);
        return playable;
    }
}

public class EndingShotBehaviour : PlayableBehaviour
{
    public EndingFlight.Shot shot;
    public WatercolorLook look;
    public float blendSeconds;
    public EndingFlight flight;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (Application.isPlaying && flight != null)
            flight.BeginShot(shot, look, blendSeconds);
    }
}
