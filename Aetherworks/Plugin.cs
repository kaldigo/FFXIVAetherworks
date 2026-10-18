using Aetherworks.Tweaks;
using Aetherworks.Windows;
using Aetherworks.Features;
using Aetherworks.Ownership;
using Aetherworks.Glamour;
using Aetherworks.Actions;
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
    private readonly OwnershipTracker ownership;
    private readonly OwnershipTooltipPanel tooltip;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IPluginLog log,
        IFramework framework, IPlayerState player, IGameGui gui, IDataManager data,
        IAddonLifecycle lifecycle, IGameInteropProvider interop, IChatGui chat,
        ICondition condition, IObjectTable objects, ITargetManager targets, ISigScanner scanner)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.EnabledTweaks ??= new(StringComparer.Ordinal);

        // Register every feature here. Constructors must not activate behavior.
        var appearances = GameAppearanceCatalog.Create(data);
        ownership = new OwnershipTracker(pluginInterface, framework, player, gui, log, data);
        tooltip = new OwnershipTooltipPanel(gui, ownership, appearances);
        var actionConveniences = new ActionConveniences(framework, condition, objects, targets, player, interop, scanner, data, log);
        var fcChest = new FcChestEntrustDuplicates(pluginInterface, framework, gui, player, data, log);
        ITweak[] registeredTweaks = [new ItemLocationTooltips(tooltip), new GlamourOwnershipTooltips(tooltip),
            new ActionConvenienceTweak(actionConveniences, ActionConvenience.Dismount),
            new ActionConvenienceTweak(actionConveniences, ActionConvenience.CastCancel),
            new ActionConvenienceTweak(actionConveniences, ActionConvenience.Target),
            new ActionConvenienceTweak(actionConveniences, ActionConvenience.ChangeTarget),
            new RecipeMaterialLocations(pluginInterface, gui, data, ownership, log),
            new SaddlebagEntrustDuplicates(pluginInterface, framework, gui, player, data, log),
            fcChest, new FcChestAllTabs(fcChest),
            new IncludeFcChestOwnership(ownership),
            new PreventDuplicateGlamourStorage(lifecycle, interop, gui, chat, log, ownership, appearances)];
        tweaks = new TweakManager(registeredTweaks, config.EnabledTweaks,
            () => pluginInterface.SavePluginConfig(config), (ex, message) => log.Error(ex, message));
        settings = new SettingsWindow(tweaks, ownership);
        settings.ConfigureSize();
        windows.AddWindow(settings);
        commands.AddHandler("/aetherworks", new CommandInfo(OnCommand) { HelpMessage = "Open Aetherworks tweak settings." });
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.Draw += tooltip.Draw;
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
        pluginInterface.UiBuilder.Draw -= tooltip.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        commands.RemoveHandler("/aetherworks");
        windows.RemoveAllWindows();
        tweaks.Dispose();
        ownership.Dispose();
    }
}
