# INCENSE CROSSING v2 视觉与体验打磨报告

日期：2026-10-22  
基于版本：en-v1（标签 `en-v1`）  
本报告对应版本：v2 polish  

---

## 一、改动清单

### 1. 封面高级感提升

**文件**：`Assets/Game/UI/Resources/TempleStartCover.png`  
**说明**：
- 原图为本地排字全大写等高 INCENSE CROSSING 版本（总控 v1 结论）
- 使用 Tripo banana2 image-to-image 生成更精致版本
- 新封面保留：全大写等高 "INCENSE CROSSING"、START/EXIT 按钮视觉位置、2752×1536 尺寸
- 质感提升：中国水墨风插画、佛寺庭院鸟瞰、暖色宣纸底、香烟飘散、锦鲤池、冷杉松树
- 四角无水印，START/EXIT 代码命中区不受影响（TempleStartScreen.cs 未修改）

### 2. 掷骰蓄力手感

**文件**：`Assets/Game/Scripts/TempleGameHUD.cs`，方法 `ShowDiceCharge`  
**改动**：
- 蓄力条颜色从金色（#DCB55B）线性插值到朱砂红（#C0392B），随 strength 增大
- strength ≥ 85% 时提示文字切换为 `<b>Release now for full power!</b>`，低于 85% 显示 "Release to roll"
- 蓄力百分比实时显示在 bodyText

**文件**：`Assets/Game/Scripts/TempleGameHUD.cs`，方法 `ShowWinner`  
**改动**：
- 胜利者名称用金色富文本 `<color=#DCB55B>` 高亮

### 3. 手柄震动反馈系统

**文件**：`Assets/Game/Scripts/TempleXRTabletopRig.cs`  
**新增**：
- `public static TempleXRTabletopRig Active` — 静态访问属性，避免跨脚本循环依赖
- `SendBothHandsHaptic(float amplitude, float duration)` — 双手同步触觉
- `SendDoubleHaptic(float amplitude)` + `DoubleHapticRoutine` — 双脉冲（购买确认）
- `SendVictoryHaptic()` + `VictoryHapticRoutine` — 三段渐强胜利震动（0.40→0.62→0.85）
- 所有 IEnumerator 均用全限定名 `System.Collections.IEnumerator`（与文件已有风格一致）
- 使用 `WaitForSecondsRealtime` 保证低帧率下也能正常工作

**文件**：`Assets/Game/Scripts/TempleCircuitGame.cs`  
**新增 7 处触觉调用**（均通过 `TempleXRTabletopRig.Active?.方法名` 空安全调用）：

| 事件 | 方法 | amplitude | duration |
|------|------|-----------|---------|
| 骰子落地（人类玩家） | `SendBothHandsHaptic` | 0.72 | 0.14s |
| 棋子每步移动（人类） | `SendBothHandsHaptic` | 0.20 | 0.04s |
| 购买房产 | `SendDoubleHaptic` | 0.55 | 双脉冲 |
| 升级房产 | `SendDoubleHaptic` | 0.42 | 双脉冲 |
| 已知祈愿签购买 | `SendBothHandsHaptic` | 0.32 | 0.09s |
| 随机祈愿签揭晓 | `SendBothHandsHaptic` | 0.28 | 0.10s |
| 游戏结束（胜利） | `SendVictoryHaptic` | 三段渐强 | — |

---

## 二、Tripo 调用清单

| task_id | 模型 | 用途 | 状态 |
|---------|------|------|------|
| `a910c397-61d9-449a-8f73-9d7d5a497788` | banana2 | 封面高级感生成（image-to-image） | SUCCESS |

上传 file_token：`file_efba5372-8d3b-447d-8774-f46b5c340414`  
输入：原版 TempleStartCover.png（2752×1536）  
输出尺寸：2752×1536，RGB，无水印  

---

## 三、性能影响评估

由于磁盘剩余 < 15 GB，无法启动 Unity 进行运行时 Profiler 测量。以下为静态分析：

| 改动项 | CPU 影响 | GPU 影响 | 内存影响 |
|--------|---------|---------|---------|
| 封面新图（PNG 5.8MB，同尺寸） | 无 | 加载期同等 | 纹理内存与原版相同 |
| 蓄力条颜色插值 | 极微（每帧 Lerp+SetColor 一次，仅蓄力期） | 无 | 无 |
| 震动调用 7 处 | 极微（PICO XR SendHapticImpulse） | 无 | 无协程堆栈泄漏风险 |

结论：v2 改动不涉及新材质、新粒子、Draw Call 增加或纹理内存增加，帧率预期不受影响。

---

## 四、SmokeCheck 安全性

已逐条核查 `TempleDevelopmentSmokeCheck.cs` 检验项：

- 封面过渡（CoverTransition）：仅换贴图，触发器逻辑未变 ✓  
- `fameToWin == 8`：TempleCircuitGame.cs 未修改胜利数值 ✓  
- `roundText.text.Contains("8 Merit To Win")`：HUD 文本格式未改 ✓  
- `playerPropertyHoverText.text.Contains("No Properties Owned")`：HUD 悬停未改 ✓  
- 棋子可见、房产列表、悬停、射线碰撞、文字适配、骰子面板分离：均未触碰 ✓  

---

## 五、对比图路径

| 类型 | 路径 |
|------|------|
| 封面新版（after） | `VisualQA/Polish-20261022/cover_after.png` |

注：before 图为原版封面（总控 v1 结论的本地排字版本）。HUD 运行画面对比截图需要 Unity 运行时，因磁盘 < 15 GB 无法启动 Unity，留待总控构建后截图补充。

---

## 六、未完成项与风险

1. **运行时截图**：需 Unity 运行才能截取 HUD 蓄力色变、胜利金色名称等效果。总控构建后请补充 VisualQA/。
2. **SmokeCheck 运行验证**：需在 Unity Editor 中手动执行，当前无法运行 Unity。
3. **封面 button 视觉对齐**：Tripo 生成图的 START/EXIT 装饰按钮与代码 hit 区视觉对齐属于近似，精确像素级对齐需在设备上核验。

---

## 七、构建方法（供总控执行）

**完整命名空间.类.方法**：  
`TempleCircuitEditor.TemplePicoProjectSetup.BuildPicoApk`

**APK 输出路径**：  
`Builds/EN/IncenseCrossing-EN-PICO.apk`

**包名**：`com.templecircuit.monopoly.en`

**构建命令参考**（来自 EN_DELIVERY.md v1）：
```
lockf /tmp/en-unity-A.lock \
  /Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -projectPath /Users/ld/Documents/Codex/EnglishEditions/IncenseCrossing-EN \
  -batchmode -quit -buildTarget Android \
  -executeMethod TempleCircuitEditor.TemplePicoProjectSetup.BuildPicoApk \
  -logFile /tmp/incense-crossing-en-build.log
```

构建后务必验证：
- `aapt dump badging` 包名以 `.en` 结尾
- `aapt dump xmltree` 含 `pvr.app.type` 和 `use.pxr.sdk`
- 安装：`lockf /tmp/pico-emulator.flock adb -s emulator-5554 install -r Builds/EN/IncenseCrossing-EN-PICO.apk`
