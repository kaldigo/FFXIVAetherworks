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
    private readonly Ember.EmberHost ember;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log,
        IClientState client, IPlayerState player, IObjectTable objects, ICondition condition,
        IGameInteropProvider interop, IChatGui chat, IFramework framework, IContextMenu contextMenu,
        IGameGui gui, IDataManager data, IAddonLifecycle lifecycle, IAddonEventManager events)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.EnabledTweaks ??= new(StringComparer.Ordinal);
        bool migrateFeatures = config.Version < 2;
        if (migrateFeatures)
        {
            if (config.EnabledTweaks.Contains(PartyDamage.FeatureId))
                config.EnabledTweaks.Add(EmberOverlay.FeatureId);
            config.Version = 2;
        }
        config.EnabledTweaks.IntersectWith([ReturnViaTeleport.FeatureId, SharedHotbarSync.FeatureId,
            SaddlebagWithdrawLeaveOne.FeatureId, PartyDamage.FeatureId, EmberOverlay.FeatureId]);
        var fallback = new ReturnViaTeleport(client, player, objects, condition, interop, chat, log,
            () => config.SkipPaidTeleportConfirmation);
        var hotbarSync = new SharedHotbarSync(framework,
            new NativeSharedHotbars(client, player, objects, condition),
            new SharedHotbarStore(Path.Combine(pluginInterface.GetPluginConfigDirectory(), "shared-hotbars")), log);
        var withdrawal = new SaddlebagWithdrawLeaveOne(contextMenu, framework, gui, player, data, log, chat);
        config.Ember ??= new();
        ember = new Ember.EmberHost(pluginInterface, config.Ember, condition, client, () => pluginInterface.SavePluginConfig(config));
        var partyDamage = new PartyDamage(pluginInterface, lifecycle, framework, gui, objects, client, config, log);
        var emberOverlay = new EmberOverlay(lifecycle, events, gui, config, ember.Toggle, ember.SetEnabled);
        tweaks = new TweakManager([fallback, hotbarSync, withdrawal, partyDamage, emberOverlay], config.EnabledTweaks,
            () => pluginInterface.SavePluginConfig(config), (ex, message) => log.Error(ex, message));
        if (migrateFeatures) tweaks.Save();
        settings = new SettingsWindow(tweaks, config, fallback, hotbarSync, partyDamage, ember.ApplyZoom);
        var confirmation = new ReturnConfirmationWindow(fallback);
        fallback.ConfirmationRequested += confirmation.Open;
        pluginInterface.UiBuilder.Draw += ember.Draw;
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
        pluginInterface.UiBuilder.Draw -= ember.Draw;
        ember.Dispose();
    }
}
