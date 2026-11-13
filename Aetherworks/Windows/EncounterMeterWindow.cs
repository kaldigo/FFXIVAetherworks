using System.Numerics;
using Aetherworks.Features;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherworks.Windows;

internal sealed class EncounterMeterWindow : Window
{
    private readonly PartyDamageFeed feed;
    private readonly Configuration config;
    private readonly Action save;
    private readonly Func<string?> localName;

    public EncounterMeterWindow(PartyDamageFeed feed, Configuration config, Action save, Func<string?> localName)
        : base("Encounter damage###AetherworksEncounterDamage")
    {
        this.feed = feed;
        this.config = config;
        this.save = save;
        this.localName = localName;
        Size = new Vector2(560, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(320, 160), MaximumSize = new(float.MaxValue) };
    }

    public override void Draw()
    {
        if (ImGui.Button("Settings")) ImGui.OpenPopup("Meter settings");
        if (ImGui.BeginPopup("Meter settings"))
        {
            bool total = config.MeterShowDamage, dps = config.MeterShowDps, job = config.MeterShowJob;
            bool changed = ImGui.Checkbox("Total damage", ref total);
            changed |= ImGui.Checkbox("Damage per second", ref dps);
            changed |= ImGui.Checkbox("Job", ref job);
            if (changed)
            {
                config.MeterShowDamage = total;
                config.MeterShowDps = dps;
                config.MeterShowJob = job;
                save();
            }
            ImGui.EndPopup();
        }

        var encounter = feed.Encounter;
        ImGui.SameLine();
        ImGui.TextUnformatted(string.Join("   ", new[] { encounter.Title, encounter.Duration }.Where(s => !string.IsNullOrEmpty(s))));
        if (encounter.Rows.Length == 0)
        {
            ImGui.TextDisabled(feed.Connected ? "Waiting for encounter damage." : "Waiting for IINACT.");
            return;
        }

        int columns = 3 + (config.MeterShowJob ? 1 : 0) + (config.MeterShowDamage ? 1 : 0) + (config.MeterShowDps ? 1 : 0);
        if (!ImGui.BeginTable("Combatants", columns, ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY)) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Combatant", ImGuiTableColumnFlags.WidthStretch);
        if (config.MeterShowJob) ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed);
        if (config.MeterShowDamage) ImGui.TableSetupColumn("Damage", ImGuiTableColumnFlags.WidthFixed);
        if (config.MeterShowDps) ImGui.TableSetupColumn("DPS", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Share", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();
        int rank = 0;
        for (int i = 0; i < encounter.Rows.Length; i++)
        {
            var row = encounter.Rows[i];
            if (i == 0 || encounter.Rows[i - 1].Damage != row.Damage) rank = i + 1;
            ImGui.TableNextRow();
            Cell(rank.ToString());
            Cell(row.Name == "YOU" ? localName() ?? row.Name : row.Name);
            if (config.MeterShowJob) Cell(row.Job);
            if (config.MeterShowDamage) Cell(row.Damage.ToString("N0"));
            if (config.MeterShowDps) Cell(row.Dps);
            Cell(row.Share ?? "—");
        }
        ImGui.EndTable();
    }

    private static void Cell(string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(value);
    }
}
