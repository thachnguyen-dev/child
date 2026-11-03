# Childhood Days (Những ngày thơ ấu)

**A collection of mini-games from a Vietnamese childhood, for Android, iOS and the web.**

> Coming soon to Google Play.

Walk into a village yard painted in ink and watercolour. Each object in the yard opens a game that Vietnamese
children grew up with: flying kites in the afternoon fields, knocking mangoes off the neighbour's tree, racing down
the village slope on an areca-palm sheath, playing Ô ăn quan on a board drawn in the dirt.

A solo project in **Unity 6 and C#**, with Vietnamese and English text.

> This repository holds the **source code only**. Art, audio and scenes are not included, so the project does not
> open as a playable game.

## The mini-games

| Game | What you do | Under the hood |
| --- | --- | --- |
| **Vẽ tranh** (drawing contest) | Draw beside an AI deskmate and get judged | A paper canvas that behaves like real wax crayons, pencil and eraser |
| **Cờ caro** (five in a row) | Play on a 15 × 15 notebook page against the AI | AI that reads caro shapes (open threes, double threats) and searches for forced wins on Hard; a hand writes each mark stroke by stroke |
| **Thả diều** (kite flying) | Run the child left and right to keep the kite up | The kite is never controlled directly: running with or against the wind raises or lowers it |
| **Trộm xoài** (mango picking) | Throw stones, then your sandals, at the mango tree | Slingshot aiming |
| **Ô ăn quan** | The traditional board game against the AI | Full rules on a 12-space ring; Hard AI uses alpha-beta search 4 moves deep |
| **Đua mo cau** (sheath sled race) | Race 3 AI children down the village slope | Top-down arcade racing |
| **Bắt chuồn chuồn + tập bơi** | Catch dragonflies, then a swimming race | Dragonflies react to real disturbances; tap-per-stroke race |
| **Nhổ tóc bạc** (special) | Pull grey hairs from grandma's head | Limited to 3 sessions a day |

Turn-based games start with **oẳn tù tì** (rock-paper-scissors) between two drawn hands to decide who moves first.

## Highlights

- **One shared flow for every game:** first-time story → instructions → play → win, loss or draw → result → back to
  the yard. A new mini-game only implements its own gameplay.
- **Game AI with fair difficulty:** difficulty changes only how well the AI chooses, never the rules.
- **Art generated in code:** a small signed-distance rasterizer draws soft-shaded shapes in the yard's style
  (children, hands, the river, the mango tree, the shops). Drawing sounds are synthesized from filtered noise.
- **Progression:** points, achievements, a village shop and a street vendor who walks across the yard now and then.
- **Crash-safe saves:** local JSON written to a temp file first, with the previous save kept as a backup.
- **Kind monetization:** rewarded ads only, played only when the player taps an offer; consumable in-app purchases.
- **Three platforms:** Android, iOS (Xcode export fixed up for the Mac) and a CrazyGames web build behind one
  platform interface.

## Code layout

```
Assets/
  Runtime/
    Core/        game state, save store, localization, achievements, shop, daily limits
    Hub/         the yard, its objects, the street vendor
    MiniGame/    shared session flow, rock-paper-scissors, input
    MiniGames/   VeTranh, Caro, ThaDieu, TromXoai, OAnQuan, MoCau, ChuonChuon, NhoToc
    Services/    ads (LevelPlay, web), IAP, CrazyGames platform
    UI/          generated art rasterizer, hands, shop screens, paper-style UI
  Editor/        project setup and builds, art bakers, crayon test sheet, iOS export fix
  Tests/         rules tests for the board games, saves and progression
```

## Tech

Unity 6 · C# · uGUI and TextMeshPro · Unity IAP 5 · LevelPlay · CrazyGames SDK · Unity Test Framework

## About

Made by **Nguyen Ngoc Thach**, Unity developer and game development student at VTC Academy. I designed the games
from my own childhood memories, painted the yard, and tested every game. I wrote the code with Claude Code as an AI
pair programmer.
More: [github.com/thachnguyen-dev](https://github.com/thachnguyen-dev) ·
[my games on Google Play](https://play.google.com/store/apps/developer?id=the%20stone)
