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
        private ILHook MiniHeartHook;
        private bool CutsceneFPSWhenUnpause;
        private bool UIFPSWhenUnpause;
        private bool CollabUtils2OnHookAdded;

        public UncapFPSModule() {
            Instance = this;
#if DEBUG
            Logger.SetLogLevel("UncapFPS", LogLevel.Verbose);
#else
            Logger.SetLogLevel("UncapFPS", LogLevel.Info);
#endif
        }

        public override void Load() {
            SetUIFPSLimit();
            On.Celeste.Pico8.Emulator.End += OnPico8End;
            On.Celeste.Pico8.Emulator.CreatePauseMenu += OnPico8Pause;
            On.Celeste.Pico8.Emulator.ResetScreen += OnPico8Reset;
            On.Celeste.LevelLoader.StartLevel += OnLevelEnter;
            Everest.Events.Level.OnExit += OnLevelExit;
            Everest.Events.Level.OnPause += OnLevelPause;
            Everest.Events.Level.OnUnpause += OnLevelUnpause;
            On.Celeste.Level.StartCutscene += OnStartCutscene;
            On.Celeste.Level.EndCutscene += OnEndCutscene;
            On.Celeste.Level.CancelCutscene += OnCancelCutscene;
            On.Celeste.Mod.Entities.CustomNPC.OnTalkEnd += OnCustomNPCTalkEnd;
            On.Celeste.Level.TransitionRoutine += OnTransition;
            On.Celeste.HeartGem.CollectRoutine += OnHeartGemCollect;
            On.Celeste.Cassette.CollectRoutine += OnCassetteCollect;
            On.Celeste.ForsakenCitySatellite.UnlockGem += On1AHeartUnlock;
            On.Celeste.UnlockedPico8Message.Routine += OnPico8Unlock;
            On.Celeste.Player.CassetteFlyBegin += OnBubbleBegin;
            On.Celeste.Player.CassetteFlyEnd += OnBubbleEnd;
            On.Celeste.Player.IntroRespawnEnd += OnPlayerRespawn;
            Everest.Events.Player.OnDie += OnPlayerDie;
            On.Celeste.Lookout.LookRoutine += OnLookout;
            On.Celeste.CS00_Ending.EndingCutsceneDelay.ctor += OnPrologueEnding;
            On.Celeste.Editor.MapEditor.ctor += OnEditMap;
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
                Name = "CollabUtils2",
                Version = new Version(1, 8, 12),
            })) {
                CollabUtils2OnHookAdded = true;
                On.Celeste.OuiChapterPanel.Enter += OnOuiChapterPanelEnter;
                On.Celeste.OuiChapterPanel.Leave += OnOuiChapterPanelLeave;
                On.Celeste.OuiJournal.Enter += OnOuiJournalEnter;
                On.Celeste.OuiJournal.Leave += OnOuiJournalLeave;
                On.Monocle.Entity.Added += OnEntityAdded;
                On.Monocle.Entity.Removed += OnEntityRemoved;
                try {
                    AddMiniHeartHook();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, "UncapFPS", "Failed to patch CollabUtils2 MiniHeart SmashRoutine.");
                    Logger.LogDetailed(e, "UncapFPS");
                }
            }
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
            SetVanillaFPSLimit();
            On.Celeste.Pico8.Emulator.End -= OnPico8End;
            On.Celeste.Pico8.Emulator.CreatePauseMenu -= OnPico8Pause;
            On.Celeste.Pico8.Emulator.ResetScreen -= OnPico8Reset;
            On.Celeste.LevelLoader.StartLevel -= OnLevelEnter;
            Everest.Events.Level.OnExit -= OnLevelExit;
            Everest.Events.Level.OnPause -= OnLevelPause;
            Everest.Events.Level.OnUnpause -= OnLevelUnpause;
            On.Celeste.Level.StartCutscene -= OnStartCutscene;
            On.Celeste.Level.EndCutscene -= OnEndCutscene;
            On.Celeste.Level.CancelCutscene -= OnCancelCutscene;
            On.Celeste.Mod.Entities.CustomNPC.OnTalkEnd -= OnCustomNPCTalkEnd;
            On.Celeste.Level.TransitionRoutine -= OnTransition;
            On.Celeste.HeartGem.CollectRoutine -= OnHeartGemCollect;
            On.Celeste.Cassette.CollectRoutine -= OnCassetteCollect;
            On.Celeste.ForsakenCitySatellite.UnlockGem -= On1AHeartUnlock;
            On.Celeste.UnlockedPico8Message.Routine -= OnPico8Unlock;
            On.Celeste.Player.CassetteFlyBegin -= OnBubbleBegin;
            On.Celeste.Player.CassetteFlyEnd -= OnBubbleEnd;
            On.Celeste.Player.IntroRespawnEnd -= OnPlayerRespawn;
            Everest.Events.Player.OnDie -= OnPlayerDie;
            On.Celeste.Lookout.LookRoutine -= OnLookout;
            On.Celeste.CS00_Ending.EndingCutsceneDelay.ctor -= OnPrologueEnding;
            On.Celeste.Editor.MapEditor.ctor -= OnEditMap;
            if (CollabUtils2OnHookAdded) {
                CollabUtils2OnHookAdded = false;
                On.Celeste.OuiChapterPanel.Enter -= OnOuiChapterPanelEnter;
                On.Celeste.OuiChapterPanel.Leave -= OnOuiChapterPanelLeave;
                On.Celeste.OuiJournal.Enter -= OnOuiJournalEnter;
                On.Celeste.OuiJournal.Leave -= OnOuiJournalLeave;
                On.Monocle.Entity.Added -= OnEntityAdded;
                On.Monocle.Entity.Removed -= OnEntityRemoved;
            }
            MiniHeartHook?.Dispose();
            MiniHeartHook = null;
            FixCelesteTASHook?.Dispose();
            FixCelesteTASHook = null;
        }

        private void AddMiniHeartHook() {
            MiniHeartHook = new ILHook(typeof(Mod.CollabUtils2.Entities.MiniHeart).GetMethod("SmashRoutine", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget(), MiniHeartManipulator);
        }

        private void MiniHeartManipulator(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(inst => inst.MatchLdcI4(-1));
            cursor.Emit(OpCodes.Call, typeof(UncapFPSModule).GetProperty("Instance").GetGetMethod());
            cursor.Emit(OpCodes.Call, typeof(UncapFPSModule).GetMethod("OnMiniHeartCollectBegin", BindingFlags.NonPublic | BindingFlags.Instance));
        }

        private void FixCelesteTAS() {
            FixCelesteTASHook = new ILHook(typeof(TAS.GameInfo).GetMethod("ConvertMicroSecondToFrames", BindingFlags.NonPublic | BindingFlags.Static), FixCelesteTASManipulator);
        }

        private void FixCelesteTASManipulator(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before, inst => inst.OpCode == OpCodes.Div);
            cursor.Emit(OpCodes.Ldc_I8, 1L);
            cursor.Emit(OpCodes.Call, typeof(Math).GetMethod("Max", new[] { typeof(long), typeof(long) }));
        }

        private void SetFPSLimit(int limit) {
            if (limit <= 0) {
                Celeste.Instance.IsFixedTimeStep = false;
            } else {
                Celeste.Instance.IsFixedTimeStep = true;
                Celeste.Instance.TargetElapsedTime = new TimeSpan((long)Math.Round(1.0 / limit * TimeSpan.TicksPerSecond));
            }
        }

        private void SetFPSLimit() => SetFPSLimit(Settings.FpsLimit);

        private void SetVanillaFPSLimit() => SetFPSLimit(60);

        private void SetLevelFPSLimit() {
            if (Settings.ApplyTo == EnumApplyTo.All) {
                SetFPSLimit();
            } else {
                SetVanillaFPSLimit();
            }
        }

        private void SetCutsceneFPSLimit() {
            if (Settings.ApplyTo != EnumApplyTo.UIOnly) {
                SetFPSLimit();
            } else {
                SetVanillaFPSLimit();
            }
        }

        private void SetUIFPSLimit() => SetFPSLimit();

        private void SetPico8FPSLimit() => SetVanillaFPSLimit();

        public override void SaveSettings() {
            base.SaveSettings();
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: SaveSettings");
            SetFPSLimit();
        }

        private void OnPico8End(On.Celeste.Pico8.Emulator.orig_End orig, Pico8.Emulator self) {
            if (self.ReturnTo is Level) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: PICO-8 End");
                SetLevelFPSLimit();
            }
            orig(self);
        }

        private void OnPico8Pause(On.Celeste.Pico8.Emulator.orig_CreatePauseMenu orig, Pico8.Emulator self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: PICO-8 Pause");
            SetFPSLimit();
            orig(self);
            var data = DynamicData.For(self);
            var menu = data.Get<TextMenu>("pauseMenu");
            Action orig_OnCancel = menu.OnCancel;
            menu.OnCancel = () => {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetPico8FPSLimit: PICO-8 Unpause (Cancel)");
                SetPico8FPSLimit();
                orig_OnCancel();
            };
            Action orig_OnESC = menu.OnESC;
            menu.OnESC = () => {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetPico8FPSLimit: PICO-8 Unpause (ESC)");
                SetPico8FPSLimit();
                orig_OnESC();
            };
            Action orig_OnPause = menu.OnPause;
            menu.OnPause = () => {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetPico8FPSLimit: PICO-8 Unpause (Pause)");
                SetPico8FPSLimit();
                orig_OnPause();
            };
        }

        private void OnPico8Reset(On.Celeste.Pico8.Emulator.orig_ResetScreen orig, Pico8.Emulator self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetPico8FPSLimit: PICO-8 Start/Reset");
            SetPico8FPSLimit();
            orig(self);
        }

        private void OnLevelEnter(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: Enter Level");
            SetLevelFPSLimit();
            orig(self);
        }

        private void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) { 
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: Exit Level");
            SetUIFPSLimit();
            CutsceneFPSWhenUnpause = false;
            UIFPSWhenUnpause = false;
        }

        private void OnLevelPause(Level self, int startIndex, bool minimal, bool quickReset) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: Pause Level");
            SetUIFPSLimit();
        }

        private void OnLevelUnpause(Level self) {
            Player player = self.Tracker.GetEntity<Player>();
            if (UIFPSWhenUnpause) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Unpause Level (In UI)");
                SetUIFPSLimit();
            } else if (
                self.InCutscene ||
                CutsceneFPSWhenUnpause ||
                player.StateMachine.State == Player.StCassetteFly
            ) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Unpause Level (In Cutscene)");
                SetCutsceneFPSLimit();
            } else {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: Unpause Level");
                SetLevelFPSLimit();
            }
        }

        private void OnStartCutscene(On.Celeste.Level.orig_StartCutscene orig, Level self, Action<Level> onSkip, bool fadeInOnSkip, bool endingChapterAfterCutscene, bool resetZoomOnSkip) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Start Cutscene");
            SetCutsceneFPSLimit();
            orig(self, onSkip, fadeInOnSkip, endingChapterAfterCutscene, resetZoomOnSkip);
        }

        private void OnEndCutscene(On.Celeste.Level.orig_EndCutscene orig, Level self) {
            if (!DynamicData.For(self).Get<bool>("endingChapterAfterCutscene") && !self.Completed) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: End Cutscene");
                SetLevelFPSLimit();
            }
            orig(self);
        }

        private void OnCancelCutscene(On.Celeste.Level.orig_CancelCutscene orig, Level self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: Cancel Cutscene");
            SetLevelFPSLimit();
            orig(self);
        }

        private void OnCustomNPCTalkEnd(On.Celeste.Mod.Entities.CustomNPC.orig_OnTalkEnd orig, Mod.Entities.CustomNPC self, Level level) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: End Cutscene (CustomNPC Workaround)");
            SetLevelFPSLimit();
            orig(self, level);
        }

        private IEnumerator OnTransition(On.Celeste.Level.orig_TransitionRoutine orig, Level self, LevelData next, Vector2 direction) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Start Transition");
            SetCutsceneFPSLimit();
            IEnumerator e = orig(self, next, direction);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            if (!self.InCutscene) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: End Transition");
                SetLevelFPSLimit();
            }
        }

        private IEnumerator OnHeartGemCollect(On.Celeste.HeartGem.orig_CollectRoutine orig, HeartGem self, Player player) {
            CutsceneFPSWhenUnpause = true;
            if (!self.IsFake) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Start Collect Heart Gem");
                SetCutsceneFPSLimit();
            }
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            CutsceneFPSWhenUnpause = false;
            if (!self.IsFake && (!(self.Scene as Level)?.Completed ?? true)) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: End Collect Heart Gem");
                SetLevelFPSLimit();
            }
        }

        private IEnumerator OnCassetteCollect(On.Celeste.Cassette.orig_CollectRoutine orig, Cassette self, Player player) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Start Collect Cassette");
            SetCutsceneFPSLimit();
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: End Collect Cassette");
            SetLevelFPSLimit();
        }

        private IEnumerator On1AHeartUnlock(On.Celeste.ForsakenCitySatellite.orig_UnlockGem orig, ForsakenCitySatellite self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: 1A Heart Unlock Start");
            SetCutsceneFPSLimit();
            CutsceneFPSWhenUnpause = true;
            IEnumerator e = orig(self);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: 1A Heart Unlock End");
            SetLevelFPSLimit();
            CutsceneFPSWhenUnpause = false;
        }

        private IEnumerator OnPico8Unlock(On.Celeste.UnlockedPico8Message.orig_Routine orig, UnlockedPico8Message self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: Pico-8 Unlock Start");
            SetUIFPSLimit();
            IEnumerator e = orig(self);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: Pico-8 Unlock End");
            SetLevelFPSLimit();
        }

        private void OnBubbleBegin(On.Celeste.Player.orig_CassetteFlyBegin orig, Player self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Return Bubble Begin");
            SetCutsceneFPSLimit();
            orig(self);
        }

        private void OnBubbleEnd(On.Celeste.Player.orig_CassetteFlyEnd orig, Player self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: Return Bubble End");
            SetLevelFPSLimit();
            orig(self);
        }

        private void OnPlayerRespawn(On.Celeste.Player.orig_IntroRespawnEnd orig, Player self) {
            if (!(self.Scene as Level)?.InCutscene ?? true) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: Player Respawn");
                SetLevelFPSLimit();
            }
            orig(self);
        }

        private void OnPlayerDie(Player self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: Player Die");
            SetCutsceneFPSLimit();
            CutsceneFPSWhenUnpause = false;
            UIFPSWhenUnpause = false;
        }

        private IEnumerator OnLookout(On.Celeste.Lookout.orig_LookRoutine orig, Lookout self, Player player) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: Start Lookout");
            SetUIFPSLimit();
            UIFPSWhenUnpause = true;
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: End Lookout");
            SetLevelFPSLimit();
            UIFPSWhenUnpause = false;
        }

        private void OnPrologueEnding(On.Celeste.CS00_Ending.EndingCutsceneDelay.orig_ctor orig, Monocle.Entity self) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: Exit Level (Prologue Ending)");
            SetUIFPSLimit();
            orig(self);
        }

        private void OnEditMap(On.Celeste.Editor.MapEditor.orig_ctor orig, Editor.MapEditor self, AreaKey area, bool reloadMapData) {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: Open Map Editor");
            SetUIFPSLimit();
            orig(self, area, reloadMapData);
        }

        private IEnumerator OnOuiChapterPanelEnter(On.Celeste.OuiChapterPanel.orig_Enter orig, OuiChapterPanel self, Oui prev) {
            if (Celeste.Scene is Level) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: CollabUtils2 Lobby OuiChapterPanel Enter");
                SetUIFPSLimit();
            }
            IEnumerator e = orig(self, prev);
            while (e.MoveNext()) {
                yield return e.Current;
            }
        }

        private IEnumerator OnOuiChapterPanelLeave(On.Celeste.OuiChapterPanel.orig_Leave orig, OuiChapterPanel self, Oui next) {
            IEnumerator e = orig(self, next);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            if (Celeste.Scene is Level) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: CollabUtils2 Lobby OuiChapterPanel Leave");
                SetLevelFPSLimit();
            }
        }

        private IEnumerator OnOuiJournalEnter(On.Celeste.OuiJournal.orig_Enter orig, OuiJournal self, Oui prev) {
            if (Celeste.Scene is Level) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: CollabUtils2 Lobby OuiJournal Enter");
                SetUIFPSLimit();
            }
            IEnumerator e = orig(self, prev);
            while (e.MoveNext()) {
                yield return e.Current;
            }
        }

        private IEnumerator OnOuiJournalLeave(On.Celeste.OuiJournal.orig_Leave orig, OuiJournal self, Oui next) {
            IEnumerator e = orig(self, next);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            if (Celeste.Scene is Level) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: CollabUtils2 Lobby OuiJournal Leave");
                SetLevelFPSLimit();
            }
        }

        private void OnEntityAdded(On.Monocle.Entity.orig_Added orig, Monocle.Entity self, Monocle.Scene scene) {
            if (self is CollabUtils2.UI.LobbyMapUI) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetUIFPSLimit: CollabUtils2 LobbyMapUI Enter");
                SetUIFPSLimit();
            }
            orig(self, scene);
        }

        private void OnEntityRemoved(On.Monocle.Entity.orig_Removed orig, Monocle.Entity self, Monocle.Scene scene) {
            if (self is CollabUtils2.UI.LobbyMapUI) {
                Logger.Log(LogLevel.Verbose, "UncapFPS", "SetLevelFPSLimit: CollabUtils2 LobbyMapUI Leave");
                SetLevelFPSLimit();
            }
            orig(self, scene);
        }

        private void OnMiniHeartCollectBegin() {
            Logger.Log(LogLevel.Verbose, "UncapFPS", "SetCutsceneFPSLimit: CollabUtils2 MiniHeart Collect Begin");
            SetCutsceneFPSLimit();
            CutsceneFPSWhenUnpause = true;
        }
    }
}
