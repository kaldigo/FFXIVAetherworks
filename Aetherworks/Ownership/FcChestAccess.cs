using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Aetherworks.Transfers;

namespace Aetherworks.Ownership;

public static class FcChestAccess
{
    public static unsafe bool? CanDeposit(IGameGui gui, ulong character, uint page)
    {
        if (character == 0 || CurrentPage(gui) is null) return null;
        var info = InfoModule.Instance();
        var company = InfoProxyFreeCompany.Instance();
        var members = InfoProxyFreeCompanyMember.Instance();
        if (info == null || info->LocalContentId != character || company == null || company->Id == 0 ||
            members == null || members->FreeCompanyId != company->Id || members->CharData == null ||
            members->EntryCount > 200) return null;
        foreach (var member in members->CharDataSpan)
        {
            if (member.ContentId != character) continue;
            // Expansion's GetMemberRankNameText/GetRankData use this rank index
            // and the 16-entry table at 0x140, stride 0x58 (revision b1ccab64).
            // The SDK's older 14-entry Ranks field uses a different table offset.
            var rankIndex = (byte)(member.ExtraFlags >> 12);
            if (rankIndex >= 16) return null;
            var rank = (byte*)company + 0x140 + rankIndex * 0x58;
            if (rank[0x23] == 0) return null; // Rank details have not been loaded.
            return ChestPermissions.CanDeposit(new ReadOnlySpan<byte>(rank, 10), page);
        }
        return null;
    }

    public static unsafe uint[] AccessiblePages(IGameGui gui)
    {
        if (CurrentPage(gui) is null) return [];
        var chest = (AtkUnitBase*)gui.GetAddonByName("FreeCompanyChest").Address;
        var pages = new List<uint>();
        // ECommons AddonMaster.FreeCompanyChest identifies item buttons by IDs 10–14.
        for (uint i = 0; i < 5; i++)
        {
            var node = chest->GetComponentNodeById(10 + i);
            if (node == null || !node->IsVisible()) continue;
            var button = node->GetAsAtkComponentRadioButton();
            if (button != null && button->IsEnabled) pages.Add(20000 + i);
        }
        return pages.ToArray();
    }

    public static unsafe bool SelectPage(IGameGui gui, uint page)
    {
        if (!AccessiblePages(gui).Contains(page)) return false;
        if (CurrentPage(gui) == page) return true;
        var chest = (AtkUnitBase*)gui.GetAddonByName("FreeCompanyChest").Address;
        var button = chest->GetComponentNodeById(10 + page - 20000)->GetAsAtkComponentRadioButton();
        var evt = button->OwnerNode->AtkResNode.AtkEventManager.Event;
        if (evt == null) return false;
        // Dispatch the registered radio-button event, as ECommons ClickRadioButton does.
        chest->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt);
        return true;
    }

    public static unsafe uint? CurrentPage(IGameGui gui)
    {
        var addon = gui.GetAddonByName("FreeCompanyChest");
        if (addon.IsNull || !addon.IsVisible) return null;
        var chest = (AtkUnitBase*)addon.Address;
        // Pandora's current-tab layout. Reject unknown layouts and non-item views;
        // never default to tab 1 when the selected tab cannot be identified.
        var uld = &chest->UldManager;
        if (uld->NodeList == null || uld->NodeListCount <= 101) return null;
        if (uld->NodeList[4] == null || uld->NodeList[7] == null ||
            uld->NodeList[4]->IsVisible() || uld->NodeList[7]->IsVisible()) return null;
        uint? selected = null;
        for (var nodeIndex = 101; nodeIndex >= 97; nodeIndex--)
        {
            var node = uld->NodeList[nodeIndex];
            if (node == null || !node->IsVisible()) continue;
            var button = node->GetAsAtkComponentRadioButton();
            if (button == null || !button->IsChecked) continue;
            if (selected is not null) return null;
            selected = (uint)InventoryType.FreeCompanyPage1 + (uint)(101 - nodeIndex);
        }
        return selected;
    }

}

