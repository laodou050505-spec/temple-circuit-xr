# EN_TEXT_TABLE — Incense Crossing (香火争渡 英文版)

生成日期：2026-10-22  
工程路径：`/Users/ld/Documents/Codex/EnglishEditions/IncenseCrossing-EN/`  
原版工程：`/Users/ld/Documents/Codex/2026-08-10/TempleMonopolyBuild/`

---

## 术语表（全游戏统一用词）

| 原文 | 英文 | 说明 |
|------|------|------|
| 功德 | Merit | 胜利积分，统一用 Merit |
| 金 | Gold (G) | 游戏货币，统一用 Gold，数字后缩写 G |
| 香火 | Incense | 游戏主题词 |
| 骰子 / 骰 | Die / Dice | 单数 die，复数 dice |
| 巡游 / 回合 | Round | 回合计数 |
| 房产 / 地块 | Property / Courtyard | 地格用 Property，建筑称 Courtyard |
| 功德圆满 | Merit To Win | 胜利条件短语 |
| 改骰签 | Re-roll Slip | 重骰道具 |
| 随机事件 | Wish Lot | 随机格名 |
| 法会 / 庆典 | Ceremony / Festival | 活动格 |
| 供奉 / 进贡 | Tribute / Offering | 具体语境见下表 |
| 离寺 / 破产 | Left Temple | 破产状态 |
| 圆满 | Fulfilled | 游戏结束/胜利标题 |

---

## 完整翻译表

### TempleCircuitGame.cs — 玩家名

| key | 原文 | 英文 | 来源文件:行 | 使用场景 | 状态 |
|-----|------|------|------------|---------|------|
| player_0 | 你 | You | TempleCircuitGame.cs:97 | 玩家 0（人类玩家）名字 | ✅ |
| player_1 | 阿满 | Aman | TempleCircuitGame.cs:97 | 玩家 1 AI 名（阿满=知足/平和） | ✅ |
| player_2 | 小岚 | Mist | TempleCircuitGame.cs:97 | 玩家 2 AI 名（小岚=山岚小雾） | ✅ |
| player_3 | 七仔 | Seven | TempleCircuitGame.cs:97 | 玩家 3 AI 名（七仔=数字七） | ✅ |

### TempleCircuitGame.cs — 32 地块名

| key | 原文 | 英文 | 来源文件:行 | 类型 | 状态 |
|-----|------|------|------------|------|------|
| tile_00 | 山门 | Bell Gate | TempleCircuitGame.cs:108 | Start（起点） | ✅ |
| tile_01 | 禅石小径 | Zen Stone Path | TempleCircuitGame.cs:109 | Property | ✅ |
| tile_02 | 香客廊 | Pilgrim Arcade | TempleCircuitGame.cs:110 | Property | ✅ |
| tile_03 | 圣物厅 | Sacred Relics Hall | TempleCircuitGame.cs:111 | Property | ✅ |
| tile_04 | 莲灯塔 | Lotus Lamp Tower | TempleCircuitGame.cs:112 | Property | ✅ |
| tile_05 | 愿签摊 | Wish Lot | TempleCircuitGame.cs:113 | Chance | ✅ |
| tile_06 | 斋食院 | Vegetarian Court | TempleCircuitGame.cs:114 | Property | ✅ |
| tile_07 | 功德满堂 | Merit Blessing | TempleCircuitGame.cs:115 | Bonus (+40G) | ✅ |
| tile_08 | 供灯楼 | Offering Lighthouse | TempleCircuitGame.cs:116 | Property | ✅ |
| tile_09 | 经阁 | Scripture Gallery | TempleCircuitGame.cs:117 | Property | ✅ |
| tile_10 | 香火捐 | Incense Levy | TempleCircuitGame.cs:118 | Tax (-35G) | ✅ |
| tile_11 | 宝物亭 | Relic Pavilion | TempleCircuitGame.cs:119 | Property | ✅ |
| tile_12 | 愿签摊 | Wish Lot | TempleCircuitGame.cs:120 | Chance | ✅ |
| tile_13 | 晨钟法会 | Morning Bell Ceremony | TempleCircuitGame.cs:121 | Festival | ✅ |
| tile_14 | 香坛 | Incense Altar | TempleCircuitGame.cs:122 | Property | ✅ |
| tile_15 | 功德门 | Dedication Gate | TempleCircuitGame.cs:123 | Gate | ✅ |
| tile_16 | 木鱼小院 | Wooden Fish Cottage | TempleCircuitGame.cs:124 | Property | ✅ |
| tile_17 | 禅修堂 | Meditation Hall | TempleCircuitGame.cs:125 | Property | ✅ |
| tile_18 | 愿签摊 | Wish Lot | TempleCircuitGame.cs:126 | Chance | ✅ |
| tile_19 | 经幢院 | Canon Tower | TempleCircuitGame.cs:127 | Property | ✅ |
| tile_20 | 积善成德 | Merit Collection | TempleCircuitGame.cs:128 | Bonus (+40G) | ✅ |
| tile_21 | 大法会 | Grand Ceremony | TempleCircuitGame.cs:129 | Festival | ✅ |
| tile_22 | 灯油捐 | Lamp Oil Levy | TempleCircuitGame.cs:130 | Tax (-45G) | ✅ |
| tile_23 | 愿签摊 | Wish Lot | TempleCircuitGame.cs:131 | Chance | ✅ |
| tile_24 | 功德门 | Dedication Gate | TempleCircuitGame.cs:132 | Gate | ✅ |
| tile_25 | 随喜功德 | Sympathetic Merit | TempleCircuitGame.cs:133 | Bonus (+45G) | ✅ |
| tile_26 | 花灯法会 | Lantern Ceremony | TempleCircuitGame.cs:134 | Festival | ✅ |
| tile_27 | 修缮捐 | Repair Levy | TempleCircuitGame.cs:135 | Tax (-50G) | ✅ |
| tile_28 | 愿签摊 | Wish Lot | TempleCircuitGame.cs:136 | Chance | ✅ |
| tile_29 | 布施功德 | Alms Merit | TempleCircuitGame.cs:137 | Bonus (+50G) | ✅ |
| tile_30 | 水陆法会 | Water-Land Ceremony | TempleCircuitGame.cs:138 | Festival | ✅ |
| tile_31 | 愿签摊 | Wish Lot | TempleCircuitGame.cs:139 | Chance | ✅ |

### TempleCircuitGame.cs — 类别名（翼）

| key | 原文 | 英文 | 来源文件:行 | 使用场景 | 状态 |
|-----|------|------|------------|---------|------|
| cat_offering | 供灯阁 | Lamp Wing | TempleCircuitGame.cs:992 | 类别/潮流名称 | ✅ |
| cat_meditation | 禅修院 | Zen Wing | TempleCircuitGame.cs:992 | 类别/潮流名称 | ✅ |
| cat_incense | 香火院 | Incense Wing | TempleCircuitGame.cs:992 | 类别/潮流名称 | ✅ |
| cat_relic | 法器阁 | Relic Wing | TempleCircuitGame.cs:992 | 类别/潮流名称 | ✅ |

### TempleCircuitGame.cs — 游戏事件文本

| key | 原文 | 英文 | 来源文件 | 使用场景 | 状态 |
|-----|------|------|---------|---------|------|
| game_start_title | （无原文，新增） | The Bell Rings | TempleCircuitGame.cs:ResetGame | 开始弹窗标题 | ✅ |
| game_start_body | （无原文，新增） | Four pilgrims enter the temple together. The Incense Circuit begins. | TempleCircuitGame.cs:ResetGame | 开始弹窗正文 | ✅ |
| win_merit | 功德圆满！{name}胜出 | Fulfilled · {name} Wins | TempleCircuitGame.cs:EndGame | 胜利提示 | ✅ |
| merit_blessing | 功德满堂 | Merit Blessing | TempleCircuitGame.cs | 地块名 | ✅ |
| chance_buy_label | 购买已知愿签 | Buy Known Lot · 35G | TempleCircuitGame.cs:ResolveTileRoutine | 机会格按钮 | ✅ |
| chance_draw_label | 免费抽签 | Draw Free Wish Lot | TempleCircuitGame.cs:ResolveTileRoutine | 机会格按钮 | ✅ |
| festival_bold | 主持法会（冒险） | Preside · Risk 30 Gold | TempleCircuitGame.cs:ResolveTileRoutine | 法会按钮 | ✅ |
| festival_safe | 协助法会（稳妥） | Assist · Gain 30 Gold | TempleCircuitGame.cs:ResolveTileRoutine | 法会按钮 | ✅ |
| property_buy | 纳入功德册 | Acquire · {price}G | TempleCircuitGame.cs:ResolvePropertyRoutine | 购买房产按钮 | ✅ |
| property_skip | 保留现金 | Keep Cash | TempleCircuitGame.cs:ResolvePropertyRoutine | 跳过购买按钮 | ✅ |
| upgrade_yes | 加盖飞檐 | Upgrade · {cost}G | TempleCircuitGame.cs:ResolvePropertyRoutine | 升级按钮 | ✅ |
| upgrade_no | 暂不升级 | Not Now | TempleCircuitGame.cs:ResolvePropertyRoutine | 跳过升级按钮 | ✅ |
| restart | 再来一局 | Play Again | TempleCircuitGame.cs:HandleAction | 重开按钮 | ✅ |
| dismiss_win | 继续观看棋盘 | Stay on Board | TempleCircuitGame.cs:HandleAction | 留在棋盘按钮 | ✅ |

### TempleGameHUD.cs — HUD 顶部状态栏

| key | 原文 | 英文 | 来源文件:行 | 使用场景 | 状态 |
|-----|------|------|------------|---------|------|
| location | 香火争渡 · 云上寺院 | Incense Crossing · Cloud Temple | TempleGameHUD.cs:185 | 标题位置标签（静态） | ✅ |
| round_label | 巡游 {r}/{max} · {n}功德圆满 | Round {r} / {max}  ·  {n} Merit To Win | TempleGameHUD.cs:305 | 回合/胜利条件 | ✅ |
| trend_label | 香火流向 · {trend} | Incense Flow · {trend} | TempleGameHUD.cs:308 | 当前类别潮流 | ✅ |
| player_status_bankrupt | 已离寺 | Left Temple | TempleGameHUD.cs:312 | 玩家状态（破产） | ✅ |
| player_status | {cash} 金 · {fame} 功德 | {cash}G  ·  {fame} Merit | TempleGameHUD.cs:312 | 玩家状态（正常） | ✅ |
| player_active | 本轮 · {name} | Active · {name} | TempleGameHUD.cs:313 | 当前玩家高亮 | ✅ |
| hover_header | {name} · 已占领房产 | {name} · Properties Owned | TempleGameHUD.cs:352 | 房产悬浮卡标题 | ✅ |
| empty_properties | 暂无已占领房产 | No Properties Owned | TempleCircuitGame.cs:GetOwnedPropertySummary | 空房产状态 | ✅ |

### TempleGameHUD.cs — 骰子面板

| key | 原文 | 英文 | 来源文件:行 | 使用场景 | 状态 |
|-----|------|------|------------|---------|------|
| roll_prompt | {name}的回合 | {name}'s Turn | TempleGameHUD.cs:379 | 等待掷骰提示标题 | ✅ |
| reroll_active | 改骰签已启用 | Re-roll Slip Active | TempleGameHUD.cs:379 | 重骰状态标题 | ✅ |
| roll_body | 按住中央骰子，积蓄香火。 | Hold the central die to build incense power. | TempleGameHUD.cs:381 | 骰子提示正文 | ✅ |
| roll_hint | 松手落骰 | Release to roll | TempleGameHUD.cs:382 | 骰子提示（底部） | ✅ |
| charge_title | 香火正在汇聚 | Incense Power Building | TempleGameHUD.cs:388 | 蓄力状态标题 | ✅ |
| charge_body | 蓄力 {n}% | Charging {n}% | TempleGameHUD.cs:389 | 蓄力百分比 | ✅ |
| rolling_title | 骰子落定中 | Die Settling | TempleGameHUD.cs:397 | 骰子滚动中标题 | ✅ |
| rolling_body | 香火点数即将揭晓。 | The incense number will be revealed. | TempleGameHUD.cs:399 | 骰子滚动中正文 | ✅ |
| choice_hint | 请选择 | Choose | TempleGameHUD.cs:410 | 选项面板底部提示 | ✅ |
| winner_title | 圆满 · {name}获胜 | Fulfilled · {name} Wins | TempleGameHUD.cs:439 | 胜利标题 | ✅ |
| play_again | 再开一局 | Play Again | TempleGameHUD.cs:439 | 重开按钮 | ✅ |
| stay_on_board | 留在棋盘 | Stay on Board | TempleGameHUD.cs:439 | 留在棋盘按钮 | ✅ |

### TempleStartCover.png — 封面图（已重新生成）

| key | 原文 | 英文 | 使用场景 | 状态 |
|-----|------|------|---------|------|
| cover_title | 香火争渡（书法大字） | Incense Crossing | 封面主标题 | ✅ Tripo banana2 生成 |
| cover_start | 开始游戏 | START | 封面 START 按钮 | ✅ Tripo banana2 生成 |
| cover_exit | 退出 | EXIT | 封面 EXIT 按钮 | ✅ Tripo banana2 生成 |

### ProjectSettings — 应用级

| key | 原文 | 英文 | 来源文件 | 状态 |
|-----|------|------|---------|------|
| productName | 香火争渡 | Incense Crossing | ProjectSettings/ProjectSettings.asset | ✅ |
| bundleId | com.templecircuit.monopoly | com.templecircuit.monopoly.en | ProjectSettings/ProjectSettings.asset | ✅ |

---

## 未翻译项（内部，非玩家可见）

以下字符串保留中文，因为它们是内部资产路径，必须与磁盘上的目录/文件名完全匹配：

| 项目 | 原因 |
|------|------|
| `Assets/模型资产/手绘低多边形中国佛寺幻想微缩沙盘风` | Unity Asset 文件夹路径，修改会破坏场景引用 |
| `path.Contains("骰子/")` | 场景安装器中骰子 Prefab 路径过滤 |

---

## 翻译不确定项

以下翻译经过多次斟酌，方案已选定，记录备查：

| 原文 | 选定方案 | 备选方案 | 选择理由 |
|------|---------|---------|---------|
| 功德圆满 | Merit To Win | Virtue Fulfilled | "Merit To Win" 更明确表达胜利条件 |
| 愿签 | Wish Lot | Fortune Slip / Prayer Lot | "Wish Lot" 简洁，与西方占卜文化类似 |
| 法会 | Ceremony | Festival / Ritual | "Ceremony" 更庄重，符合寺院语境 |
| 水陆法会 | Water-Land Ceremony | Water & Land Rite | "Water-Land" 是水陆法会的直译，专有名词 |
| 随喜功德 | Sympathetic Merit | Rejoicing Merit | "Sympathetic Merit" 在英文佛教语境中较常见 |
| 木鱼小院 | Wooden Fish Cottage | Mokugyo Court | 选中文语义翻译而非日语借词 mokugyo |
