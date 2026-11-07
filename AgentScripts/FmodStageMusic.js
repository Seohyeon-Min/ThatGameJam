// event:/Music/backgroundMusic 의 내용을 스테이지 BGM 일곱 트랙으로 바꾼다. 이벤트 자체(경로, GUID)는 그대로 두어서 Unity의
// 연결이 유지된다. 예전과 같은 방식: 트랙마다 자기 파라미터(0이면 -80 dB, 1이면 0 dB)로 볼륨이 정해지고, MusicManager가 그
// 파라미터를 천천히 올리고 내린다. 모두 한 타임라인에서 함께 돌아서 박자가 어긋나지 않는다. 타임라인은 bgm_st_3 길이(47.407초)로
// 반복하고, 23.704초짜리 트랙은 그 안에 두 번 이어 놓는다.
// 음원은 FMODProject/ThatGameJam/Assets/Music/ 에 있어야 한다. 다시 돌리면 같은 모양으로 다시 만든다.
// "C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.09/fmodstudiocl.exe" -script AgentScripts/FmodStageMusic.js FMODProject/ThatGameJam/ThatGameJam.fspro
var Rate = 48000;
var Tracks = [
    ["bgm_st_0", 1137778],
    ["bgm_st_1_1", 1137778],
    ["bgm_st_1_2", 1137778],
    ["bgm_st_2_1", 1137778],
    ["bgm_st_2_2", 1137778],
    ["bgm_st_3", 2275555],
    ["bgm_st_4", 1137778],
];
var LoopFrames = 2275555;

var projectDir = studio.project.filePath.replace(/[\\\/][^\\\/]*$/, "");
var ev = studio.project.lookup("event:/Music/backgroundMusic");
if (!ev)
    throw "event:/Music/backgroundMusic 이 없다";

// 예전 트랙, 파라미터, 반복 구간을 지운다. 전에 이 스크립트가 만든 파라미터 프리셋(bgm_st_*)도 지운다.
ev.groupTracks.slice().forEach(function (t) { studio.project.deleteObject(t); });
ev.parameters.slice().forEach(function (p) { studio.project.deleteObject(p); });
studio.project.model.ParameterPreset.findInstances().forEach(function (preset) {
    if (preset.name.indexOf("bgm_st_") === 0)
        studio.project.deleteObject(preset);
});
ev.timeline.markers.slice().forEach(function (m) { studio.project.deleteObject(m); });

var loopSeconds = LoopFrames / Rate;
Tracks.forEach(function (entry) {
    var name = entry[0];
    var seconds = entry[1] / Rate;
    var audio = studio.project.importAudioFile(projectDir + "/Assets/Music/" + name + ".wav");
    if (!audio)
        throw name + ".wav 를 가져오지 못했다";

    var parameter = ev.addGameParameter({ name: name, type: studio.project.parameterType.User, min: 0, max: 1 });
    var track = ev.addGroupTrack(name);
    for (var start = 0; start < loopSeconds - 0.01; start += seconds) {
        var sound = track.addSound(ev.timeline, "SingleSound", start, seconds);
        sound.audioFile = audio;
    }
    var automator = track.mixerGroup.addAutomator("volume");
    // 곡선은 파라미터 자체(프록시가 가리키는 것)에 붙는다.
    var curve = automator.addAutomationCurve(parameter.preset ? parameter.preset : parameter);
    curve.addAutomationPoint(0, -80);
    curve.addAutomationPoint(1, 0);
    console.log("track " + name + " " + seconds.toFixed(4) + " s");
});

var loop = studio.project.create("LoopRegion");
loop.position = 0;
loop.length = loopSeconds;
loop.timeline = ev.timeline;
loop.markerTrack = ev.markerTracks[0];

studio.project.save();
console.log("saved, loop " + loopSeconds.toFixed(4) + " s");
