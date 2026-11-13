using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherworks.Windows;

internal sealed class AetherworksShell
{
    internal const string ReturnId = "return-via-teleport", HotbarId = "shared-hotbar-sync", SaddlebagId = "saddlebag-withdraw-leave-one";
    internal SettingsNavigation Navigation { get; } = new();
    private float scale = 1;
    private AetherworksUiState state = null!;
    private AetherworksUiActions actions = null!;
    private bool resetScroll;
    private bool inCard;
    private float AvailableWidth => ImGui.GetContentRegionAvail().X-(inCard?14*scale:0);
    private bool Compact => AvailableWidth < 440*scale;

    internal void Draw(AetherworksUiState view,AetherworksUiActions handlers,float uiScale)
    {
        state = view; actions = handlers; scale = uiScale;
        AetherworksChrome.BackgroundFill();
        string summary=state.IssueCount>0?"Needs attention":state.EnabledCount==0?"All off":$"{state.EnabledCount} enabled";
        AetherworksChrome.Header(summary,actions.Close,scale,state.IssueCount>0?UiTone.Attention:state.EnabledCount>0?UiTone.Good:UiTone.Neutral);
        if (ImGui.BeginChild("Aetherworks rail",new Vector2(48*scale,0),false,ImGuiWindowFlags.NoScrollbar))
            foreach(var area in Enum.GetValues<Workspace>())
                if (AetherworksChrome.RailItem(area,Navigation.Area==area,scale)) Navigation.OpenArea(area);
        ImGui.EndChild();
        ImGui.SameLine(0,8*scale);
        if (ImGui.BeginChild("Aetherworks workspace",Vector2.Zero,false))
        {
            if (state.SaveError != null)
            {
                Message("Changes are not saved",state.SaveError,UiTone.Error);
                if (ImGui.Button("Retry saving")) actions.RetrySave();
                Divider();
            }
            if (Navigation.Feature==null && Navigation.Area==Workspace.Features)
            {
                ImGui.SetNextItemWidth(-1);
                if(ImGui.InputTextWithHint("##Find feature","Find a feature...",ref Navigation.Search,128)) resetScroll=true;
            }
            if (ImGui.BeginChild($"Content-{Navigation.Area}-{Navigation.Feature}",Vector2.Zero,false))
            {
                if(resetScroll) { ImGui.SetScrollY(0); resetScroll=false; }
                if (Navigation.Feature is {} id)
                {
                    if (ImGui.Button("< Back")) Navigation.Back();
                    ImGui.SameLine(); Secondary(Navigation.BackLabel);
                    var feature = state.Features.FirstOrDefault(f=>f.Id==id);
                    if (feature!=null) DrawFeature(feature);
                }
                else switch(Navigation.Area)
                {
                    case Workspace.Overview: DrawOverview(); break;
                    case Workspace.Features: DrawFeatures(); break;
                    default: DrawRecovery(); break;
                }
            }
            ImGui.EndChild();
        }
        ImGui.EndChild();
    }
    private void Heading(string text,string? detail=null)
    {
        ImGui.Spacing(); ImGui.TextWrapped(text);
        if (detail!=null) Secondary(detail);
        ImGui.Spacing();
    }
    private static void Secondary(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text,AetherworksChrome.Muted);
        ImGui.TextWrapped(text); ImGui.PopStyleColor();
    }
    private void Divider()
    {
        var p=ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(p,p+new Vector2(AvailableWidth,0),
            ImGui.GetColorU32(new Vector4(.17f,.20f,.25f,1)),scale);
        ImGui.Dummy(new Vector2(AvailableWidth,scale));
    }
    private void Section(string id,Action draw)
    {
        ImGui.PushID(id);
        var p=ImGui.GetCursorScreenPos();
        float width=ImGui.GetContentRegionAvail().X, padding=14*scale;
        var dl=ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(width,4*scale));
        ImGui.Indent(padding);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+width-2*padding);
        inCard=true;
        try
        {
            draw();
        }
        finally
        {
            inCard=false;
            ImGui.PopTextWrapPos();
            ImGui.Unindent(padding);
            ImGui.Dummy(new Vector2(width,4*scale));
            ImGui.EndGroup();
            float bottom=ImGui.GetItemRectMax().Y;
            dl.ChannelsSetCurrent(0);
            dl.AddRectFilled(p,new Vector2(p.X+width,bottom),ImGui.GetColorU32(AetherworksChrome.Surface),7*scale);
            dl.AddRect(p,new Vector2(p.X+width,bottom),ImGui.GetColorU32(new Vector4(.17f,.20f,.25f,.65f)),7*scale,ImDrawFlags.None,scale);
            dl.ChannelsMerge();
            ImGui.PopID();
        }
        ImGui.Spacing();
    }
    private static void Message(string title,string detail,UiTone tone)
    {
        ImGui.PushStyleColor(ImGuiCol.Text,AetherworksChrome.Tone(tone));
        ImGui.TextWrapped(title); ImGui.PopStyleColor();
        ImGui.TextWrapped(detail);
    }
    private void Toggle(FeatureView feature)
    {
        bool enabled=feature.Enabled;
        if (SwitchRow("Enable feature","Saved preference",ref enabled,feature.NeedsCleanup)) actions.SetFeature(feature.Id,enabled);
        if(feature.NeedsCleanup) Secondary("Cleanup must finish before this feature can be enabled again.");
    }
    private bool SwitchRow(string label,string? detail,ref bool value,bool disabled=false)
    {
        var p=ImGui.GetCursorScreenPos();
        float width=AvailableWidth,controlWidth=44*scale;
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+Math.Max(40*scale,width-controlWidth-16*scale));
        ImGui.TextWrapped(label);
        if(detail!=null) Secondary(detail);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        float height=Math.Max(ImGui.GetItemRectSize().Y,24*scale);
        ImGui.SameLine(0,0);
        ImGui.SetCursorScreenPos(p+new Vector2(width-controlWidth,(height-24*scale)/2));
        ImGui.BeginDisabled(disabled);
        bool changed=AetherworksChrome.Switch(label,ref value,scale);
        ImGui.EndDisabled();
        ImGui.SetCursorScreenPos(p+new Vector2(0,height));
        ImGui.Dummy(Vector2.Zero);
        return changed;
    }
    private void ActionRow(string title,string detail,string button,Action click,UiTone tone=UiTone.Neutral)
    {
        var p=ImGui.GetCursorScreenPos();
        float width=AvailableWidth,buttonWidth=ImGui.CalcTextSize(button).X+24*scale;
        bool stack=width<380*scale;
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+(stack?width:width-buttonWidth-20*scale));
        if(tone!=UiTone.Neutral) ImGui.PushStyleColor(ImGuiCol.Text,AetherworksChrome.Tone(tone));
        ImGui.TextWrapped(title);
        if(tone!=UiTone.Neutral) ImGui.PopStyleColor();
        Secondary(detail);
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        float height=ImGui.GetItemRectSize().Y;
        if(stack) { ImGui.SetCursorPosX(ImGui.GetCursorPosX()+width-buttonWidth); }
        else
        {
            ImGui.SameLine(0,0);
            ImGui.SetCursorScreenPos(p+new Vector2(width-buttonWidth,Math.Max(0,(height-ImGui.GetFrameHeight())/2)));
        }
        if(ImGui.Button(button,new Vector2(buttonWidth,0))) click();
        if(!stack)
        {
            ImGui.SetCursorScreenPos(p+new Vector2(0,Math.Max(height,ImGui.GetFrameHeight())));
            ImGui.Dummy(Vector2.Zero);
        }
    }
    private void Metrics()
    {
        bool compact=AvailableWidth<540*scale;
        bool syncOn=state.Features.Any(f=>f.Id==HotbarId && f.Enabled);
        (string Value,string Label,UiTone Tone)[] metrics=compact
            ? [($"{state.EnabledCount} / {state.Features.Count}","Enabled",state.EnabledCount>0?UiTone.Good:UiTone.Neutral),(state.IssueCount.ToString(),"Needs review",state.IssueCount>0?UiTone.Attention:UiTone.Neutral)]
            : [($"{state.EnabledCount} / {state.Features.Count}","Enabled",state.EnabledCount>0?UiTone.Good:UiTone.Neutral),
                (state.SharedBars.Count>0?state.SharedBars.Count.ToString():"—",syncOn?"Shared bars observed":"Hotbar sync is off",UiTone.Neutral),
                (state.IssueCount.ToString(),"Needs review",state.IssueCount>0?UiTone.Attention:UiTone.Neutral)];
        float gap=8*scale,width=(AvailableWidth-gap*(metrics.Length-1))/metrics.Length;
        var p=ImGui.GetCursorScreenPos();
        var dl=ImGui.GetWindowDrawList();
        for(int i=0;i<metrics.Length;i++)
        {
            var at=p+new Vector2(i*(width+gap),0);
            dl.AddRectFilled(at,at+new Vector2(width,64*scale),ImGui.GetColorU32(AetherworksChrome.Surface),7*scale);
            dl.AddRect(at,at+new Vector2(width,64*scale),ImGui.GetColorU32(new Vector4(.17f,.20f,.25f,1)),7*scale,ImDrawFlags.None,scale);
            dl.AddText(ImGui.GetFont(),ImGui.GetFontSize()*1.3f,at+new Vector2(12,9)*scale,ImGui.GetColorU32(AetherworksChrome.Tone(metrics[i].Tone)),metrics[i].Value);
            dl.AddText(at+new Vector2(12,39)*scale,ImGui.GetColorU32(AetherworksChrome.Muted),metrics[i].Label);
        }
        ImGui.Dummy(new Vector2(AvailableWidth,64*scale));
        ImGui.Spacing();
    }
    private void DrawOverview()
    {
        Heading("Overview","Your conveniences, at a glance.");
        Metrics();
        if(state.IssueCount>0) Section("Attention",()=>
        {
            ActionRow("Needs your attention",state.Conflicts.Count>0
                ? "Shared slots are waiting for a PvE/PvP choice." : "Review current errors and recovery options.",
                "Review status",()=>Navigation.OpenArea(Workspace.Recovery),UiTone.Attention);
        });
        Heading("Quick controls");
        Section("Quick controls",()=>
        {
            for(int i=0;i<state.Features.Count;i++)
            {
                var feature=state.Features[i];
                ImGui.PushID(feature.Id);
                bool enabled=feature.Enabled;
                if(SwitchRow(feature.Name,null,ref enabled,feature.NeedsCleanup)) actions.SetFeature(feature.Id,enabled);
                ActionRow(feature.Enabled?"Enabled":"Off",feature.Enabled||feature.Error!=null||feature.NeedsCleanup?feature.Status:ShortDescription(feature.Id),
                    "Settings",()=>Navigation.OpenFeature(feature.Id),feature.Tone);
                if(i<state.Features.Count-1) { ImGui.Spacing(); Divider(); ImGui.Spacing(); }
                ImGui.PopID();
            }
        });
        Secondary("Settings save automatically. Each feature is independent.");
    }
    private void DrawFeatures()
    {
        Heading("Features","Find a convenience and adjust how it works.");
        var matches=state.Features.Where(f=>SettingsNavigation.Matches(f,Navigation.Search)).ToArray();
        if(matches.Length==0)
        {
            Section("Empty search",()=>
            {
                ImGui.TextWrapped("No matching features");
                Secondary("Try Return, hotbars, saddlebag or a category name.");
                if(ImGui.Button("Clear search")) { Navigation.Search=""; resetScroll=true; }
            });
            return;
        }
        foreach(var group in matches.GroupBy(f=>f.Category))
        {
            Secondary(group.Key);
            foreach(var feature in group) Section(feature.Id,()=>
            {
                ActionRow(feature.Name,ShortDescription(feature.Id),"Configure",()=>Navigation.OpenFeature(feature.Id));
                Secondary(feature.Enabled?"Enabled":"Off");
            });
        }
    }
    private void DrawFeature(FeatureView feature)
    {
        Heading(feature.Name,feature.Category);
        Section("Feature state",()=>
        {
            if(feature.Id==HotbarId && !feature.Enabled)
            {
                ImGui.TextWrapped("The first sync copies shared PvE assignments into PvP.");
                Secondary("Existing PvP assignments are backed up before replacement. Only bars marked Shared are included.");
                ImGui.Spacing();
            }
            Toggle(feature); ImGui.Spacing();
            Message(feature.NeedsCleanup?"Cleanup required":feature.Enabled?"Current state":"Feature is off",feature.Status,feature.Tone);
            FeatureError(feature);
        });
        if(feature.Id==ReturnId) DrawReturn();
        else if(feature.Id==HotbarId) DrawHotbars(feature);
        else if(feature.Id==SaddlebagId) DrawSaddlebag();
        else if(feature.Id=="party-damage" && feature.Enabled)
        {
            int mode = state.PartyDamageBeforeRequestedUpdate ? 1 : 0;
            if (ImGui.Combo("Entry point", ref mode, new[] { "Before draw", "After requested update" }, 2))
                actions.SetPartyDamageEntryPoint?.Invoke(mode == 1);
            Secondary(feature.Status);
        }
        Secondary("Changes save automatically. Feature choices are independent.");
    }
    private void DrawReturn()
    {
        Heading("Paid travel");
        Section("Confirmation",()=>
        {
            bool confirm=!state.SkipConfirmation;
            if(SwitchRow("Ask before paid travel","Show the home destination and current fare",ref confirm)) actions.SetSkipConfirmation(!confirm);
            ImGui.Spacing();
            if(state.SkipConfirmation) Message("Paid Teleport starts immediately",
                "When Return is on cooldown, pressing it spends the normal fare without an Aetherworks confirmation.",UiTone.Attention);
            else ImGui.TextWrapped("Review your home destination and the current fare before confirming paid Teleport.");
            Secondary("This preference applies while Return via Teleport is enabled.");
        });
        Heading("Using Return");
        Section("Return behavior",()=>
        {
            ImGui.TextWrapped("Ready"); Secondary("Free Return, as usual.");
            Divider();
            ImGui.TextWrapped("On cooldown"); Secondary("Paid Teleport to your current home point.");
        });
        if(ImGui.CollapsingHeader("Travel rules"))
            Secondary("Normal travel restrictions and aetheryte ticket settings apply. No trip is queued for later.");
    }
    private void DrawHotbars(FeatureView feature)
    {
        if(state.Conflicts.Count>0) DrawConflicts();
        Heading("Included bars");
        Section("Included",()=>
        {
            if(state.SharedBars.Count==0) Secondary(feature.Enabled
                ? "No shared bars are currently available. Check the current state above."
                : "Enable the feature to read the bars marked Shared in Character Configuration.");
            else foreach(var bar in state.SharedBars) ImGui.BulletText(bar);
        });
        Heading("Sync behavior");
        Section("Behavior",()=>
        {
            ImGui.TextWrapped("Newly tracked bars start from PvE. Later edits and cleared slots sync in both directions.");
            ImGui.Spacing(); Secondary("Shared keyboard and cross hotbars are included. Sprint converts to its PvE or PvP action; other assignments copy as-is.");
            ImGui.Spacing(); Secondary("Sync waits during combat, loading and hotbar edits. Unshared bars, layouts and keybinds are unchanged.");
        });
        if(ImGui.Button("View status & recovery")) Navigation.OpenArea(Workspace.Recovery);
    }
    private void DrawSaddlebag()
    {
        Heading("Keep a copy for next time");
        Section("How to use",()=>
        {
            ImGui.TextWrapped("1. Open your saddlebag.");
            ImGui.TextWrapped("2. Right-click a stack of two or more.");
            ImGui.TextWrapped("3. Choose Withdraw all but one.");
            Divider();
            ImGui.TextWrapped("99 in the stack: withdraw 98, leave 1");
            Secondary("The remaining copy lets Pandora's Entrust Duplicates button match the item when you put it back.");
        });
        Heading("Supported items");
        Section("Scope",()=>
        {
            ImGui.TextWrapped("Regular and premium saddlebags. Only the selected stack is affected; HQ and NQ stay separate.");
            ImGui.Spacing(); Secondary("Stacks of one have no extra menu action. If a stack changes or the native action is unavailable, automatic withdrawal stops.");
            ImGui.Spacing(); Secondary("Check inventory space if an action fails. A quantity dialog left open is for manual handling; an unexpected failure is also reported in chat.");
        });
    }
    private void DrawConflicts()
    {
        Heading("Choose which assignments to keep");
        Section("Conflicts",()=>
        {
            Message($"{state.Conflicts.Count} shared slot(s) need a choice","Both PvE and PvP changed these slots. Other shared slots continue syncing.",UiTone.Attention);
            foreach(var conflict in state.Conflicts) ImGui.BulletText(conflict);
            ImGui.Spacing(); bool compact=Compact;
            float width=compact?AvailableWidth:(AvailableWidth-ImGui.GetStyle().ItemSpacing.X)/2;
            if(ImGui.Button("Keep PvE for conflicts",new Vector2(width,0))) actions.ResolveConflicts(false);
            if(!compact) ImGui.SameLine();
            if(ImGui.Button("Keep PvP for conflicts",new Vector2(width,0))) actions.ResolveConflicts(true);
            Secondary("Applies only to the listed slots. If their assignments change, review them again.");
        });
    }
    private void FeatureError(FeatureView feature)
    {
        if(feature.Error!=null) Message("Feature needs attention",feature.Error+". See the Dalamud log for details.",UiTone.Error);
        if(feature.NeedsCleanup && ImGui.Button("Retry disabling")) actions.SetFeature(feature.Id,false);
    }
    private void DrawRecovery()
    {
        Heading("Status & recovery","Current state and the actions available to you.");
        if(state.IssueCount==0) Section("Healthy",()=>Message("No problems reported",
            "Review feature activity below. An enabled feature may still be waiting for the game.",UiTone.Good));
        if(state.Conflicts.Count>0) DrawConflicts();
        foreach(var feature in state.Features) Section(feature.Id,()=>
        {
            ActionRow(feature.Name,feature.Status,"Settings",()=>Navigation.OpenFeature(feature.Id),feature.Tone);
            FeatureError(feature);
            if(feature.Id==HotbarId && state.SyncFaulted)
                Secondary("Check the error and your backups, then disable and re-enable Shared Hotbar Sync to retry.");
        });
        Heading("Settings & backups");
        Section("Persistence",()=>
        {
            Message(state.SaveError==null?"Settings saved automatically":"Settings need saving",
                state.SaveError??"Feature changes are saved as you make them.",state.SaveError==null?UiTone.Neutral:UiTone.Error);
            Divider(); ImGui.TextWrapped("Shared hotbar backups");
            Secondary("Initial assignments and the latest before-sync state are saved locally for each character.");
            if(ImGui.CollapsingHeader("Backup location"))
                Secondary("Find shared-hotbars/backups inside Aetherworks' plugin configuration folder. Initial backups preserve the original assignments; last-change.json records the most recent before-sync state.");
            if(ImGui.CollapsingHeader("Troubleshooting"))
            {
                ImGui.TextWrapped("Use the Dalamud log for errors. Retain your sync history and backups when investigating a problem; unreadable history pauses sync instead of starting over.");
                ImGui.TextWrapped("Native game behavior still needs in-game validation after an update.");
            }
        });
    }
    private static string ShortDescription(string id) => id switch
    {
        ReturnId=>"A paid trip home when Return is on cooldown.",
        HotbarId=>"Keep shared PvE and PvP assignments in step.",
        "party-damage"=>"Show IINACT damage rank and share beside party names.",
        SaddlebagId=>"Withdraw a stack and leave one for easy entrusting.",
        _=>"An independently enabled convenience."
    };
}
