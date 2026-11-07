using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// The ending's shots in time (EndingFlight's Timeline): each clip on this track starts its shot as the playhead reaches it, and the
// shot goes on until the next clip starts, so a clip's length is how long its shot lasts. What moves in a shot (the bird's flight,
// the clouds, the light, the girl) is worked out by EndingFlight as it plays, so the Timeline window shows only when each shot comes,
// not what is in it. Only in Play Mode: scrubbing in the editor does nothing.
[TrackColor(0.95f, 0.75f, 0.3f)]
[TrackClipType(typeof(EndingShotClip))]
public class EndingShotTrack : TrackAsset
{
}
