using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using UseActionMode = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.UseActionMode;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using Sheets = Lumina.Excel.Sheets;

namespace Aetherworks.Actions;

/// <summary>Native adapters for the four ReAction-compatible behaviours; resources are owned by enabled tweaks.</summary>
public sealed unsafe class ActionConveniences
{
    private readonly IFramework framework;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly ITargetManager targets;
    private readonly IPlayerState player;
    private readonly IGameInteropProvider interop;
    private readonly ISigScanner scanner;
    private readonly IPluginLog log;
    private readonly Dictionary<uint, Sheets.Action> actions;
    private readonly HashSet<uint> mountActions;
    private readonly HashSet<uint> groundActions;
    private ActionConvenience enabled;
    private Hook<UseAction>? hook;
    private bool subscribed;
    private bool cancelled;
    private bool errorLogged;
    private QueuedAction? queued;
    private ResolveObject? resolve;
    private CancelCast? cancel;
    private const ulong NoTarget = 0xE0000000;

    private delegate byte UseAction(ActionManager* manager, ActionType type, uint id, ulong target,
        uint parameter, UseActionMode mode, uint comboRoute, bool* groundTargeted);
    private delegate NativeObject* ResolveObject(ulong id, byte flag);
    private delegate void CancelCast();
    private sealed record QueuedAction(ActionType Type, uint Id, ulong Target, UseActionMode Mode,
        uint ComboRoute, long Started, ulong Character);

    public ActionConveniences(IFramework framework, ICondition condition, IObjectTable objects,
        ITargetManager targets, IPlayerState player, IGameInteropProvider interop, ISigScanner scanner,
        IDataManager data, IPluginLog log)
    {
        this.framework = framework; this.condition = condition; this.objects = objects;
        this.targets = targets; this.player = player; this.interop = interop; this.scanner = scanner; this.log = log;
        var sheet = data.GetExcelSheet<Sheets.Action>();
        actions = sheet.Where(a => a.RowId > 8 && a.ClassJobCategory.RowId > 0 && a.ActionCategory.RowId <= 4).ToDictionary(a => a.RowId);
        mountActions = sheet.Where(a => a.ActionCategory.RowId == 12).Select(a => a.RowId).ToHashSet();
        groundActions = sheet.Where(a => a.TargetArea).Select(a => a.RowId).ToHashSet();
    }

    public void Set(ActionConvenience feature, bool value)
    {
        if (value) enabled |= feature; else enabled &= ~feature;
        if (!value && feature == ActionConvenience.Dismount) queued = null;
        if (!value && feature == ActionConvenience.CastCancel) cancelled = false;
        if ((enabled & (ActionConvenience.Target | ActionConvenience.CastCancel)) != 0 &&
            ActionManager.MemberFunctionPointers.CanUseActionOnTarget == null)
            throw new InvalidOperationException("Action target validation is unavailable.");
        if ((enabled & (ActionConvenience.Target | ActionConvenience.CastCancel)) != 0 && resolve is null)
        {
            // Dalamud already resolves leading CALL/JMP signatures to their destination.
            resolve = Marshal.GetDelegateForFunctionPointer<ResolveObject>(scanner.ScanText("E8 ?? ?? ?? ?? 48 8B D8 F3 0F 10 15"));
        }
        if (Has(ActionConvenience.CastCancel) && cancel is null)
            cancel = Marshal.GetDelegateForFunctionPointer<CancelCast>(scanner.ScanText("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9"));
        var needHook = (enabled & (ActionConvenience.Target | ActionConvenience.Dismount)) != 0;
        if (needHook && hook is null)
        {
            hook = interop.HookFromAddress<UseAction>((nint)ActionManager.MemberFunctionPointers.UseAction, OnAction);
            hook.Enable();
        }
        else if (!needHook && hook is not null) { hook.Dispose(); hook = null; }
        var needUpdate = (enabled & (ActionConvenience.Dismount | ActionConvenience.CastCancel)) != 0;
        if (needUpdate && !subscribed) { framework.Update += Update; subscribed = true; }
        else if (!needUpdate && subscribed) { framework.Update -= Update; subscribed = false; }
        errorLogged = false;
    }

    private bool Has(ActionConvenience feature) => (enabled & feature) != 0;
    private bool Usable(uint action, NativeObject* target) =>
        ActionManager.CanUseActionOnTarget(action, target) || groundActions.Contains(action);

    private byte OnAction(ActionManager* manager, ActionType type, uint id, ulong target,
        uint parameter, UseActionMode mode, uint comboRoute, bool* groundTargeted)
    {
        try
        {
            if (objects.LocalPlayer is null) return 0;
            var adjusted = type == ActionType.Action ? manager->GetAdjustedActionId(id) : id;
            if (Has(ActionConvenience.Dismount) && condition[ConditionFlag.Mounted] &&
                ActionRules.DismountEligible((uint)type, id, mountActions.Contains(adjusted)) &&
                manager->GetActionStatus(type, id, target, false, false) != 0)
            {
                var result = hook!.Original(manager, (ActionType)5, 23, 0, 0, 0, 0, null);
                if (result != 0) queued = new(type, id, target, mode, comboRoute, Environment.TickCount64, player.ContentId);
                return result;
            }
            if (Has(ActionConvenience.Target) && type == ActionType.Action)
            {
                var current = targets.Target is { } t ? (NativeObject*)t.Address : null;
                var explicitOther = target != NoTarget && resolve!(target, 0) != current;
                if (ActionRules.ShouldTarget(true, Has(ActionConvenience.ChangeTarget), (uint)type, current != null,
                    explicitOther, Usable(adjusted, current), actions.TryGetValue(adjusted, out var row) && row.CanTargetHostile))
                {
                    SelectEnemy();
                    if (targets.Target is { } chosen) target = chosen.GameObjectId;
                }
            }
            return hook!.Original(manager, type, id, target, parameter, mode, comboRoute, groundTargeted);
        }
        catch (Exception ex) { Report(ex); return 0; }
    }

    private void SelectEnemy()
    {
        if (objects.LocalPlayer is not { } local) return;
        var cameras = CameraManager.Instance();
        if (cameras == null || cameras->Camera == null) return;
        var camera = cameras->Camera;
        // Hypostasis exposes this byte as isFlipped; native Camera currently leaves it unnamed.
        var offset = (int)camera->ZoomMode == *((byte*)camera + 0x1F4);
        IGameObject? nearest = null;
        foreach (var candidate in objects)
        {
            if (candidate.ObjectKind is not (ObjectKind.Pc or ObjectKind.BattleNpc) || candidate.CurrentDistance >= 30 ||
                candidate is not IBattleChara { CurrentHp: > 0 } || !Usable(7, (NativeObject*)candidate.Address)) continue;
            var delta = candidate.Position - local.Position;
            if (ActionRules.InTargetCone(camera->DirH, offset, delta.X, delta.Z) &&
                (nearest is null || candidate.CurrentDistance < nearest.CurrentDistance)) nearest = candidate;
        }
        if (nearest is not null) targets.Target = nearest;
    }

    private void Update(IFramework _)
    {
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null || objects.LocalPlayer is null)
            { queued = null; cancelled = false; return; }
            if (queued is { } pending && Has(ActionConvenience.Dismount))
            {
                if (pending.Character != player.ContentId) queued = null;
                else if (!condition[ConditionFlag.Mounted])
                {
                    queued = null;
                    if (ActionRules.RetryDismountedAction(Environment.TickCount64 - pending.Started))
                        OnAction(manager, pending.Type, pending.Id, pending.Target, 0, pending.Mode, pending.ComboRoute, null);
                }
            }
            if (!Has(ActionConvenience.CastCancel)) return;
            if (cancelled && manager->CastActionType == 0) { cancelled = false; return; }
            if (cancelled || !ActionRules.CancelEligible((uint)manager->CastActionType,
                actions.ContainsKey(manager->CastActionId), groundActions.Contains(manager->CastActionId))) return;
            var castTarget = resolve!(manager->CastTargetId, 0);
            if (castTarget == null || Usable(manager->CastActionId, castTarget)) return;
            cancel!();
            cancelled = true;
        }
        catch (Exception ex) { Report(ex); }
    }

    private void Report(Exception ex)
    {
        if (!errorLogged) log.Error(ex, "Action convenience failed");
        errorLogged = true;
    }
}

