// event:/Music/backgroundMusic 의 bgm_st_* 트랙 볼륨을 지금 값에서 Delta dB 만큼 바꾼다(지금 값 기준이라 다시 돌리면 또 바뀐다).
// 트랙마다 붙은 파라미터 볼륨 곡선(-80..0 dB)은 그대로 두고, 트랙 페이더만 옮긴다.
// "C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.09/fmodstudiocl.exe" -script AgentScripts/FmodMusicVolume.js FMODProject/ThatGameJam/ThatGameJam.fspro
var Delta = -4.0;

var ev = studio.project.lookup("event:/Music/backgroundMusic");
if (!ev)
    throw "event:/Music/backgroundMusic 이 없다";
var count = 0;
ev.groupTracks.forEach(function (track) {
    var group = track.mixerGroup;
    if (group.name.indexOf("bgm_st_") !== 0)
        return;
    var before = group.volume;
    group.volume = before + Delta;
    console.log(group.name + " " + before.toFixed(2) + " -> " + group.volume.toFixed(2) + " dB");
    count++;
});
if (count !== 7)
    throw "트랙이 " + count + "개다";
studio.project.save();
console.log("saved");
