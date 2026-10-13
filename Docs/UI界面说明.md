# 余音 · UI 界面说明

对应：主菜单 / 设置 / 创作团队 / 暂停菜单四个界面，以及音量口径、分辨率适配、文件边界、UI 规范的约定。

## 一、界面清单

| 场景 | 内容 | 入口 |
| --- | --- | --- |
| `Assets/Scenes/MainMenu.unity` | 标题 + 开始 / 制作名单 / 设置 / 退出 | **Build Settings 第 0 个（启动场景）** |
| `Assets/Scenes/Settings.unity` | 主音量 / 音乐 / 音效 / 亮度 + 返回 | 主菜单「设置」 |
| `Assets/Scenes/Credits.unity` | 创作团队名单（名字暂时留空） + 返回 | 主菜单「制作名单」 |
| `Assets/Scenes/Pause.unity` | 暂停菜单（继续 / 设置 / 退出）+ 场景内设置面板 | 游戏内 ESC，或 `PauseDirector.PauseGame()` |

## 二、代码在哪

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/UI/MainMenuUI.cs` | 主菜单按钮：开始 / 制作名单 / 设置 / 退出 |
| `Assets/Scripts/UI/SettingsMenuUI.cs` | 设置界面的音量**转发** + 亮度（亮度暂未实现） |
| `Assets/Scripts/UI/PauseMenuUI.cs` | 暂停菜单：主面板 / 设置面板互斥 + 音量转发 |
| `Assets/Scripts/UI/BackToSceneButton.cs` | 通用「返回」按钮（目标场景在 Inspector 填，自带场景名校验） |
| `Assets/Scripts/UI/ButtonHoverScale.cs` | 悬停放大效果（**矩形判定，不依赖 Raycast Target**） |
| `Assets/Scripts/UI/PauseButtonHUD.cs` | 游戏内暂停时钟的**运行时挂载**（见第五节） |
| `Assets/Scripts/UI/GameClockUI.cs` | 暂停时钟的两根指针 |
| `Assets/Scripts/Audio/BGMManager.cs` | 主菜单 BGM（早期实现，现由 `AudioManager` 接管） |
| `Assets/Resources/UI/PauseButtonHUD.prefab` | 暂停时钟预制件（Canvas + 圆盘 + 分针 + 秒针） |

## 三、和程序的接口

### 3.1 音量：只转发，不自己存

音量真值在 `AudioManager`（`GameRoot.Instance.Audio`），UI **只调这几个方法**：

| 界面操作 | 调用 |
| --- | --- |
| 主音量滑条 | `Audio.SetMasterVolume(float)` |
| 音乐滑条 | `Audio.SetMusicVolume(float)` |
| 音效滑条 | `Audio.SetSfxVolume(float)` |
| 静音开关 | `Audio.SetMuted(bool)` |
| 回显当前值 | `Audio.MasterVolume` / `MusicVolume` / `SfxVolume` / `IsMuted`（只读属性） |

**明确不做**（《音频接口说明》禁止，做了会出问题）：

- ❌ 不碰 `AudioListener.volume` —— 它是全局乘数，会和 `MasterVolume` 相乘
- ❌ 不碰 `AudioMixer.SetFloat` —— `AudioManager` 不用 mixer，直接算 `AudioSource.volume`
- ❌ 不自己存音量 `PlayerPrefs` —— 键名归 `AudioManager`（`yuyin_audio_master` / `_music` / `_sfx` / `_muted`）
- ✅ **只有亮度**留在 UI 侧（键 `Brightness`，不属于音频）

### 3.2 暂停

| 按钮 | 方法 |
| --- | --- |
| 继续游戏 | `PauseScreen.OnResumeClicked()` |
| 退出游戏 | `PauseScreen.OnQuitClicked()` |
| 设置 | `PauseMenuUI.OpenSettingsPanel()` —— **不走 `PauseDirector.OpenSettings()`，原因见第四节** |
| 设置面板的返回 | `PauseMenuUI.CloseSettingsPanel()` |

游戏内暂停：`PauseDirector.Instance.PauseGame()`。
⚠️ `PauseDirector.CanPause` 只在**玩家可操作阶段**为真（NPC 行动时不开放暂停，避免打断演出）—— **设计如此**。

## 四、暂停里的「设置」为什么做成面板

`PauseDirector.OpenSettings()` 是直接 `LoadScene("Settings", Single)`，
而 `Settings` 场景的返回按钮回的是**主菜单** —— 在暂停流程里会掉到主菜单，
而且游戏还停在 `timeScale = 0` + `GameState.Paused` 的坏状态。

所以暂停界面的「设置」做成**暂停场景内部的一个面板**：
不切场景、不破坏暂停状态。（尽管按钮和滑块组件是从Settings.unity里复制来的）`主菜单 → 设置` 那条路照旧走 `Settings.unity`。

## 五、暂停时钟（游戏内 HUD）

**需求**：游戏画面上叠加一个会走的时钟，点它 = 暂停；ESC 也能暂停。

**做法**：**不修改 `FirstFloor` / `Backyard` 场景**：

```
Assets/Resources/UI/PauseButtonHUD.prefab     ← 预制件（Canvas + 圆盘 + 分针 + 秒针）
        ↓ PauseButtonHUD.cs
[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]   ← 游戏启动时自动执行，不用挂在任何物体上
        ↓ Resources.Load("UI/PauseButtonHUD") → Instantiate → DontDestroyOnLoad
游戏里出现时钟（只在 FirstFloor / Backyard）
```

**为什么不在场景里摆**：
1. `Tools → 余音 → 重建基础场景` 会**重建 `FirstFloor.unity` 和 `Backyard.unity`，并移除所有场景表现层** → 摆进去的 UI 会被清掉
2. 那两个场景由程序侧维护，两边同时改会 git 冲突

**同一套路程序侧也在用**：`PromptOverlay` / `DebugOverlay` / `SceneFadeOverlay` 都不在任何场景里。

**实现细节**：
- 游戏场景里**既没有 Canvas 也没有 EventSystem**（游戏 HUD 是 IMGUI 画的）
  → `PauseButtonHUD` 会在游戏场景里**补一个 EventSystem**（不常驻，随场景销毁）
- 指针用 `Time.unscaledTime` → `timeScale = 0` 时不冻
- 时钟只累计「游戏中且未暂停」的时间
- ESC 触发暂停是 `PauseDirector` 是主程序预先加好的

## 六、分辨率适配

所有 UI 场景的 `Canvas` 统一：

```
Canvas Scaler
  UI Scale Mode        = Scale With Screen Size
  Reference Resolution = 1920 x 1080
  Screen Match Mode    = Expand
```

锚点规矩：

| 元素类型 | 锚点 | 例子 |
| --- | --- | --- |
| 居中的 | `middle-center`，`Pos X/Y = 0` | 按钮组、标题 |
| 贴边的 | 对应的边 / 角 | 返回按钮 → `top-right` |
| 要铺满的 | `stretch-stretch`，四边填 0 | 压暗底、面板底 |

- ⚠️ `Shift` + `Alt` 点锚点预设**会同时改 `Pivot`** → **改完必须回头检查 Pivot**
- 旋转类元素转轴放**根部** `(0.5, 0)`；表盘/圆盘类 `(0.5, 0.5)`
- 动画一律用 `Time.unscaledTime`

## 七、UI 规范

### 7.1 命名

| 类型 | 规范 | 现有 |
| --- | --- | --- |
| 场景 | `PascalCase` | `MainMenu` / `Settings` / `Credits` / `Pause` |
| 面板容器 | `<用途>Panel` | `MainPanel` / `SettingsPanel` |
| 按钮 | `<动作>Button` | `ResumeButton` / `SettingsButton` / `QuitButton` / `BackButton` |
| 滑条 | **`<通道>Slider`** | `MasterSlider` / `MusicSlider` / `SFXSlider` / `BrightnessSlider` |
| 开关 | `<用途>Toggle` | `MuteToggle` |
| 脚本 | `<界面>UI.cs` / `<用途>Button.cs` | — |
| 按钮回调 | `On<动作>Clicked()` | ✅ 已统一 |
| 滑条回调 | `On<通道>Changed(float)` | ⚠️ `SettingsMenuUI.OnSFXChanged` 大写，其余小写 |
| 开关回调 | `On<用途>Toggled(bool)` | — |
| 面板开关 | `Open<用途>Panel()` / `Close<用途>Panel()` | — |

> ⚠️ **滑条/开关/面板一定要起有意义的名字** —— 曾有 4 个滑条全叫 `Slider`，
> 在 Inspector 里拖引用时分不清，导致 `sfxSlider` 漏挂（症状：拖了没反应、重进又回默认值0.5）。

### 7.2 脚本

| # | 规定 |
| --- | --- |
| 1 | 每个脚本顶部有 XML 头注释：职责 / **明确不做** / 怎么用 / 依赖 |
| 2 | 每个 `public` 字段有 `[Tooltip]`，并用 `[Header]` 分组 |
| 3 | **每个 `public` 字段访问前必须 null 保护**，缺失时 `Debug.LogWarning` |
| 4 | 只转发，不持真值（音量真值在 `AudioManager`） |
| 5 | 滑条 `Min/Max` 一律 **0 ~ 1** |
| 6 | 脚本存成 **UTF-8** |

### 7.3 按钮尺寸

按钮高度不要直接按「每字宽度」推，因为**宽度和高度用的不是同一个量**：

- 宽度 = 字数 × 每个字的**前进宽度**（中文全角 ≈ 1 个字号/字）
- 高度 = 行数 × **行高**（ascent + descent，通常比字号大 10~30%）

**最稳的做法**：选中 `Text (TMP)`，看 Inspector 最下面「布局属性」里的
**`Preferred Width` / `Preferred Height`**，按钮尺寸 ≥ 这两个数即可。

⚠️ 如果 `Preferred Width` 超过按钮宽度，TMP 会**逐字换行**，需要的高度直接翻倍。


### 7.4 射线（Raycast Target）

> **一个按钮里只留一个吃射线的东西 = 按钮自己的 Image；`Text (TMP)` 一律关掉。**

- 按钮 `Image → Raycast Target` ✅ 勾上（即使 `Color.a = 0` 也能接射线）
- `Text (TMP) → Extra Settings → Raycast Target` ❌ 取消
- `Image → Alpha Hit Test Minimum Threshold` 保持 **0**

## 八、文件边界（重要）

### ✅ UI 侧可以自由改
- `Assets/Scenes/MainMenu.unity`、`Settings.unity`、`Credits.unity`
- `Assets/Scenes/Pause.unity` ← 程序侧邀请 UI 在此加界面（见《暂停说明》）
- `Assets/Scripts/UI/*`、`Assets/Scripts/Audio/BGMManager.cs`
- `Assets/Resources/UI/*`、`Assets/Fonts/*`、`Assets/TextMesh Pro/*`

### ⚠️ 共享文件 —— 改完必须告知程序侧
- `ProjectSettings/EditorBuildSettings.asset`
  - 加场景**必须**改这里；**只追加**，不要重排 / 删别人的
  - 万一冲突：**两边都保留**（合并两个场景列表）
- `Packages/manifest.json`、`Packages/packages-lock.json`

### ❌ 不要动
- `Assets/Scenes/FirstFloor.unity`、`Backyard.unity`（会被「重建基础场景」清空）
- `Assets/Scripts/Core/*`（`GameRoot` / `PauseDirector` / `AudioManager` …）
- `Assets/Prefabs/*`（程序侧工具按硬编码路径管理，且 `Resources.Load` 读不到）

### 要往游戏里加 UI 怎么办
做成 **prefab**（新文件，零冲突）→ 用 `[RuntimeInitializeOnLoadMethod]` 运行时挂载（见第五节）

## 九、常见坑

| # | 坑 | 后果 |
| --- | --- | --- |
| 1 | 多个滑条/按钮都叫默认名（`Slider` / `Button`） | 拖引用时分不清 → **漏挂 / 挂错** |
| 2 | **按钮矩形比文字小** | 可点 / 可悬停范围比看起来小（见 7.3） |
| 3 | 预制件里只有按钮没有 `Canvas` | UI **完全不渲染** |
| 4 | 图形没勾 `Raycast Target` | **点不到** |
| 5 | 游戏场景**没有 `EventSystem`** | 按钮看得见点不动（`PauseButtonHUD` 已自动补） |
| 6 | 指针/动画用 `Time.time` | `timeScale = 0` 时冻住（要用 `unscaledTime`） |
| 7 | UI 预制件放 `Assets/Prefabs/` | 程序侧工具的地盘 + `Resources.Load` 读不到 |
| 8 | **改方法名** | `Settings.unity` 里的 `OnBackClicked` 是**按方法名**挂的，改名会让 Inspector 变 `Missing` |
| 9 | 忘了 `Ctrl+S` 保存场景 | 改动不生效 |
| 10 | 没连引用就点按钮 | 静默无效（所以规范 7.2 第 3 条要求 null 保护 + 打警告） |

## 十、已知问题

| 问题 | 说明 |
| --- | --- |
| 亮度滑条是空壳 | `SettingsMenuUI` / `PauseMenuUI` 的 `OnBrightnessChanged` 只打日志。**要么实现，要么先隐藏该滑条** |
| 音量重启会回默认值 | `AudioManager` 的 `SetMasterVolume` / `SetMusicVolume` / `SetSfxVolume` 只 `PlayerPrefs.SetFloat`、**没有 `PlayerPrefs.Save()`**（`SetMuted` 有）。已反馈程序侧 |
| 暂停界面残留淡入遮罩 | `SceneFadeOverlay`（挂 `GameRoot`，IMGUI 全屏黑）切场景淡入后会留在暂停界面。已反馈程序侧 |

## 十一、怎么加一个新界面

1. 新建场景 → 加 `Canvas`（`Screen Space - Overlay` + `CanvasScaler` 见第六节）+ `EventSystem`
2. 建 UI，按第六节的锚点规矩摆；按钮高度按 7.3 算
3. 写 `Assets/Scripts/UI/<你的>UI.cs`，回调写成 `public void OnXxxClicked()`；字段做 null 保护 + `[Tooltip]`
4. 在按钮 `OnClick()` 里挂**你自己脚本**的方法
5. 返回按钮：挂 `BackToSceneButton.OnBackClicked()`，`Scene Name` 填目标场景
6. **把场景加进 Build Settings** ⚠️ 不在里面的场景 `LoadScene` 会**静默失败**
7. 确认 `MainMenu` 仍然是第 0 个

## 十二、测试

| 位置 | 覆盖 |
| --- | --- |
| 手动（推荐打包 Windows 验） | 主菜单 → 设置 / 制作名单 / 开始 → ESC 暂停 → 继续 / 设置 / 退出；拖动窗口改分辨率看 UI 是否跑偏 |
| `Tests/PlayMode/PausePlayModeTests.cs` | 暂停 / 继续流程（程序侧） |

## 十三、相关文件

| 文件 | 职责 |
| --- | --- |
| `Docs/音频接口说明.md` | 音频唯一真值在 `AudioManager`（3.1 节的依据） |
| `Docs/暂停说明.md` | 暂停系统本身（程序侧）；3.2 / 第四节是它的 UI 侧补充 |
| `Assets/Scripts/Core/AudioManager.cs` | 音量真值 + PlayerPrefs 键 |
| `Assets/Scripts/Core/PauseDirector.cs` | `PauseGame` / `ResumeGame` / `OpenSettings` / `QuitGame` |
| `Assets/Scripts/Core/PauseSnapshot.cs` | 暂停现场（截图 + 回来时的场景 + 站位） |
| `Assets/Scripts/Views/PauseScreen.cs` | 暂停场景表现 + 三个按钮回调 |
| `Assets/Scripts/Data/SfxId.cs` | 音效种类枚举 |