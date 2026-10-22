# Incense Crossing — EN_TEXT_TABLE

English edition of 香火争渡 (Temple Monopoly Build, game #4).  
Source file: `Assets/Game/Scripts/TempleCircuitGame.cs` + `TempleGameHUD.cs`  
All 原文 strings extracted verbatim from the original source.

---

## 棋盘格名称 / Tile Names (32 格)

| 序号 | 原文（verbatim）    | EN Long Name          | EN Short (CompactTileName) |
|------|--------------------|-----------------------|---------------------------|
| 0    | 山门鸣钟            | Gate Bell             | Gate Bell                 |
| 1    | 禅心石径            | Stone Path            | Stone Path                |
| 2    | 香客长廊            | Pilgrim's Corridor    | Corridor                  |
| 3    | 法器库              | Ritual Vault          | Ritual                    |
| 4    | 莲花灯阁            | Lotus Lamp Pavilion   | Lotus Lamps               |
| 5    | 祈愿签              | Prayer Slip           | Prayer Slip               |
| 6    | 素斋院              | Refectory             | Refectory                 |
| 7    | 功德红包            | Merit Envelope        | Envelope                  |
| 8    | 供灯塔              | Offering Tower        | Lamp Tower                |
| 9    | 经藏回廊            | Scripture Cloister    | Cloister                  |
| 10   | 香火公费            | Incense Fee           | Incense Fee               |
| 11   | 舍利阁              | Reliquary             | Relics                    |
| 12   | 祈愿签              | Prayer Slip           | Prayer Slip               |
| 13   | 晨钟法会            | Morning Bell Rite     | Morning Bell              |
| 14   | 祈愿香台            | Prayer Altar          | Altar                     |
| 15   | 回向门              | Dedication Gate       | Dedication Gate           |
| 16   | 木鱼舍              | Wooden Fish Hall      | Wooden Fish               |
| 17   | 禅修堂              | Meditation Hall       | Meditation                |
| 18   | 祈愿签              | Prayer Slip           | Prayer Slip               |
| 19   | 藏经楼              | Scripture Library     | Library                   |
| 20   | 收功德              | Collect Merit         | Collect                   |
| 21   | 大法会              | Grand Ceremony        | Grand                     |
| 22   | 香油公费            | Oil Fee               | Oil Fee                   |
| 23   | 祈愿签              | Prayer Slip           | Prayer Slip               |
| 24   | 回向门              | Dedication Gate       | Dedication Gate           |
| 25   | 随喜功德            | Rejoice in Merit      | Rejoice                   |
| 26   | 灯火法会            | Lantern Rite          | Lantern Rite              |
| 27   | 修缮公费            | Repair Fee            | Repairs                   |
| 28   | 祈愿签              | Prayer Slip           | Prayer Slip               |
| 29   | 布施功德            | Almsgiving Merit      | Giving                    |
| 30   | 水陆法会            | Water-Land Ceremony   | Water & Land              |
| 31   | 祈愿签              | Prayer Slip           | Prayer Slip               |

---

## 分组名称 / Category Names (4 组)

| 原文      | 英文                  |
|----------|-----------------------|
| 供灯阁    | Lamp Pavilion         |
| 禅修院    | Meditation Court      |
| 香火院    | Incense Court         |
| 法器阁    | Ritual Pavilion       |

---

## 玩家名称 / Player Names

| 原文  | 英文      |
|------|-----------|
| 你   | You       |
| 阿满  | Ah Man    |
| 小岚  | Xiao Lan  |
| 七仔  | Qi Zai    |

---

## 开局 / Game Start

| 原文 (TempleCircuitGame.cs L.220)                         | 英文                                                                   |
|----------------------------------------------------------|------------------------------------------------------------------------|
| 晨钟一响                                                  | The Bell Rings                                                         |
| 四位行脚者同时入寺，香火巡游开始。                          | Four pilgrims enter the temple together. The incense circuit begins.   |

---

## 掷骰 / Dice

| 原文 (L.287)                                              | 英文                                                           |
|----------------------------------------------------------|----------------------------------------------------------------|
| 骰子落定：{value} 点                                      | Dice Settled: {value}                                         |
| 前进 {value} 格，或消耗一张改骰签重新投掷。               | Move {value}, or use a Re-roll Slip to roll again.             |
| 按此点数行走                                              | Accept Roll                                                    |
| 改骰签 · 剩 {n} 张                                       | Re-roll Slip · {n} left                                       |

---

## 抽签格 / Prayer Slip Tile (L.438)

| 原文                                            | 英文                                                                                           |
|------------------------------------------------|-----------------------------------------------------------------------------------------------|
| 山门递来两支签                                   | Two Slips at the Gate                                                                         |
| 明签稳定获取功德；免费抽签则会当场结算随机结果。 | A Revealed Slip gives steady merit; a free draw resolves a random outcome on the spot.        |
| 35 金买明签                                     | Buy Revealed Slip · 35G                                                                       |
| 免费抽祈愿签                                    | Draw a Free Prayer Slip                                                                       |

---

## 法会格 / Ceremony Tile (L.458, L.465, L.472)

| 原文                                                          | 英文                                                                          |
|--------------------------------------------------------------|-------------------------------------------------------------------------------|
| 法会开坛，要不要搏一把？                                       | The Ceremony Begins — Take the Gamble?                                        |
| 冒险有 65% 概率获得 80 金与 2 功德；稳妥则固定获得 30 金。    | Bold: 65% chance of 80G and 2 Merit; Safe: guaranteed 30G.                   |
| 押 30 金主坛                                                  | Stake 30G to Lead                                                             |
| 协助法会得 30 金                                              | Assist for 30G                                                                |
| 没主坛，也没空手                                              | Can't Lead, But Not Empty-Handed                                              |
| 协助收场，获得 20 金。                                        | Assisted and received 20G.                                                    |

---

## 回向门 / Dedication Gate (L.482)

| 原文                          | 英文                                           |
|------------------------------|------------------------------------------------|
| 回向门把好运交到手里           | The Dedication Gate Turns Luck Your Way        |
| 35 金入袋，再添一张改骰签。   | 35G gained, plus one Re-roll Slip.             |

---

## 集组奖励 / Set Complete (L.761)

| 原文                      | 英文                            |
|--------------------------|---------------------------------|
| 集齐「{CategoryName}」   | Set Complete: {CategoryName}    |
| 功德 +2。                 | Merit +2.                       |

---

## HUD 状态栏 / Status HUD (TempleGameHUD.cs)

| 原文 (L.185)                | 英文                                |
|----------------------------|-------------------------------------|
| 香火争渡 · 云上寺院          | Incense Crossing · Cloud Temple     |

| 原文 (L.305)                                        | 英文                                              |
|----------------------------------------------------|---------------------------------------------------|
| 巡游 {n} / {max} · {fameToWin}功德圆满             | Round {n} / {max}  ·  {fameToWin} Merit To Win   |

| 原文 (L.307)                | 英文                    |
|----------------------------|-------------------------|
| 香火流向 · {trend}          | Incense Flow · {trend}  |

| 原文 (L.312)                | 英文                      |
|----------------------------|---------------------------|
| 已离寺                      | Left Temple               |
| {cash} 金 · {fame} 功德    | {cash}G  ·  {fame} Merit  |

| 原文 (L.313)               | 英文                        |
|---------------------------|------------------------------|
| <b>本轮 · {name}</b>      | <b>Active · {name}</b>       |

| 原文 (L.348)               | 英文                  |
|---------------------------|-----------------------|
| 玩家 {n}                  | Player {n}            |

| 原文 (L.352)                             | 英文                              |
|-----------------------------------------|-----------------------------------|
| <b>{name} · 已占领房产</b>\n            | <b>{name} · Properties Owned</b>\n |

---

## 掷骰提示 / Roll Prompts (TempleGameHUD.cs)

| 原文                          | 英文                                               |
|------------------------------|----------------------------------------------------|
| 改骰签已启用                   | Re-roll Slip Active                               |
| {name}的回合                  | {name}'s Turn                                     |
| 按住中央骰子，积蓄香火。        | Hold the central die to build incense power.       |
| 松手落骰                       | Release to roll                                   |
| 香火正在汇聚                   | Gathering Incense                                 |
| 蓄力 {n}%                     | Charging {n}%                                     |
| 骰子落定中                     | Rolling…                                          |
| 香火点数即将揭晓。              | Your roll is about to land.                        |
| 请选择                         | Choose                                            |

---

## 胜利画面 / Winner Screen

| 原文 (L.ShowWinner)          | 英文                            |
|-----------------------------|----------------------------------|
| 圆满 · {winner}获胜           | Merit Fulfilled · {winner} Wins |
| 再开一局                      | Play Again                      |
| 留在棋盘                      | View Board                      |

---

## 房产 / Properties

| 原文                               | 英文                        |
|-----------------------------------|-----------------------------|
| 暂无已占领房产                     | No Properties Owned         |
| {tile.name} · {n+1}级             | {tile.name} · Lv.{n+1}      |

---

## 格子信息 / Tile Info (GetTileInfo / DescribeTile)

| 类型     | 原文                         | 英文                                   |
|---------|------------------------------|----------------------------------------|
| 出发格  | 寺院外围，巡游起点            | Temple gate · starting point           |
| 公费格  | 公账支出 {n} 金               | Temple levy · {n}G                     |
| 收入格  | 功德簿添记 {n} 金             | Merit credited · {n}G                  |
| 抽签格  | 祈愿签 · 随机结果             | Prayer Slip · random outcome           |
| 法会格  | 盛大法会 · 需做抉择           | Grand rite · choose your path          |
| 回向门  | 回向 · 35 金 + 改骰签         | Dedication · 35G + Re-roll Slip        |
| 可购/未占 | 供 {price} 金购入           | Available · {price}G to claim          |
| 已占/可升 | 已占 Lv.{n} · 访客付 {rent}G | Owned Lv.{n} · visitor pays {rent}G   |
| 满级    | 已占 Lv.{max} · 完全修缮     | Owned Lv.{max} · fully renovated       |

---

_生成时间：2026-10-22_  
_源码版本：TempleMonopolyBuild 2026-08-10_
