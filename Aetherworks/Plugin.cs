using Aetherworks.Features;
using Aetherworks.Hotbars;
using Aetherworks.Tweaks;
using Aetherworks.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Aetherworks;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly WindowSystem windows = new("Aetherworks");
    private readonly SettingsWindow settings;
    private readonly TweakManager tweaks;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log,
        IClientState client, IPlayerState player, IObjectTable objects, ICondition condition,
        IGameInteropProvider interop, IChatGui chat, IFramework framework, IContextMenu contextMenu,
        IGameGui gui, IDataManager data)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.EnabledTweaks ??= new(StringComparer.Ordinal);
        // Old choices never opt a character into the new feature.
        config.EnabledTweaks.IntersectWith([ReturnViaTeleport.FeatureId, SharedHotbarSync.FeatureId,
            SaddlebagWithdrawLeaveOne.FeatureId]);
        var fallback = new ReturnViaTeleport(client, player, objects, condition, interop, chat, log,
            () => config.SkipPaidTeleportConfirmation);
        var hotbarSync = new SharedHotbarSync(framework,
            new NativeSharedHotbars(client, player, objects, condition),
            new SharedHotbarStore(Path.Combine(pluginInterface.GetPluginConfigDirectory(), "shared-hotbars")), log);
        var withdrawal = new SaddlebagWithdrawLeaveOne(contextMenu, framework, gui, player, data, log);
        tweaks = new TweakManager([fallback, hotbarSync, withdrawal], config.EnabledTweaks,
            () => pluginInterface.SavePluginConfig(config), (ex, message) => log.Error(ex, message));
        settings = new SettingsWindow(tweaks, config, fallback, hotbarSync);
        var confirmation = new ReturnConfirmationWindow(fallback);
        fallback.ConfirmationRequested += confirmation.Open;
        windows.AddWindow(settings);
        windows.AddWindow(confirmation);
        commands.AddHandler("/aetherworks", new CommandInfo(OnCommand) { HelpMessage = "Open Aetherworks settings." });
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        tweaks.RestoreEnabled();
    }

    private void OnCommand(string command, string args) => OpenSettings();
    private void OpenSettings() => settings.IsOpen = true;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        commands.RemoveHandler("/aetherworks");
        windows.RemoveAllWindows();
        tweaks.Dispose();
    }
}
