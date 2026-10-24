using System.Security.Cryptography;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherworks.Hotbars;

public sealed unsafe class NativeSharedHotbars(IClientState client, IPlayerState player,
    IObjectTable objects, ICondition condition) : ISharedHotbarAccess
{
    public SharedHotbarSnapshot? Capture(out string waitingReason)
    {
        if (!TryContext(out var module, out var context, out waitingReason)) return null;
        var pvpIndex = PvPIndex(module);
        var bars = new Dictionary<int, SharedBar>();
        for (var bar = 0; bar < 18; bar++)
        {
            if ((context.SharedMask & (1u << bar)) == 0) continue;
            var pve = new HotbarCommand[SharedHotbarSnapshot.SlotCount(bar)];
            var pvp = new HotbarCommand[pve.Length];
            for (var slot = 0; slot < pve.Length; slot++)
            {
                pve[slot] = Saved(module, 0, bar, slot);
                pvp[slot] = Saved(module, pvpIndex, bar, slot);
                if (Live(module, bar, slot) != (context.PvP ? pvp[slot] : pve[slot]))
                {
                    waitingReason = "Waiting for live hotbars and saved assignments to agree.";
                    return null;
                }
            }
            bars.Add(bar, new(pve, pvp));
        }
        return new(context, bars);
    }

    public void Write(HotbarContext context, SharedSlotEdit edit)
    {
        var module = RequireContext(context);
        if (edit.Slot < 0 || edit.Slot >= SharedHotbarSnapshot.SlotCount(edit.Bar) ||
            (context.SharedMask & (1u << edit.Bar)) == 0)
            throw new InvalidDataException("Only shared hotbar slots can be synchronized.");
        Validate(edit.After);
        var storage = edit.PvP ? PvPIndex(module) : 0;
        if (Saved(module, storage, edit.Bar, edit.Slot) != edit.Before ||
            edit.PvP == context.PvP && Live(module, edit.Bar, edit.Slot) != edit.Before)
            throw new InvalidOperationException("A hotbar slot changed before it could be written.");
        if (edit.PvP == context.PvP)
            module->SetAndSaveSlot((uint)edit.Bar, (uint)edit.Slot,
                (RaptureHotbarModule.HotbarSlotType)edit.After.Type, edit.After.Id, false, true);
        else
        {
            var source = new RaptureHotbarModule.HotbarSlot
            {
                CommandType = (RaptureHotbarModule.HotbarSlotType)edit.After.Type,
                CommandId = edit.After.Id,
            };
            // The native writer resolves the shared group and marks the hotbar file for saving.
            module->WriteSavedSlot(context.Job, (uint)edit.Bar, (uint)edit.Slot, &source, false, edit.PvP);
        }
        module = RequireContext(context);
        if (Saved(module, storage, edit.Bar, edit.Slot) != edit.After ||
            edit.PvP == context.PvP && Live(module, edit.Bar, edit.Slot) != edit.After)
            throw new InvalidOperationException("A shared hotbar write could not be verified.");
    }

    public void Flush(HotbarContext context) => RequireContext(context)->SaveFile(true);

    private RaptureHotbarModule* RequireContext(HotbarContext expected)
    {
        if (!TryContext(out var module, out var current, out _) || current != expected)
            throw new InvalidOperationException("Character, job, mode or shared hotbar settings changed.");
        return module;
    }

    private bool TryContext(out RaptureHotbarModule* module, out HotbarContext context, out string reason)
    {
        module = null;
        context = null!;
        reason = "Waiting for login and ready hotbars.";
        if (!client.IsLoggedIn || !player.IsLoaded || player.ContentId == 0 || objects.LocalPlayer is not { } local)
            return false;
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        { reason = "Waiting for the area change to finish."; return false; }
        if (condition[ConditionFlag.InCombat])
        { reason = "Sync paused during combat; edits will sync afterwards."; return false; }
        module = RaptureHotbarModule.Instance();
        if (module == null || !module->ModuleReady || !module->DatFileLoadedSuccessfully ||
            local.ClassJob.RowId == 0 || module->ActiveHotbarClassJobId != local.ClassJob.RowId) return false;
        uint mask = 0;
        for (var bar = 0; bar < 18; bar++)
            if (module->IsHotbarShared((uint)bar)) mask |= 1u << bar;
        var character = Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(player.ContentId)))[..16];
        context = new(character, local.ClassJob.RowId, module->PvPHotbarsActive, client.TerritoryType, mask);
        reason = string.Empty;
        return true;
    }

    private static int PvPIndex(RaptureHotbarModule* module)
    {
        var index = module->GetPvPSavedHotbarIndexForClassJobId(0, true);
        if (index <= 0 || index >= module->SavedHotbars.Length)
            throw new InvalidOperationException("The shared PvP hotbar storage is unavailable.");
        return index;
    }

    private static HotbarCommand Saved(RaptureHotbarModule* module, int storage, int bar, int slot)
    {
        ref var value = ref module->SavedHotbars[storage].Hotbars[bar].Slots[slot];
        return Validate(new((byte)value.CommandType, value.CommandId));
    }

    private static HotbarCommand Live(RaptureHotbarModule* module, int bar, int slot)
    {
        ref var value = ref module->Hotbars[bar].Slots[slot];
        return Validate(new((byte)value.CommandType, value.CommandId));
    }

    private static HotbarCommand Validate(HotbarCommand value)
    {
        if (!Enum.IsDefined((RaptureHotbarModule.HotbarSlotType)value.Type))
            throw new InvalidDataException("A shared hotbar contains an unsupported command type.");
        return value.Normalized;
    }
}
