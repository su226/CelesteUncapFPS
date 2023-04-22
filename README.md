# UncapFPS
Remove or change FPS limit in Celeste. May affect physics slightly, making some techniques hard or impossible.

FPS too high may crash some mods. Include a IL-patch for CelesteTAS DivideByZeroException from TAS.GameInfo.ConvertMicroSecondToFrames.

`All` mode will apply to everywhere except PICO-8. (Because game speed of PICO-8 is same with FPS.) `UIOnly` mode will only apply to UI. `UIAndCutscene` will also apply to death transition, room transition, postcard, heart gem and (skipable) cutscene.

移除 FPS 限制或设置自定义 FPS 限制。会对游戏物理有些许影响，让一些高级技巧变得困难或不可能。

FPS 过高可能会导致部分 Mod 崩溃。本 Mod 包含一个修复 CelesteTAS 的 TAS.GameInfo.ConvertMicroSecondToFrames 方法抛出 DivideByZeroException 的 IL 补丁。

`All` 模式会应用于除了 PICO-8 的所有地方。（因为 PICO-8 的速度和帧率挂钩）`UIOnly` 模式只会应用于界面，`UIAndCutscene` 模式还会应用于死亡过渡、切板过渡、明信片、水晶之心和（可跳过的）过场动画。

See: [`TAS/GameInfo.cs` of EverestAPI/CelesteTAS-EverestInterop](https://github.com/EverestAPI/CelesteTAS-EverestInterop/blob/c9e84e6e1fe7af33fade1c360ed809828b8ea227/CelesteTAS-EverestInterop/Source/TAS/GameInfo.cs#L555)
```csharp
private static long ConvertMicroSecondToFrames(long time) {
  // Original, may throw DivideByZeroException when Ticks is zero. (Which means FPS is too high and RawDeltaTime is to small.)
  return time / TimeSpan.FromSeconds(Engine.RawDeltaTime).Ticks;
  // Patched
  return time / Math.Max(TimeSpan.FromSeconds(Engine.RawDeltaTime).Ticks, 1L);
}
```
