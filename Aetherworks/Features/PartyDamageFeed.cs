using System.Globalization;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;

namespace Aetherworks.Features;

internal sealed class PartyDamageFeed(IDalamudPluginInterface plugin, IPluginLog log)
{
    private const string Channel = "Aetherworks.PartyDamage";
    private ICallGateProvider<JObject, bool>? receiver;
    private ICallGateSubscriber<JObject, bool>? sender;
    private long nextCheck;
    private bool subscribed;
    private volatile Dictionary<string, DamageEntry> damage = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, DamageEntry> Damage => damage;
    public bool Connected => subscribed;

    public void Start()
    {
        receiver = plugin.GetIpcProvider<JObject, bool>(Channel);
        receiver.RegisterFunc(Receive);
        sender = plugin.GetIpcSubscriber<JObject, bool>("IINACT.IpcProvider." + Channel);
        nextCheck = 0;
    }

    public void CheckConnection()
    {
        if (Environment.TickCount64 < nextCheck || sender == null) return;
        nextCheck = Environment.TickCount64 + 3000;
        if (subscribed && sender.HasAction) return;
        subscribed = false;
        Clear();
        try
        {
            var create = plugin.GetIpcSubscriber<string, bool>("IINACT.CreateSubscriber");
            if (!create.HasFunction || !create.InvokeFunc(Channel)) return;
            sender.InvokeAction(new JObject { ["call"] = "subscribe", ["events"] = new JArray("CombatData") });
            subscribed = true;
        }
        catch (Exception ex) { log.Debug(ex, "Waiting for IINACT combat data subscription"); }
    }

    private bool Receive(JObject payload)
    {
        if ((string?)payload["type"] != "CombatData" || payload["Combatant"] is not JObject entries) return true;
        var snapshot = new Dictionary<string, DamageEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Properties())
        {
            if (entry.Value is JObject combatant && long.TryParse((string?)combatant["damage"],
                NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var total) && total >= 0)
                snapshot[entry.Name] = new DamageEntry(total, (string?)combatant["damage%"]);
        }
        damage = snapshot;
        return true;
    }

    public void Clear() => damage = new(StringComparer.OrdinalIgnoreCase);

    public void Stop()
    {
        try
        {
            var unsubscribe = plugin.GetIpcSubscriber<string, bool>("IINACT.Unsubscribe");
            if (sender != null && unsubscribe.HasFunction) unsubscribe.InvokeFunc(Channel);
        }
        catch (Exception ex) { log.Debug(ex, "IINACT disconnected before cleanup"); }
        receiver?.UnregisterFunc();
        receiver = null;
        sender = null;
        subscribed = false;
        Clear();
    }
}
