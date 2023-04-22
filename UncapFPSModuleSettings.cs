namespace Celeste.Mod.UncapFPS {
    public enum EnumApplyTo {
        All,
        UIAndCutscene,
        UIOnly,
    }

    public class UncapFPSModuleSettings : EverestModuleSettings {
        [SettingName("FPS Limit")]
        [SettingSubText("60 for vanilla, 0 for unlimited. Disable VSync for FPS higher than native.\nFPS other than 60 may affect physics slightly, making some techniques hard or impossible.\nFPS too high may crash some mods.\nDo NOT edit this ingame or game will crash. This is a Everest bug.")]
        [SettingNumberInput(false, 4)]
        public int FpsLimit { get; set; } = 0;

        [SettingName("Apply To")]
        [SettingSubText("UIAndCutscene may randomly crash some mods.\nPICO-8 will be always limited to 60 FPS due to problems.")]
        public EnumApplyTo ApplyTo { get; set; } = EnumApplyTo.All;
    }
}
