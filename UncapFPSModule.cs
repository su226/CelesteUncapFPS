using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.UncapFPS {
    public class UncapFPSModule : EverestModule {
        public static UncapFPSModule Instance { get; private set; }

        public override Type SettingsType => typeof(UncapFPSModuleSettings);
        public static UncapFPSModuleSettings Settings => (UncapFPSModuleSettings) Instance._Settings;
        private ILHook FixCelesteTASHook;
        private bool CollectingHeartGem;

        public UncapFPSModule() {
            Instance = this;
#if DEBUG
            Logger.SetLogLevel(nameof(UncapFPSModule), LogLevel.Verbose);
#else
            Logger.SetLogLevel(nameof(UncapFPSModule), LogLevel.Info);
#endif
        }

        public override void Load() {
            SetFpsLimit();
            On.Celeste.Pico8.Emulator.End += OnPico8End;
            On.Celeste.Pico8.Emulator.CreatePauseMenu += OnPico8Pause;
            On.Celeste.Pico8.Emulator.ResetScreen += OnPico8Reset;
            Everest.Events.Level.OnEnter += OnLevelEnter;
            Everest.Events.Level.OnExit += OnLevelExit;
            Everest.Events.Level.OnPause += OnLevelPause;
            Everest.Events.Level.OnUnpause += OnLevelUnpause;
            On.Celeste.Level.StartCutscene += OnStartCutscene;
            On.Celeste.Level.EndCutscene += OnEndCutscene;
            On.Celeste.Level.TransitionRoutine += OnTransition;
            On.Celeste.HeartGem.CollectRoutine += OnHeartGemCollect;
            On.Celeste.Postcard.DisplayRoutine += OnPostcard;
            Everest.Events.Player.OnSpawn += OnPlayerSpawn;
            Everest.Events.Player.OnDie += OnPlayerDie;
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
                Name = "CelesteTAS",
                Version = new Version(3, 25, 11),
            })) {
                Logger.Log(LogLevel.Info, "UncapFPS", "CelesteTAS detected, fixing DivideByZeroException from TAS.GameInfo.ConvertMicroSecondToFrames");
                try {
                    FixCelesteTAS();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, "UncapFPS", "Failed to patch CelesteTAS.");
                    Logger.LogDetailed(e, "UncapFPS");
                }
            }
        }

        public override void Unload() {
            ResetFpsLimit();
            On.Celeste.Pico8.Emulator.End -= OnPico8End;
            On.Celeste.Pico8.Emulator.CreatePauseMenu -= OnPico8Pause;
            On.Celeste.Pico8.Emulator.ResetScreen -= OnPico8Reset;
            Everest.Events.Level.OnEnter -= OnLevelEnter;
            Everest.Events.Level.OnExit -= OnLevelExit;
            Everest.Events.Level.OnPause -= OnLevelPause;
            Everest.Events.Level.OnUnpause -= OnLevelUnpause;
            On.Celeste.Level.StartCutscene -= OnStartCutscene;
            On.Celeste.Level.EndCutscene -= OnEndCutscene;
            On.Celeste.Level.TransitionRoutine -= OnTransition;
            On.Celeste.Postcard.DisplayRoutine -= OnPostcard;
            Everest.Events.Player.OnSpawn -= OnPlayerSpawn;
            Everest.Events.Player.OnDie -= OnPlayerDie;
            FixCelesteTASHook?.Dispose();
        }

        private void SetFpsLimit() {
            if (Settings.FpsLimit == 0) {
                Celeste.Instance.IsFixedTimeStep = false;
            } else {
                Celeste.Instance.IsFixedTimeStep = true;
                Celeste.Instance.TargetElapsedTime = new TimeSpan((long)Math.Round(1.0 / Settings.FpsLimit * TimeSpan.TicksPerSecond));
            }
        }

        private void ResetFpsLimit() {
            Celeste.Instance.IsFixedTimeStep = true;
            Celeste.Instance.TargetElapsedTime = new TimeSpan(166667L);
        }

        public override void SaveSettings() {
            base.SaveSettings();
            SetFpsLimit();
        }

        private void OnPico8End(On.Celeste.Pico8.Emulator.orig_End orig, Pico8.Emulator self) {
            SetFpsLimit();
            orig(self);
        }

        private void OnPico8Pause(On.Celeste.Pico8.Emulator.orig_CreatePauseMenu orig, Pico8.Emulator self) {
            SetFpsLimit();
            orig(self);
            var data = DynamicData.For(self);
            var menu = data.Get<TextMenu>("pauseMenu");
            Action orig_OnCancel = menu.OnCancel;
            menu.OnCancel = () => {
                ResetFpsLimit();
                orig_OnCancel();
            };
            Action orig_OnESC = menu.OnESC;
            menu.OnESC = () => {
                ResetFpsLimit();
                orig_OnESC();
            };
            Action orig_OnPause = menu.OnPause;
            menu.OnPause = () => {
                ResetFpsLimit();
                orig_OnPause();
            };
        }

        private void OnPico8Reset(On.Celeste.Pico8.Emulator.orig_ResetScreen orig, Pico8.Emulator self) {
            ResetFpsLimit();
            orig(self);
        }

        private void OnLevelEnter(Session session, bool fromSaveData) {
            if (Settings.ApplyTo != EnumApplyTo.All) {
                ResetFpsLimit();
            }
        }

        private void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) { 
            SetFpsLimit();
        }

        private void OnLevelPause(Level self, int startIndex, bool minimal, bool quickReset) {
            SetFpsLimit();
        }

        private void OnLevelUnpause(Level self) {
            if (self.InCutscene || CollectingHeartGem ? Settings.ApplyTo == EnumApplyTo.UIOnly : Settings.ApplyTo != EnumApplyTo.All) {
                ResetFpsLimit();
            }
        }

        private void OnStartCutscene(On.Celeste.Level.orig_StartCutscene orig, Level self, Action<Level> onSkip, bool fadeInOnSkip, bool endingChapterAfterCutscene, bool resetZoomOnSkip) {
            if (Settings.ApplyTo != EnumApplyTo.UIOnly) {
                SetFpsLimit();
            }
            orig(self, onSkip, fadeInOnSkip, endingChapterAfterCutscene, resetZoomOnSkip);
        }

        private void OnEndCutscene(On.Celeste.Level.orig_EndCutscene orig, Level self) {
            DynamicData data = new DynamicData(self);
            if (Settings.ApplyTo != EnumApplyTo.All && !data.Get<bool>("endingChapterAfterCutscene")) {
                ResetFpsLimit();
            }
            orig(self);
        }

        private IEnumerator OnTransition(On.Celeste.Level.orig_TransitionRoutine orig, Level self, LevelData next, Vector2 direction) {
            if (Settings.ApplyTo != EnumApplyTo.UIOnly) {
                SetFpsLimit();
            }
            IEnumerator e = orig(self, next, direction);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            if (Settings.ApplyTo != EnumApplyTo.All) {
                ResetFpsLimit();
            }
        }

        private IEnumerator OnHeartGemCollect(On.Celeste.HeartGem.orig_CollectRoutine orig, HeartGem self, Player player) {
            CollectingHeartGem = true;
            if (Settings.ApplyTo != EnumApplyTo.UIOnly) {
                SetFpsLimit();
            }
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            CollectingHeartGem = false;
            if (Settings.ApplyTo != EnumApplyTo.All) {
                ResetFpsLimit();
            }
        }

        private IEnumerator OnPostcard(On.Celeste.Postcard.orig_DisplayRoutine orig, Postcard self) {
            if (Settings.ApplyTo != EnumApplyTo.UIOnly) {
                SetFpsLimit();
            }
            IEnumerator e = orig(self);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            if (Settings.ApplyTo != EnumApplyTo.All) {
                ResetFpsLimit();
            }
        }

        private void OnPlayerSpawn(Player self) {
            if (Settings.ApplyTo != EnumApplyTo.All) {
                ResetFpsLimit();
            }
        }

        private void OnPlayerDie(Player self) {
            if (Settings.ApplyTo != EnumApplyTo.UIOnly) {
                SetFpsLimit();
            }
        }

        private void FixCelesteTAS() {
            FixCelesteTASHook = new ILHook(typeof(TAS.GameInfo).GetMethod("ConvertMicroSecondToFrames", BindingFlags.NonPublic | BindingFlags.Static), FixCelesteTASManipulator);
        }

        private void FixCelesteTASManipulator(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before, inst => inst.OpCode == OpCodes.Div);
            cursor.Emit(OpCodes.Ldc_I8, 1L);
            cursor.Emit(OpCodes.Call, typeof(Math).GetMethod("Max", new Type[] { typeof(long), typeof(long) }));
        }
    }
}
