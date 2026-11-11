# Primavis

A small bird walks a child through a frozen forest until spring arrives. Made in two weeks for **thatgamejam #02** (hosted by thatgamecompany) by a 5-person team, built with Unity for the web.

**[Play in the browser on itch.io](https://handalhandal.itch.io/primavis)**

## About the Game

Spring comes in the end. You play a bird who brings it a little sooner and makes sure the child, who stands for spring, does not walk through the winter alone.

- The child walks on her own and stops when the way is blocked
- Every obstacle clears by itself over time; the bird can step in and clear it sooner
- No tutorials and no failure: the game reaches its ending even if you do nothing

## Features

- **Intro snow walk**: hold the bird over the child to shelter her from the falling snow
- **Stage 1, the leaning log**: perch on the log so it falls and bridges the stream
- **Stage 2, the blocked rabbit burrow**: cry to the rabbits and help dig the snow away
- **Stage 3, the fog maze**: six forks hidden in fog; animals on the right ways answer the bird's cry and the child runs toward the bird
- **Seasons**: each time the child sleeps in a sunlit spot, winter retreats and the forest turns a step closer to spring
- **Discovery journal and easter eggs**: hidden interactions around the forest, each recorded with a close-up photo
- **Watercolor look**: painted materials and screen effects across the whole forest

## Controls

| Input | Action |
|-------|--------|
| Left click | Move the bird, perch, interact |
| Hold left click | Follow the ground under the pointer |
| Right click | Cry (wakes the child, makes animals answer) |
| Mouse wheel | Zoom the bird down toward the ground |
| Esc | Open the discovery journal |

## Team

| Member | Role |
|--------|------|
| Seohyeon Min | Art direction, production |
| Seungju Song | Game design, programming |
| Sam Jeon ([@Git-Mere](https://github.com/Git-Mere)) | Programming |
| Tommy Ji | Art |
| Port Nugent | Sound |

## Built With

- **Unity 6** (6000.4.2f1), Universal Render Pipeline
- **C#**
- **FMOD** for music and sound
- **WebGL** build for the browser

## Getting Started

1. Install Unity **6000.4.2f1** and the FMOD for Unity integration used by the project.
2. Clone this repository and open the folder in Unity Hub.
3. Open `Assets/00_Scenes/Main.unity` and press Play.

Design notes, decision records (ADRs), and per-commit history live in [`docs/`](docs/) in English and Korean. The original design document is [`Game Design.pdf`](Game%20Design.pdf).