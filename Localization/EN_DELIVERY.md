# Incense Crossing — EN Delivery

Product name: **Incense Crossing**  
Slug: **IncenseCrossing**  
Source game: 香火争渡 (Temple Monopoly Build #4)  
Delivery date: 2026-10-22 (rework pass — QA corrections applied)

---

## APK

| Field           | Value                                                                   |
|-----------------|-------------------------------------------------------------------------|
| Path            | `Builds/EN/IncenseCrossing-EN-PICO.apk`                                 |
| Package name    | `com.templecircuit.monopoly.en`                                         |
| Size            | 108 MB                                                                  |
| SHA-256         | `f093f2ba90acd81947a9812cf200bef5cea1b372d344a82953113a73fab6e119`      |
| Unity version   | 6000.5.5f1                                                              |
| Target platform | Android ARM64 (PICO XR), IL2CPP release                                |
| Build method    | `TempleCircuitEditor.TemplePicoProjectSetup.BuildPicoApk` (batchmode)  |
| Build time      | 2026-10-22 ~13:35                                                       |

---

## Emulator Install

```
Performing Streamed Install
Success
```

Package verification via `pm list packages | grep templecircuit`:
```
package:com.templecircuit.monopoly
package:com.templecircuit.monopoly.en
```

Both packages coexist. Original package (`com.templecircuit.monopoly`) was not touched.

---

## Translation Rework Summary

Corrections applied in this rework pass:

### Tile Names — fixed to match source verbatim
All 32 tile names extracted verbatim from original `TempleCircuitGame.cs`. Previous
delivery used approximate names. Current mapping is in `EN_TEXT_TABLE.md`.

### String corrections
| 原文                          | 旧译（错误）              | 新译（正确）                          |
|------------------------------|--------------------------|---------------------------------------|
| 明签                          | Known Slip               | Revealed Slip                        |
| 法会开坛，要不要搏一把？        | Ceremony Begins…         | The Ceremony Begins — Take the Gamble?|
| 押 30 金主坛                  | Bid 30G                  | Stake 30G to Lead                    |
| 协助法会得 30 金              | Assist 30G               | Assist for 30G                       |
| 圆满 · {name}获胜              | Fulfilled · {name} Wins  | Merit Fulfilled · {name} Wins        |
| 留在棋盘                      | Stay on Board            | View Board                           |
| 香火正在汇聚                  | Incense Gathering        | Gathering Incense                    |
| 骰子落定中                    | Dice Settling            | Rolling…                             |
| 香火点数即将揭晓。             | Incense points imminent. | Your roll is about to land.          |
| 回向门把好运交到手里           | Gate Luck                | The Dedication Gate Turns Luck Your Way|
| 祈愿签 (globally)             | Wish Lot                 | Prayer Slip                          |
| 香火公费 / 香油公费 / 修缮公费  | Levy                     | Fee (in compact name)                |
| 阿满 / 小岚 / 七仔             | Aman / Mist / Seven      | Ah Man / Xiao Lan / Qi Zai          |

### Opening line (was missing from prior table)
- 晨钟一响 → The Bell Rings
- 四位行脚者同时入寺，香火巡游开始。 → Four pilgrims enter the temple together. The incense circuit begins.
  (This line was already in the original source at L.220, not a new addition.)

---

## Font Architecture

Original HUD (`ResolveSourceFont`): `Resources.Load<Font>("Fonts/NotoSansSC-Variable")` — a CJK Variable font (黑体/Gothic style).

### English edition font decisions

**Why Cinzel for titles:**  
Cinzel is a Roman-inscription typeface (all uppercase with true small caps). It suits the temple/incense aesthetic — dignified, historic, legible for short display text. Used for `headlineText` and button labels.

**Why Cinzel alone is insufficient:**  
Cinzel has no true lowercase letters — lowercase glyphs render as optical small capitals. For multi-line body text (game events, player status, property lists), sustained small-caps reading is fatiguing and reduces comprehension speed.

**Why EB Garamond for body:**  
EB Garamond is a classical humanist serif (OFL). It has full true-case support, matching the temple aesthetic (Renaissance-era letterforms evoke liturgical manuscripts), and is highly readable at 22–28px for body text. Used for `bodyText`, `hintText`, `roundText`, `trendText`, `playerTexts`, `playerPropertyHoverText`.

### Font files

| File                                | Role        | License  | Source                            |
|------------------------------------|-------------|----------|-----------------------------------|
| `Assets/Game/UI/Resources/Fonts/Cinzel-Regular.ttf`    | Titles/buttons | SIL OFL 1.1 | @fontsource/cinzel@5.0.5 (woff2→TTF via fonttools) |
| `Assets/Game/UI/Resources/Fonts/Cinzel-OFL.txt`        | License     |          |                                   |
| `Assets/Game/UI/Resources/Fonts/EBGaramond-Regular.ttf` | Body text   | SIL OFL 1.1 | @fontsource/eb-garamond@5.3.0 (woff2→TTF via fonttools) |
| `Assets/Game/UI/Resources/Fonts/EBGaramond-OFL.txt`    | License     |          |                                   |
| `Assets/Game/UI/Resources/Fonts/NotoSansSC-Variable.ttf` | (retained, unused in EN build) | OFL | Original project |

---

## SmokeCheck Results

`TempleDevelopmentSmokeCheck` checks verified at build time:
- `game.fameToWin == 8` ✓
- `roundText.text.Contains("8 Merit To Win")` ✓  
  (Format: `$"Round {n} / {max}  ·  {fameToWin} Merit To Win"`)
- `playerPropertyHoverText.text.Contains("No Properties Owned")` ✓  
  (Returned by `GetOwnedPropertySummary()` when player has no properties)
- PICO release APK message logged: `PICO release APK (EN): Builds/EN/IncenseCrossing-EN-PICO.apk`

---

## CJK Scan

No CJK characters in string literals. CJK only in developer source comments
(e.g., `// EN: 晨钟一响 → The Bell Rings`) which are stripped by the compiler.

Scan command used:
```
grep -Pn '"[^"]*[　-〿㐀-鿿豈-﫿＀-￯][^"]*"' TempleCircuitGame.cs TempleGameHUD.cs
```
Result: 0 hits in string literals.

---

## Files Changed

```
Assets/Game/Scripts/TempleCircuitGame.cs   (EN rewrite — all strings, tile names, player names)
Assets/Game/Scripts/TempleGameHUD.cs        (EN rewrite — all UI strings, two-font architecture)
Assets/Game/UI/Resources/Fonts/Cinzel-Regular.ttf   (added)
Assets/Game/UI/Resources/Fonts/Cinzel-OFL.txt        (added)
Assets/Game/UI/Resources/Fonts/EBGaramond-Regular.ttf (added)
Assets/Game/UI/Resources/Fonts/EBGaramond-OFL.txt    (added)
Localization/EN_TEXT_TABLE.md              (rebuilt — verbatim Chinese + EN translations)
Localization/EN_DELIVERY.md               (this file)
```

Original source at `2026-08-10/TempleMonopolyBuild/` — not modified.

---

## Constraints Satisfied

- Only `IncenseCrossing-EN` project touched; other 7 games untouched
- Original source project at `2026-08-10/TempleMonopolyBuild/` not modified
- unity-mcp / unity-pico2 MCP tools not used
- User's open pico2 Unity editor not touched
- Disk at time of build: ~33 GB free (well above 10 GB threshold)
- Build method: batchmode, release configuration
- Package name `com.templecircuit.monopoly.en` (ends in `.en`) ✓
- Original `com.templecircuit.monopoly` package preserved on emulator ✓
