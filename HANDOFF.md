# DINO 美术优化交接文档

## 2026-09-30 战技／进化内容池扩展（Codex）

- 总计 25 战技、28 进化（22 普通／6 稀有）；本轮按照具体清单增加 17 战技，而非验收中的数量笔误 20。
- `ActExpandedPowerCatalog.cs` 是新增效果的主要参数入口；`ActBuildRuntime.cs` 保存计数、临时修正、继承及一次性进房回血记录；`ActBuildEffects.cs` 管理战技执行与命中／击杀／冲刺／下坠钩子。
- 高难奖励门占约 20%，每个大厅最多一个高难门、至少一个普通门，实际房间类别由玩家选择决定。
- 稀有进化同时有 Rarity 与 BossOnly 元数据，生成池和 RunState 获取均验证来源；Boss 尸体奖励点调用专属池，普通奖励、商人、龙蛋都不能获得稀有。Boss 默认 35% 出现一张稀有，最终 Boss 65%，普通库存充足时其余至少两张为普通。
- 稀有选择卡：金色双 DNA、暖色面板、独立边框、粒子、0.65 秒展开；获得时有嵌入代码生成的短音、金色粒子。RunState 获得提示提供 PowerCueRare。更新 HUD 使全部 28 个进化都可悬停查看，新增战技效果有倒计时。
- 普通卡池耗尽时不退化为稀有池；Boss 普通池耗尽后可提供一个剩余稀有，避免卡死。不重复填充已拥有特质。
- 延迟骨雨独立排队，两张连续使用不会覆盖。碎裂不递归碎裂，穿透记录已命中单位，捕食值仅对每个可计数单位生效一次；以后 Boss 召唤物应标记 BuildRewardEligible=false。
- 未使用 Computer Use，真实试玩、音量及 Build 平衡继续由用户验收；程序绘制的残影、震波和身体强化光效可后续细化美术。
- 验证：143 组原生代码测试通过，包括四章混合 Build 模拟；全部离屏界面／动画检查通过，无编译错误。

## 2026-09-27 场景标记清理与区域地名（Codex）

- 用户要求后续按正式成品标准开发：正常游戏不要显示测试区名称、内部房间类别及无功能的边界标记；技术信息仅留在调试入口。
- 删除 `GameCanvas.DrawLevelChrome` 内房间入口／出口的倒 L 图形、区域分隔竖条、常驻房间类型文字及“捷径”标记。它们仅为绘制内容，未修改实体平台、房间数据或事件。
- `ActLevel.Zones` 改用地理名称；新增 `ActRegionPresentation.cs`，首次进入区域淡入、短暂停留后淡出，往返不重复，重开清空发现记录。当前改动是名称与提示呈现，尚未为十个区域各自重绘独立环境美术。
- 保留有真实阻挡的封锁门及敌人清除反馈、危险预警、奖励、Boss 血条和出口，避免隐形墙。普通菜单去掉“生成新的地图布局”等实现说明。
- 40 项游戏测试和离屏检查通过，新增区域边界／淡出／往返／重开检查；未启动 Computer Use。

## 2026-09-27 中文输入法与心形 HP（Codex）

- `GameForm`、`GameCanvas`、`SettingsDialog` 设置窗口级 `ImeMode.Disable`，不切换或修改系统默认键盘布局。
- 新增 `GameKeyboard.cs`：统一解码普通／系统键消息，对 `VK_PROCESSKEY` 优先用 `ImmGetVirtualKey` 取回原键，必要时按扫描码还原，处理左右 Shift 与松键；游戏和绑定菜单共用。
- `ActHudRenderer` 将生命格改回像素心形，保留空心／受伤渐隐／恢复闪亮／低血量提示，当前最大生命仍为 5。
- 40 项玩法测试通过；新增 96 种普通／IME 模拟键消息检查，以及长按去重、松键、冲刺组合、暂停和文本隔离验证。离屏界面检查通过；未操作用户输入法或启动 Computer Use，真实输入法体验由用户验收。

## 2026-09-27 画面内 UI 重排（Codex）

- 移除外部标题、整行状态栏、底部说明和厚边框，Canvas 铺满客户区，默认 1280×720。
- 新增 `ActHudRenderer.cs`，在画面左上显示生命／骨刺／冲刺和临时增益；保留损失渐隐、恢复闪亮和低血量提示。Boss 血条改在屏幕底部绘制，修正旧代码叠加 CameraX 的偏移。
- 按钮移到右上，按文字实测尺寸排版；原生字体与 Canvas HUD 同步缩放，启用 PerMonitorV2。开始／暂停／死亡卡片及按键设置统一深色、淡金色主题，操作说明收进菜单。
- 纵向多余空间由背景和地下景色延展填充，完整保留原 960×360 战斗可视区域、角色比例及所有玩法参数。
- 离屏检查覆盖三种窗口大小、菜单长文字及设置页面；不打开游戏窗口，不使用 Computer Use。玩法源码未改动。

## 2026-09-27 接续更新（Codex）

- 已修复下文列出的三个动画问题：飞行敌人改用碰撞盒对齐的身体／脚爪锚点与 44px 头喙宽度；玩家尾扫加入连续过渡和完整像素下沿限制；Boss 尾扫增加蓄力、挥动、收势缓动，攻击到收势的尾部像素连续。
- 新增 `desktop/ActHudMeter.cs`：HP 与骨刺能量分格显示、明确数值、损失渐隐、恢复闪亮和低血量描边提示。
- `GameForm.cs` 更新死亡卡片和响应式弹窗布局，显示本局时间与击败数；重开和指定 Seed 时显式重置 HUD、残影、粒子与倒地体。
- 新增 `visual-checks` 离屏绘制工具，不打开游戏窗口。运行 `dotnet run --project visual-checks/VisualChecks.csproj -c Release`，输出动画检查图与关卡检查图；检查 501 个尾扫时间采样和 Boss 尾扫的收势边界。
- 背景仅做了离屏层次检查，保留现有视差与色调方案。
- 玩法源码未作修改。原有 40 个测试通过，桌面构建通过；本节以下保留 Claude 的原始交接记录，其中三个“待优化”问题已由本轮处理。

---

**项目路径：** `G:\CODEX\DINO`
**构建命令：** `powershell -NoProfile -ExecutionPolicy Bypass -File build-exe.ps1`
**可执行文件：** 根目录 `恐龙冲刺.exe`（双击直接运行）
**测试命令：** `dotnet run --project native-tests/NativeTests.csproj -c Release`（40 个测试，全部通过）

---

## 本次会话已完成的工作

### 新增的纯视觉文件（不影响任何 Gameplay 逻辑）

| 文件 | 用途 |
|---|---|
| `desktop/ActVisualTheme.cs` | 调色板、OKLCH 手工 10 步中性灰阶、强调色、稳定哈希噪声、画笔/画笔缓存 |
| `desktop/ActParallaxBackdrop.cs` | 多层视差背景：天空渐变→太阳光晕→两层云→远近山脊→遗迹→植被剪影→远景长颈龙与鸟群 |
| `desktop/ActLevelDecor.cs` | 世界空间地面装饰：分层地表、巨石/骨架/头骨/灌木/石碑、平台描边与悬挂藤蔓、前景草丛 |
| `desktop/ActVisualFx.cs` | 特效系统：观察引擎状态，播放落地尘土、冲刺残影（`FxAfterimage`）、攻击拖尾（`FxTrail`）、冲击波（`FxShockwave`）、粒子、敌人倒地体（`FxCorpse`）、屏幕震动 |
| `desktop/ActFxRenderer.cs` | 上述特效的绘制，分前层/后层传递给 `GameCanvas` |

### 修改的文件

| 文件 | 改动内容 |
|---|---|
| `desktop/PixelSprites.cs` | **完全重写**：新增 `DinoPose` / `DinoArm` / `DinoLegs` 结构体；`DrawDinoPosed`（可驱动每个骨骼偏移的通用绘制函数）；`DrawDinoTinted`（着色版本供残影使用）；`DrawDinoDeath`（三段倒地序列）；`DrawMeleeEnemy` / `DrawRangedEnemy` / `DrawFlyingEnemy` / `DrawHeavyEnemy` / `DrawBossEnemy`（全部重绘，各有独立骨架和分状态动作） |
| `desktop/ActSpriteAnimator.cs` | **完全重写**：把所有玩家动作（Idle/Run/Jump/Fall/Dash/Attack1-3/Tail/Stomp/Ranged/Hurt/Dead）映射到 `DinoPose` 关键帧；Squash&Stretch 通过 `GraphicsTransform` 实现，量化到 2px 网格；调用新敌人绘制函数；新增 `DrawEnemySprite` 入口（含命中闪白、位移击退视觉）和 `DrawEnemyBody`（供 `FxCorpse` 复用） |
| `desktop/GameCanvas.cs` | **完全重写**：接入视差背景→地面装饰→FX后层→敌人→弹体→玩家→FX前层→前景草丛的完整渲染管线；Boss 血条升级为动态；奖励图标脉冲；出口门在 Boss 死后发光 |
| `desktop/GameForm.cs` | 在 `Tick` 里加入 `canvas.Tick(wallDt)` 调用，让特效层在 Dying 状态也能继续更新 |

### 未改动（Gameplay 逻辑零改动确认）

以下文件**一个字节都没有改动**：
`ActEngine.cs` / `ActCombat.cs` / `ActEnemies.cs` / `ActLevel.cs` / `ActLevelRandomizer.cs` / `ActRoomEvents.cs` / `ActFeedback.cs` / `ActInputRouter.cs` / `GameEngine.cs` / `GameInputRouter.cs`

---

## 当前状态与已知问题

### 已完成的动画

**玩家**
- Idle（4 帧呼吸 + 偶尔眨眼）
- Run（4 帧步伐，body/head/tail 分别偏移，接触帧有脚跟尘土）
- Jump（起跳腿甩后、顶点腿收起，身体拉伸）
- Fall（腿下垂，快速下落时手臂甩开）
- Dash（蹲伏姿态 + 3 条速度线，残影最多 ~7 帧）
- 三段爪击（各有蓄力/接触/收势关键帧，接触帧有爪痕线，第三击有十字冲击星）
- 尾扫（线段拼接连续弧线，4 分段覆盖两侧）
- 下坠踩踏（空中拉伸 + 气流竖线，落地蹲缩 + 地面裂纹扩散）
- 骨刺（充能 + 发射 + 炮口光）
- 受伤（身体冲击偏移 + 受伤眼）
- 死亡（击飞→跪地→侧躺三段）

**敌人**
- 近战：四帧巡逻/追击步伐，蓄力前扑体，攻击出爪，受击后仰
- 远程：背鳍随蓄力升起，喉部蓄力闪烁，发射手臂伸出
- 飞行（重绘）：四帧扑翼（上冲/过渡高/下冲/过渡低），俯冲翼折叠，张嘴，伸展脚爪
- 重型：装甲骨板 + 白切纹路，蓄力地面刨爪，冲锋前倾，眩晕旋转星
- Boss：Charge/Tail/Quake 各有独立身体语言；第二阶段眼眶变亮 + 侧腹爪痕；Quake 地面阴影扩散

**死亡过渡**
- 敌人死亡时不再瞬间消失，改为 `FxCorpse`：闪白→碎步折叠缩小→淡出闪烁

### 背景（用户要求暂缓）

视差背景代码已写完并接入 `GameCanvas`，但用户指示先专注角色动画，背景可能需要进一步审查（色调、层次感等）。

### 待优化的地方

1. **飞行敌人的碰撞盒与新立绘错位** — 立绘重绘后整体比旧版稍高，但碰撞盒 `Height = 28` 未变；视觉上看上去脚踩在半空。建议把 `HomeY` 的初始化 Y 值（`ActLevel.cs:76` 的 `ActEnemyKind.Flying => 155f`）下移约 10px，不改碰撞盒尺寸。
2. **尾扫线段在极快帧率下偶尔出现过地面线** — 线段方向系数 `dy` 为正时段末端会超出 GroundY。可在 `DrawTailSweep` 中对 `sy` 做 `MathF.Min(sy, y + 52 - 2)` 夹紧。
3. **Boss Tail 动画的尾巴插值感稍生硬** — 从高到低的 `tail` 变量是线性，加一个 `MathF.Sin` 缓出会更流畅。

---

## 给下一个 AI 的提示词

```
你继续做 G:\CODEX\DINO 这个 C# WinForms 横版 ACT 游戏的美术工作。

项目已有完整运行的 Gameplay 逻辑（不要修改）和一套刚完成的视觉层。
构建：powershell -NoProfile -ExecutionPolicy Bypass -File build-exe.ps1
测试：dotnet run --project native-tests/NativeTests.csproj -c Release（40 个测试必须全部通过）

当前视觉层现状：
- desktop/ActVisualTheme.cs         — 调色板与噪声工具
- desktop/ActParallaxBackdrop.cs    — 视差背景（已写完，用户说先不急）
- desktop/ActLevelDecor.cs          — 地面装饰
- desktop/ActVisualFx.cs            — 特效系统（粒子/拖尾/冲击波/残影/敌人倒地体）
- desktop/ActFxRenderer.cs          — 特效渲染
- desktop/PixelSprites.cs           — 像素立绘（玩家 + 5 类敌人，全部重绘）
- desktop/ActSpriteAnimator.cs      — 动画驱动（玩家全动作 + 敌人分状态动作）

已知待修复的小问题（见 HANDOFF.md）：
1. 飞行敌人立绘与碰撞盒有约 10px 垂直错位
2. 尾扫末段偶尔穿地
3. Boss Tail 动作尾巴插值生硬

用户要求的下一步工作（等用户确认后再做）：
- 背景视差层的色调和层次感审查
- UI 优化（HP 格、骨刺能量显示、死亡界面）
- 进一步打磨任何动画细节

原则：
- Gameplay 文件（ActEngine/ActCombat/ActEnemies/ActLevel/ActLevelRandomizer/ActRoomEvents/ActFeedback/ActInputRouter）一个字节都不能改。
- 新视觉效果优先写成独立的 Visual/Animation/Particle 组件，通过读取引擎 public 属性驱动，不向引擎写入任何状态。
- 每次改完必须 dotnet build 通过且 40 个原有测试全部通过再汇报结果。
- 使用中文回复用户。
```
