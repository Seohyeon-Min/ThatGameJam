// event:/Music/cutsceneEnding 의 클립 길이를 지금 cutsceneEnding.wav 길이에 맞춘다. 시작 위치(0초)는 그대로 둔다.
// 음원을 바꿔 끼운 뒤 길이가 달라졌을 때 돌린다. 길이(프레임)는 wav 헤더에서 읽은 값을 넣는다.
// "C:/Program Files/FMOD SoundSystem/FMOD Studio 2.03.09/fmodstudiocl.exe" -script AgentScripts/FmodEndingLength.js FMODProject/ThatGameJam/ThatGameJam.fspro
var Frames = 2560000;
var Rate = 48000;

var ev = studio.project.lookup("event:/Music/cutsceneEnding");
if (!ev)
    throw "event:/Music/cutsceneEnding 이 없다";
var seconds = Frames / Rate;
var count = 0;
ev.groupTracks.forEach(function (track) {
    track.modules.forEach(function (sound) {
        if (!sound.audioFile)
            return;
        console.log("before start " + sound.start + " length " + sound.length + " file " + sound.audioFile.length);
        sound.length = seconds;
        count++;
    });
});
if (count !== 1)
    throw "클립이 " + count + "개다";
studio.project.save();
console.log("length now " + seconds.toFixed(4) + " s");
