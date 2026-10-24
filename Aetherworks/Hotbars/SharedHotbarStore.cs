using System.Text.Json;

namespace Aetherworks.Hotbars;

public sealed class SharedHotbarStore(string directory) : ISharedHotbarStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SharedHotbarState Load(string character)
    {
        var path = StatePath(character);
        if (!File.Exists(path)) return new();
        var state = JsonSerializer.Deserialize<SharedHotbarState>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Shared hotbar state is empty.");
        state.Validate();
        return state;
    }

    public void Save(string character, SharedHotbarState state)
    {
        state.Validate();
        Write(StatePath(character), state);
    }

    public void Backup(SharedHotbarSnapshot before, SharedHotbarPlan plan)
    {
        before.Validate();
        var folder = Path.Combine(directory, "backups", CharacterKey(before.Context.Character));
        var backup = new { Version = 1, CreatedUtc = DateTime.UtcNow, Before = before, plan.Edits, plan.InitialBars };
        if (plan.InitialBars.Count != 0)
            Write(Path.Combine(folder, $"initial-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json"), backup);
        Write(Path.Combine(folder, "last-change.json"), backup);
    }

    private string StatePath(string character) => Path.Combine(directory, CharacterKey(character) + ".json");
    private static string CharacterKey(string character) => character.Length == 16 && character.All(Uri.IsHexDigit)
        ? character : throw new InvalidDataException("Invalid character key.");

    private static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, JsonOptions);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
