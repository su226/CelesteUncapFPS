using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.UncapFPS {
    internal enum State {
        UI,
        Cutscene
    }

    public class UncapFPSModule : EverestModule {
        public static UncapFPSModule Instance { get; private set; }

        public override Type SettingsType => typeof(UncapFPSModuleSettings);
        public static UncapFPSModuleSettings Settings => (UncapFPSModuleSettings) Instance._Settings;
        private ILHook FixCelesteTASHook;
        private ILHook MiniHeartHook;
        private ILHook FixIngameOuiCloseHook;
        private bool CollabUtils2Loaded;
        private bool MaxHelpingHandLoaded;
        private const string LogTag = "UncapFPS";
        private const string StateKey = "UncapFPS_State";

        public UncapFPSModule() {
            Instance = this;
#if DEBUG
            Logger.SetLogLevel(LogTag, LogLevel.Verbose);
#else
            Logger.SetLogLevel(LogTag, LogLevel.Info);
#endif
        }

        public override void Load() {
            SetUIFPSLimit();
            On.Celeste.Pico8.Emulator.End += OnPico8End;
            On.Celeste.Pico8.Emulator.CreatePauseMenu += OnPico8Pause;
            On.Celeste.Pico8.Emulator.ResetScreen += OnPico8Reset;
            On.Celeste.LevelEnter.Go += OnLevelEnterGo;
            On.Celeste.LevelLoader.StartLevel += OnLevelLoaderEnterLevel;
            Everest.Events.Level.OnExit += OnLevelExit;
            Everest.Events.Level.OnPause += OnLevelPause;
            Everest.Events.Level.OnUnpause += OnLevelUnpause;
            On.Celeste.Level.StartCutscene += OnStartCutscene;
            On.Celeste.Level.EndCutscene += OnEndCutscene;
            On.Celeste.Level.CancelCutscene += OnCancelCutscene;
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
            On.Monocle.Entity.Added += OnEntityAdded;
            On.Monocle.Entity.Removed += OnEntityRemoved;
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
                Name = "CollabUtils2",
                Version = new Version(1, 8, 12),
            })) {
                CollabUtils2Loaded = true;
                Logger.Log(LogLevel.Info, LogTag, "CollabUtils2 detected, adding support for MiniHeart.SmashRoutine");
                try {
                    AddMiniHeartHook();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, LogTag, "Failed to patch CollabUtils2 MiniHeart.SmashRoutine");
                    Logger.LogDetailed(e, LogTag);
                }
                Logger.Log(LogLevel.Info, LogTag, "CollabUtils2 detected, fixing incorrect limit for InGameOverworldHelper.Close");
                try {
                    FixIngameOuiClose();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, LogTag, "Failed to patch CollabUtils2 InGameOverworldHelper.Close");
                    Logger.LogDetailed(e, LogTag);
                }
            }
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
                Name = "CelesteTAS",
                Version = new Version(3, 25, 11),
            })) {
                Logger.Log(LogLevel.Info, LogTag, "CelesteTAS detected, fixing DivideByZeroException from TAS.GameInfo.ConvertMicroSecondToFrames");
                try {
                    FixCelesteTAS();
                } catch (Exception e) {
                    Logger.Log(LogLevel.Error, LogTag, "Failed to patch CelesteTAS.");
                    Logger.LogDetailed(e, LogTag);
                }
            }
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
                Name = "SpeedrunTool",
                Version = new Version(3, 22, 5),
            })) {
                Logger.Log(LogLevel.Info, LogTag, "SpeedrunTool detected, adding load state action.");
                AddSpeedrunToolActions();
            }
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata {
                Name = "MaxHelpingHand",
                Version = new Version(1, 29, 0),
            })) {
                Logger.Log(LogLevel.Info, LogTag, "MaxHelpingHand detected.");
                MaxHelpingHandLoaded = true;
            }
        }

        public override void Unload() {
            SetVanillaFPSLimit();
            On.Celeste.Pico8.Emulator.End -= OnPico8End;
            On.Celeste.Pico8.Emulator.CreatePauseMenu -= OnPico8Pause;
            On.Celeste.Pico8.Emulator.ResetScreen -= OnPico8Reset;
            On.Celeste.LevelEnter.Go -= OnLevelEnterGo;
            On.Celeste.LevelLoader.StartLevel -= OnLevelLoaderEnterLevel;
            Everest.Events.Level.OnExit -= OnLevelExit;
            Everest.Events.Level.OnPause -= OnLevelPause;
            Everest.Events.Level.OnUnpause -= OnLevelUnpause;
            On.Celeste.Level.StartCutscene -= OnStartCutscene;
            On.Celeste.Level.EndCutscene -= OnEndCutscene;
            On.Celeste.Level.CancelCutscene -= OnCancelCutscene;
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
            On.Monocle.Entity.Added -= OnEntityAdded;
            On.Monocle.Entity.Removed -= OnEntityRemoved;
            CollabUtils2Loaded = false;
            MiniHeartHook?.Dispose();
            MiniHeartHook = null;
            FixCelesteTASHook?.Dispose();
            FixCelesteTASHook = null;
            FixIngameOuiCloseHook?.Dispose();
            FixIngameOuiCloseHook = null;
            MaxHelpingHandLoaded = false;
        }

        private void AddMiniHeartHook() {
            MethodInfo stateMachine = typeof(CollabUtils2.Entities.MiniHeart).GetMethod("SmashRoutine", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget();
            MiniHeartHook = new ILHook(stateMachine, il => {
                ILCursor cursor = new ILCursor(il);
                cursor.GotoNext(inst => inst.MatchLdcI4(-1));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, stateMachine.DeclaringType.GetField("<>4__this"));
                cursor.EmitDelegate<Action<CollabUtils2.Entities.MiniHeart>>(self => {
                    Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: CollabUtils2 MiniHeart Collect Begin");
                    SetCutsceneFPSLimit();
                    DynamicData.For(self.Scene).Set(StateKey, State.Cutscene);
                });
            });
        }

        private void FixCelesteTAS() => FixCelesteTASHook = new ILHook(typeof(TAS.GameInfo).GetMethod("ConvertMicroSecondToFrames", BindingFlags.NonPublic | BindingFlags.Static), il => {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(inst => inst.OpCode == OpCodes.Div);
            cursor.Emit(OpCodes.Ldc_I8, 1L);
            cursor.Emit(OpCodes.Call, typeof(Math).GetMethod("Max", new[] {typeof(long), typeof(long)}));
        });

        private void FixIngameOuiClose() => FixIngameOuiCloseHook = new ILHook(typeof(CollabUtils2.UI.InGameOverworldHelper).GetMethod("Close"), il => {
            var cursor = new ILCursor(il);
            var addMethod = typeof(Monocle.Scene).GetEvent("OnEndOfFrame").GetAddMethod();
            cursor.GotoNext(MoveType.After, inst => inst.MatchCallvirt(addMethod));
            cursor.EmitDelegate(() => {
                Logger.Log(LogLevel.Verbose, LogTag, "CollabUtils2 ingame oui closing, setting player dash cooldown.");
                var data = DynamicData.For(Celeste.Scene.Tracker.GetEntity<Player>());
                data.Set("dashCooldownTimer", 0.2f);
            });
        });

        private void AddSpeedrunToolActions() {
            SpeedrunTool.SaveLoad.SaveLoadAction.SafeAdd(loadState: (_, level) => {
                OnLevelUnpause(level);
            });
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
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: SaveSettings");
            SetFPSLimit();
        }

        private void OnPico8End(On.Celeste.Pico8.Emulator.orig_End orig, Pico8.Emulator self) {
            if (self.ReturnTo is Level) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: PICO-8 End");
                SetLevelFPSLimit();
            }
            orig(self);
        }

        private void OnPico8Pause(On.Celeste.Pico8.Emulator.orig_CreatePauseMenu orig, Pico8.Emulator self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: PICO-8 Pause");
            SetFPSLimit();
            orig(self);
            var data = DynamicData.For(self);
            var menu = data.Get<TextMenu>("pauseMenu");
            Action orig_OnCancel = menu.OnCancel;
            menu.OnCancel = () => {
                Logger.Log(LogLevel.Verbose, LogTag, "SetPico8FPSLimit: PICO-8 Unpause (Cancel)");
                SetPico8FPSLimit();
                orig_OnCancel();
            };
            Action orig_OnESC = menu.OnESC;
            menu.OnESC = () => {
                Logger.Log(LogLevel.Verbose, LogTag, "SetPico8FPSLimit: PICO-8 Unpause (ESC)");
                SetPico8FPSLimit();
                orig_OnESC();
            };
            Action orig_OnPause = menu.OnPause;
            menu.OnPause = () => {
                Logger.Log(LogLevel.Verbose, LogTag, "SetPico8FPSLimit: PICO-8 Unpause (Pause)");
                SetPico8FPSLimit();
                orig_OnPause();
            };
        }

        private void OnPico8Reset(On.Celeste.Pico8.Emulator.orig_ResetScreen orig, Pico8.Emulator self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetPico8FPSLimit: PICO-8 Start/Reset");
            SetPico8FPSLimit();
            orig(self);
        }

        private void OnLevelEnterGo(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData) {
            // If an error occurs, LevelEnter.Go will be called AGAIN, displaying a postcard and setting Engine.Scene to Overworld.
            // Usually FPS limit will be correct, except teleporting in collab lobbies with CollabLobbyUI mod.
            if (LevelEnter.ErrorMessage != null) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Leve load failed");
                SetUIFPSLimit();
            }
            orig(session, fromSaveData);
        }

        private void OnLevelLoaderEnterLevel(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: Enter Level");
            SetLevelFPSLimit();
            orig(self);
        }

        private void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) { 
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Exit Level");
            SetUIFPSLimit();
            DynamicData.For(level).Set(StateKey, null);
        }

        private void OnLevelPause(Level self, int startIndex, bool minimal, bool quickReset) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Pause Level");
            SetUIFPSLimit();
        }

        private void OnLevelUnpause(Level self) {
            Player player = self.Tracker.GetEntity<Player>();
            State? state = DynamicData.For(self).Get<State?>(StateKey);
            if (state == State.UI) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Unpause Level (In UI)");
                SetUIFPSLimit();
            } else if (
                self.InCutscene ||
                state == State.Cutscene ||
                player.StateMachine.State == Player.StCassetteFly
            ) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Unpause Level (In Cutscene)");
                SetCutsceneFPSLimit();
            } else {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: Unpause Level");
                SetLevelFPSLimit();
            }
        }

        private void OnStartCutscene(On.Celeste.Level.orig_StartCutscene orig, Level self, Action<Level> onSkip, bool fadeInOnSkip, bool endingChapterAfterCutscene, bool resetZoomOnSkip) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Start Cutscene");
            SetCutsceneFPSLimit();
            orig(self, onSkip, fadeInOnSkip, endingChapterAfterCutscene, resetZoomOnSkip);
        }

        private void OnEndCutscene(On.Celeste.Level.orig_EndCutscene orig, Level self) {
            if (!DynamicData.For(self).Get<bool>("endingChapterAfterCutscene") && !self.Completed) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: End Cutscene");
                SetLevelFPSLimit();
            }
            orig(self);
        }

        private void OnCancelCutscene(On.Celeste.Level.orig_CancelCutscene orig, Level self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: Cancel Cutscene");
            SetLevelFPSLimit();
            orig(self);
        }

        private IEnumerator OnTransition(On.Celeste.Level.orig_TransitionRoutine orig, Level self, LevelData next, Vector2 direction) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Start Transition");
            SetCutsceneFPSLimit();
            IEnumerator e = orig(self, next, direction);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            if (!self.InCutscene) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: End Transition");
                SetLevelFPSLimit();
            }
        }

        private IEnumerator OnHeartGemCollect(On.Celeste.HeartGem.orig_CollectRoutine orig, HeartGem self, Player player) {
            DynamicData data = DynamicData.For(self.Scene);
            data.Set(StateKey, State.Cutscene);
            if (!self.IsFake) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Start Collect Heart Gem");
                SetCutsceneFPSLimit();
            }
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            data.Set(StateKey, null);
            if (!self.IsFake && (!(self.Scene as Level)?.Completed ?? true)) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: End Collect Heart Gem");
                SetLevelFPSLimit();
            }
        }

        private IEnumerator OnCassetteCollect(On.Celeste.Cassette.orig_CollectRoutine orig, Cassette self, Player player) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Start Collect Cassette");
            SetCutsceneFPSLimit();
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: End Collect Cassette");
            SetLevelFPSLimit();
        }

        private IEnumerator On1AHeartUnlock(On.Celeste.ForsakenCitySatellite.orig_UnlockGem orig, ForsakenCitySatellite self) {
            DynamicData data = DynamicData.For(self.Scene);
            Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: 1A Heart Unlock Start");
            SetCutsceneFPSLimit();
            data.Set(StateKey, State.Cutscene);
            IEnumerator e = orig(self);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: 1A Heart Unlock End");
            SetLevelFPSLimit();
            data.Set(StateKey, null);
        }

        private IEnumerator OnPico8Unlock(On.Celeste.UnlockedPico8Message.orig_Routine orig, UnlockedPico8Message self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Pico-8 Unlock Start");
            SetUIFPSLimit();
            IEnumerator e = orig(self);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: Pico-8 Unlock End");
            SetLevelFPSLimit();
        }

        private void OnBubbleBegin(On.Celeste.Player.orig_CassetteFlyBegin orig, Player self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Return Bubble Begin");
            SetCutsceneFPSLimit();
            orig(self);
        }

        private void OnBubbleEnd(On.Celeste.Player.orig_CassetteFlyEnd orig, Player self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: Return Bubble End");
            SetLevelFPSLimit();
            orig(self);
        }

        private void OnPlayerRespawn(On.Celeste.Player.orig_IntroRespawnEnd orig, Player self) {
            if (!(self.Scene as Level)?.InCutscene ?? true) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: Player Respawn");
                SetLevelFPSLimit();
            }
            orig(self);
        }

        private void OnPlayerDie(Player self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetCutsceneFPSLimit: Player Die");
            SetCutsceneFPSLimit();
            DynamicData.For(self.Scene).Set(StateKey, null);
        }

        private IEnumerator OnLookout(On.Celeste.Lookout.orig_LookRoutine orig, Lookout self, Player player) {
            DynamicData data = DynamicData.For(self.Scene);
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Start Lookout");
            SetUIFPSLimit();
            data.Set(StateKey, State.UI);
            IEnumerator e = orig(self, player);
            while (e.MoveNext()) {
                yield return e.Current;
            }
            Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: End Lookout");
            SetLevelFPSLimit();
            data.Set(StateKey, null);
        }

        private void OnPrologueEnding(On.Celeste.CS00_Ending.EndingCutsceneDelay.orig_ctor orig, Monocle.Entity self) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Exit Level (Prologue Ending)");
            SetUIFPSLimit();
            orig(self);
        }

        private void OnEditMap(On.Celeste.Editor.MapEditor.orig_ctor orig, Editor.MapEditor self, AreaKey area, bool reloadMapData) {
            Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: Open Map Editor");
            SetUIFPSLimit();
            orig(self, area, reloadMapData);
        }

        private static bool IsLobbyMapUI(object obj) => obj is CollabUtils2.UI.LobbyMapUI;

        private static bool IsMoreCustomNPC(object obj) => obj is MaxHelpingHand.Entities.MoreCustomNPC;

        private void OnEntityAdded(On.Monocle.Entity.orig_Added orig, Monocle.Entity self, Monocle.Scene scene) {
            if (CollabUtils2Loaded && IsLobbyMapUI(self)) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: CollabUtils2 LobbyMapUI Enter");
                SetUIFPSLimit();
            }
            if (scene is Level && self is Entities.SceneWrappingEntity wrap && wrap.WrappedScene is Overworld) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetUIFPSLimit: SceneWrappingEntity<Overworld> Added");
                SetUIFPSLimit();
            }
            if (MaxHelpingHandLoaded && IsMoreCustomNPC(self)) {
                (self as Entities.CustomNPC).OnEnd += () => {
                    Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: MaxHelpingHand MoreCustomNPC OnEnd");
                    SetLevelFPSLimit();
                };
            }
            orig(self, scene);
        }

        private void OnEntityRemoved(On.Monocle.Entity.orig_Removed orig, Monocle.Entity self, Monocle.Scene scene) {
            if (CollabUtils2Loaded && IsLobbyMapUI(self)) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: CollabUtils2 LobbyMapUI Leave");
                SetLevelFPSLimit();
            }
            if (scene is Level && !scene.Paused && self is Entities.SceneWrappingEntity wrap && wrap.WrappedScene is Overworld) {
                Logger.Log(LogLevel.Verbose, LogTag, "SetLevelFPSLimit: SceneWrappingEntity<Overworld> Removed");
                SetLevelFPSLimit();
            }
            orig(self, scene);
        }
    }
}
