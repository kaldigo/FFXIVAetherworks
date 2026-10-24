using Aetherworks.Actions;
using Aetherworks.Tweaks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using UseActionMode = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.UseActionMode;

namespace Aetherworks.Features;

public sealed unsafe class ReturnViaTeleport(IClientState client, IPlayerState player, IObjectTable objects,
    ICondition condition, IGameInteropProvider interop, IChatGui chat, IPluginLog log,
    Func<bool> skipConfirmation) : ITweak
{
    public const string FeatureId = "return-via-teleport";
    private const uint ReturnAction = 6;
    private const uint TeleportAction = 5;
    private Hook<UseAction>? hook;
    private ReturnFlow? flow;
    private bool enabled;
    private bool handling;
    private bool errorLogged;
    private ReturnFlow Flow => flow ??= new(
        () => !ActionManager.Instance()->IsRecastTimerActive(ActionType.Action, ReturnAction),
        () => TryQuote(out var quote) ? quote : null, skipConfirmation, Teleport, NativeReturn,
        () => Environment.TickCount64);
    public event Action? ConfirmationRequested
    {
        add => Flow.ConfirmationRequested += value;
        remove => Flow.ConfirmationRequested -= value;
    }
    public string Id => FeatureId;
    public string Name => "Return via Teleport";
    public string Category => "Actions";
    public string Description => "Use Return normally when it is ready. While Return is on cooldown, use Teleport to your registered home point at the normal fare.";

    private delegate byte UseAction(ActionManager* manager, ActionType type, uint id, ulong target,
        uint parameter, UseActionMode mode, uint comboRoute, bool* groundTargeted);

    public void Enable()
    {
        if (enabled) return;
        if (ActionManager.MemberFunctionPointers.UseAction == null)
            throw new InvalidOperationException("The action hook is unavailable for this game version.");
        hook = interop.HookFromAddress<UseAction>((nint)ActionManager.MemberFunctionPointers.UseAction, OnAction);
        hook.Enable();
        client.Logout += OnLogout;
        client.TerritoryChanged += OnTerritoryChanged;
        enabled = true;
        Flow.Enabled = true;
        errorLogged = false;
    }

    public void Disable()
    {
        enabled = false;
        Flow.Enabled = false;
        Cancel();
        client.Logout -= OnLogout;
        client.TerritoryChanged -= OnTerritoryChanged;
        hook?.Dispose();
        hook = null;
    }

    private byte OnAction(ActionManager* manager, ActionType type, uint id, ulong target,
        uint parameter, UseActionMode mode, uint comboRoute, bool* groundTargeted)
    {
        if (enabled && !handling)
        {
            try
            {
                var isReturn = type == ActionType.Action && id == ReturnAction ||
                    type == ActionType.GeneralAction && ActionManager.GetSpellIdForAction(type, id) == ReturnAction;
                if (isReturn)
                {
                    if (Flow.OnReturn() is { } result)
                    {
                        if (groundTargeted != null) *groundTargeted = false;
                        return result ? (byte)1 : (byte)0;
                    }
                }
            }
            catch (Exception ex) { Report(ex); Cancel(); }
        }
        // Preserve native behavior for ready Return, unrelated actions and unavailable Teleport.
        return hook!.Original(manager, type, id, target, parameter, mode, comboRoute, groundTargeted);
    }

    private bool TryQuote(out ReturnRequest quote)
    {
        quote = null!;
        if (!enabled || !client.IsLoggedIn || !player.IsLoaded || objects.LocalPlayer is not { IsDead: false } ||
            condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ||
            condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]) return false;
        var manager = ActionManager.Instance();
        if (manager == null || manager->AnimationLock > 0 ||
            manager->GetActionStatus(ActionType.Action, TeleportAction) != 0) return false;
        var home = player.HomeAetheryte;
        if (home.RowId == 0 || player.ContentId == 0) return false;
        var telepo = Telepo.Instance();
        if (telepo == null || telepo->ActiveTeleportRequest) return false;
        telepo->UpdateAetheryteList();
        foreach (var entry in telepo->TeleportList)
        {
            if (entry.AetheryteId != home.RowId || entry.SubIndex != 0) continue;
            quote = new(player.ContentId, client.TerritoryType, home.RowId, entry.GilCost,
                home.Value.PlaceName.Value.Name.ToString(), Environment.TickCount64 + 30_000);
            return true;
        }
        return false;
    }

    public ReturnRequest? GetPending()
    {
        try
        {
            return Flow.GetPending();
        }
        catch (Exception ex) { Report(ex); }
        Cancel();
        return null;
    }

    public void Cancel() => Flow.Cancel();

    private void OnLogout(int type, int code) => Cancel();
    private void OnTerritoryChanged(uint territory) => Cancel();

    public void Confirm()
    {
        try { Flow.Confirm(); }
        catch (Exception ex) { Cancel(); Report(ex); }
    }

    private bool Teleport(ReturnRequest request)
    {
        handling = true;
        try
        {
            var result = Telepo.Instance()->Teleport(request.Home, 0);
            if (!result) chat.PrintError("Aetherworks: Teleport could not be started. Try Return again when travel is available.");
            return result;
        }
        finally { handling = false; }
    }

    private bool NativeReturn()
    {
        handling = true;
        try { return ActionManager.Instance()->UseAction(ActionType.Action, ReturnAction); }
        finally { handling = false; }
    }

    private void Report(Exception ex)
    {
        if (errorLogged) return;
        errorLogged = true;
        log.Error(ex, "Return via Teleport failed");
        chat.PrintError("Aetherworks: Return via Teleport failed. See the Dalamud log for details.");
    }
}
