# EN_DELIVERY — Incense Crossing 英文版交付报告

生成日期：2026-10-22  
工程路径：`/Users/ld/Documents/Codex/EnglishEditions/IncenseCrossing-EN/`  
英文产品名：**Incense Crossing**  
包名：`com.templecircuit.monopoly.en`

---

## 工程来源

| 项目 | 内容 |
|------|------|
| 克隆来源 | `/Users/ld/Documents/Codex/2026-08-10/TempleMonopolyBuild/` |
| 克隆方式 | rsync 排除 Library/Temp/Logs/Builds/build/obj/.git |
| 选择原因 | 交接文档第 4 节明确指定该目录为权威工程 |
| git 初始化 | 英文工程另起 `git init`，基线提交 + 翻译提交共 2 次 |

---

## 改动清单

### 代码文件（5 个脚本完整翻译）

| 文件 | 改动摘要 |
|------|---------|
| `Assets/Game/Scripts/TempleCircuitGame.cs` | 全部玩家可见字符串改为英文：玩家名（You/Aman/Mist/Seven）、32 块地名、类别名（Lamp Wing / Zen Wing / Incense Wing / Relic Wing）、所有游戏事件文本、GetOwnedPropertySummary "No Properties Owned" |
| `Assets/Game/Scripts/TempleGameHUD.cs` | 位置标签（Incense Crossing · Cloud Temple）、回合标签（Round/Merit To Win）、香火潮流（Incense Flow）、玩家状态（{n}G / {n} Merit / Left Temple / Active）、骰子面板（Hold the central die / Charging / Die Settling / Release to roll / Choose）、胜利（Fulfilled · {name} Wins / Play Again / Stay on Board）；FormatChoiceBody 改为"Move "前缀 + 英文逗号；字体加载改为 Cinzel-Regular |
| `Assets/Game/Scripts/TempleDevelopmentSmokeCheck.cs` | 两处 Contains 校验改为英文："8 Merit To Win"（原"8功德圆满"）；"No Properties Owned"（原"暂无已占领房产"） |
| `Assets/Game/Scripts/TempleXRTabletopRig.cs` | 第 474 行调试警告中"香火争渡"改为"Incense Crossing" |
| `Assets/Game/Editor/TemplePicoProjectSetup.cs` | productName → "Incense Crossing"；bundleId → com.templecircuit.monopoly.en；APK 路径 → Builds/EN/IncenseCrossing-EN-PICO.apk；macOS 路径 → Builds/EN/IncenseCrossing-EN-macOS.app；所有菜单路径 → Tools/IncenseCrossing/… |
| `Assets/Game/Editor/TempleGameSceneInstaller.cs` | 6 处 MenuItem 中文路径全部改为英文（Install / Fix UI, Dice & Camera / Update 16:9 Start Cover / Fix POLYGON Asset Pink Materials / Import & Install Game Audio / Fix Current Scene Water Surface） |

### 资源文件

| 文件 | 改动摘要 |
|------|---------|
| `Assets/Game/UI/Resources/TempleStartCover.png` | 用 Tripo banana2 重新生成：标题"Incense Crossing"（英文书法衬线体）、START 按钮（朱红）、EXIT 按钮（青绿）；构图/按钮位置/透明角完全保留原版 |
| `Assets/Game/UI/Resources/Fonts/Cinzel-Regular.ttf` | 新增：来自 @fontsource/cinzel@5.0.5 npm 包的 woff2，用 fonttools 转 TrueType |
| `Assets/Game/UI/Resources/Fonts/Cinzel-OFL.txt` | 新增：Cinzel SIL OFL 1.1 许可证原文 |
| `ProjectSettings/ProjectSettings.asset` | productName → "Incense Crossing"；Android bundleId → com.templecircuit.monopoly.en |

---

## 字体与许可

| 字体 | 许可证 | 来源 | 用途 |
|------|--------|------|------|
| Cinzel Regular | SIL OFL 1.1 | @fontsource/cinzel@5.0.5（woff2→TTF via fonttools） | 所有 TMP HUD 文字 |
| NotoSansSC-Variable | SIL OFL 1.1 | 继承原版 | 保留在工程但不再用于显示；英文版 HUD 改用 Cinzel |

Cinzel 设计风格：罗马碑文式大写衬线体，源自经典拉丁字母碑刻，风格庄严。与原版 NotoSansSC（东方感无衬线）同属重量感强的标题字体。

---

## Tripo 调用清单

| 序号 | task_id | 模型 | 用途 | 状态 |
|------|---------|------|------|------|
| 1 | `55e7d08b-83fb-4055-be9f-b7e8587e08af` | banana2 | 封面图 TempleStartCover：中文→英文文字替换 | 成功（10 credits） |

总调用次数：1 次  
总消耗：10 credits

---

## CJK 扫描结果

扫描范围：`Assets/**/*.cs`、`Assets/**/*.unity`、`Assets/**/*.prefab`、`Assets/**/*.asset`、`ProjectSettings/**`

- **字符串字面量（玩家可见）中 CJK 字符：0 处** — 已全部翻译
- 注释中含 CJK：22 处（均为"原文 → 英文"说明注释，非玩家可见，保留）
- 内部路径中含 CJK：2 处（详见下表）

| 文件 | 内容 | 原因（保留） |
|------|------|------------|
| `TempleGameSceneInstaller.cs:20` | `Assets/模型资产/手绘低多边形中国佛寺幻想微缩沙盘风` | 必须匹配磁盘上实际 Unity 资产文件夹名，修改会破坏所有场景引用 |
| `TempleGameSceneInstaller.cs:76` | `path.Contains("骰子/")` | 场景安装器中 Prefab 文件路径匹配，必须与原始骰子 Prefab 命名一致 |

扫描结论：无任何中文会出现在运行时画面上。

---

## APK 状态

**APK 未构建。**

原因：扫描时磁盘剩余空间 7.2GB，低于规范要求的 10GB 最低阈值（"剩余空间少于 10GB 时不要开始导入或构建"），且当前无可用 Unity 槽位。

APK 目标路径（待构建）：  
`/Users/ld/Documents/Codex/EnglishEditions/IncenseCrossing-EN/Builds/EN/IncenseCrossing-EN-PICO.apk`

构建方法（磁盘释放后）：
```bash
mkdir /tmp/en-unity-slot-1 && echo "IncenseCrossing $(date)" > /tmp/en-unity-slot-1/owner
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -logFile /tmp/incense-crossing-build.log \
  -projectPath "/Users/ld/Documents/Codex/EnglishEditions/IncenseCrossing-EN" \
  -executeMethod TempleCircuitEditor.TemplePicoProjectSetup.BuildPicoApk
rm -rf /tmp/en-unity-slot-1
```

---

## 截图状态

**截图未生成。**

原因：同上——磁盘空间不足，无法启动 Unity（Unity Library 重建需要约 3-4GB）。

已有的唯一视觉验证截图：
- `Assets/Game/UI/Resources/TempleStartCover.png`（Tripo 生成的封面图，人工目检已确认拼写正确无中文）

待补充（磁盘释放后）：
- `VisualQA/EN-20261022/01-cover.png`（封面 START/EXIT 按钮）
- `VisualQA/EN-20261022/02-game-reveal.png`（游戏开始过渡帧）
- `VisualQA/EN-20261022/03-hud-default-view.png`（52° 桌面视野 HUD）
- `VisualQA/EN-20261022/04-4-player-cards.png`（四玩家卡显示）
- `VisualQA/EN-20261022/05-property-list.png`（长房产列表悬浮卡）
- `VisualQA/EN-20261022/06-dice-panel.png`（骰子面板 Move N, 选项按钮）
- `VisualQA/EN-20261022/07-8-merit-win.png`（8 Merit 胜利画面）
- `VisualQA/EN-20261022/08-restart.png`（Play Again / Stay on Board）
- `VisualQA/EN-20261022/09-festival-choice.png`（Ceremony 法会选项）
- `VisualQA/EN-20261022/10-buy-property.png`（购买 Courtyard 选项）

---

## 已验证项

| 项目 | 状态 |
|------|------|
| 原版工程完全未修改 | ✅ 已核查 |
| 英文工程独立在 ASCII 路径 | ✅ `/Users/ld/Documents/Codex/EnglishEditions/IncenseCrossing-EN/` |
| 所有玩家可见字符串翻译为英文 | ✅ 代码已修改，CJK 扫描通过 |
| 功德统一 Merit、金统一 Gold | ✅ 全局一致 |
| 玩家名无拼音（You/Aman/Mist/Seven） | ✅ |
| 地块名按含义翻译（无拼音） | ✅ 32 块全部翻译 |
| FormatChoiceBody 英文前缀/分隔符 | ✅ "Move " + 英文逗号 |
| SmokeCheck 英文字符串校验 | ✅ "8 Merit To Win" / "No Properties Owned" |
| 封面 TempleStartCover 英文化 | ✅ Tripo banana2 生成，人工目检通过 |
| 按钮命中区域未变动 | ✅ TempleStartScreen.cs 的硬编码坐标未改 |
| Cinzel TTF 字体（SIL OFL） | ✅ 32KB TrueType，许可证文件已包含 |
| ProjectSettings 产品名/包名更新 | ✅ |
| git 提交记录 | ✅ 基线提交 + 翻译提交 |

## 未验证边界

| 边界 | 原因 |
|------|------|
| APK 未构建 | 磁盘空间 7.2GB < 10GB 阈值，规范禁止启动构建 |
| 截图均为待生成 | 同上（Unity 导入需 3-4GB Library） |
| 实体 PICO 头显测试 | 无设备；需 APK 先完成 |
| 模拟器验证 | 未启动模拟器（因磁盘限制） |
| Cinzel 在 Unity TMP SDF 中的实际渲染质量 | 需编辑器首次导入后目检 SDF atlas |
| 两列长房产列表实际布局 | 需截图验证 <pos=292> 标记效果 |

## 翻译不确定项（决策已锁定）

| 原文 | 选定方案 | 选择理由 |
|------|---------|---------|
| 功德圆满 | Merit To Win | 明确表达胜利条件；"Fulfilled"用于胜利标题 |
| 愿签 | Wish Lot | 简洁；中立；无文化误导 |
| 随喜功德 | Sympathetic Merit | 英文佛教语境中"Rejoicing in Others' Merit"的简写；此处指代功德类别 |
| 水陆法会 | Water-Land Ceremony | 直译专有名词；"Rite of Water and Land"过长 |
| 木鱼小院 | Wooden Fish Cottage | 保留"木鱼"（wooden fish）是佛教法器的通用英译，不用日语借词 mokugyo |
| 改骰签 | Re-roll Slip | "Slip" 指小纸条/签，与"愿签"的签同族，风格统一 |
