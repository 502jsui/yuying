# DEV LOG.02 - 2D 雨天效果

日期：2026-10-04  
参考：[2D游戏中的雨天效果 DEV LOG.02](https://www.bilibili.com/video/BV1s6rDBpEvo/)  
参考作者：Mushreb  
参考引擎：Godot 4.5.1 / C#

## 目标

这版先完成一个不依赖外部美术的 Unity 2D 雨系统，视觉拆解为：

1. 前后两层雨线，形成空间层次
2. 地面初始水花
3. 扩散并淡出的雨水涟漪
4. 地面湿润反光和浅水洼
5. 冷色环境遮罩
6. 后院可选的闪电闪白

当前方案全部由 `ParticleSystem`、运行时生成的 Sprite 和程序化动画组成，不依赖 `Assets/Art`。

## 实现

核心脚本：

- `Assets/Scripts/Views/RainController.cs`
- `Assets/Scripts/Views/RainRipple2D.cs`

`RainController` 在运行时自动创建：

- `Rain_Background`：慢速、较小、低透明度的背景雨
- `Rain_Foreground`：快速、较大、排序在最前的近景雨
- `Rain_Splash`：贴近地面的横向水花粒子
- `Wet_Ground_Sheen`：地面湿润反光层
- `Puddle_0...6`：程序化浅水洼
- `Rain_Tint`：跟随摄像机的冷色环境遮罩
- `Rain_Lightning`：可选闪电闪白层

雨线使用 `ParticleSystemRenderMode.Stretch`，通过 X/Y 非等比粒子尺寸形成细长雨丝。水花使用扁平的粒子矩形。涟漪使用运行时生成的环形纹理，再由 `RainRipple2D` 控制扩散和透明度。

## 场景参数

| 场景 | 强度 | 背景雨 | 近景雨 | 闪电 | 冷色遮罩 |
| --- | ---: | ---: | ---: | --- | ---: |
| FirstFloor | 无雨 | - | - | - | - |
| Backyard | 1.00 | 200 条 | 110 条 | 开启 | 0.14 |

一楼不生成雨效；雨天参数集中在后院的 `Environment/Rain` 对象上，主要可调项：

- `intensity`：总强度
- `backgroundRate` / `foregroundRate`：雨量
- `backgroundSpeed` / `foregroundSpeed`：下落速度
- `windSpeed`：横向风
- `rainColor` / `splashColor` / `tintColor`
- `rippleInterval` / `rippleLife` / `rippleSize`
- `lightningEnabled` / `minLightningInterval` / `maxLightningInterval`

## 验证

Unity Play Mode 烟雾测试通过：

```text
RAIN_TEST Built=True Background=True Foreground=True Splash=True
```

实际渲染预览：

![Rain Backyard](Images/DevLog_02_RainBackyard.png)

## 第二轮调参

根据实际渲染结果做了第二轮收敛：

- 雨速提高：背景雨 `10 -> 18`，近景雨 `14 -> 26`
- 雨滴在整个摄像机高度范围内生成，避免中下部雨量断档
- 雨线改回稳定的竖向 Billboard 粒子，避免 Stretch 方向异常
- 横向风从 `2.8` 降到 `1.1`，只保留自然风偏
- 增加速度、长度、透明度和噪声的随机差
- 涟漪尺寸 `1.05 -> 0.45`，寿命 `0.55 -> 0.40`
- 涟漪高度压缩为宽度的 `0.32`，更符合地面水花
- 水花粒子缩小：宽 `0.24 -> 0.12`，高 `0.045 -> 0.025`
- 冷色遮罩移到前景雨线下方，提升雨丝可读性

调参后的 Play Mode 验证：

```text
RAIN_TEST Built=True Background=True Foreground=True Splash=True
BgCount=174 FgCount=82 SplashCount=10
```
## 第三轮调参

目标：斜雨、轨迹清楚、密集快速、雨线适中偏短。

- 雨线从 `ParticleSystem` 改为 `RainStreakField2D` 程序化对象池
- 每条雨线按速度方向旋转，统一斜向下落
- 背景雨速度 `(5.6, -22)`，数量 `200`
- 前景雨速度 `(8, -32)`，数量 `110`
- 背景雨线长度 `0.45`，前景雨线长度 `0.65`
- 保留少量随机角度抖动，避免完全机械
- 地面涟漪继续保持小尺寸和压扁椭圆

最终 Play Mode 验证：

```text
RAIN_TEST Built=True Background=True Foreground=True Splash=True
BgCount=200 FgCount=110 SplashCount=11
```
## 已知限制

- 一楼目前是简化室内，雨层没有做建筑遮罩，后续需要按房间/窗户区域限制雨线。
- 水洼是程序化几何，不是真实倒影，后续可接折射/反射 Shader。
- 闪电暂时只有屏幕闪白，没有雷声和相机震动。
- 涟漪为独立对象，后续高强度场景建议改为对象池。

## 下一步

1. 加入室内/室外雨区遮罩
2. 增加雷声、雨声和音量包络
3. 给闪电加相机震动和背景亮度联动
4. 增加地面倒影/闪烁 Shader
5. 将涟漪改为对象池并加入性能统计



