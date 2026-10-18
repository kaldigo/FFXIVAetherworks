namespace Aetherworks.Tweaks;

public sealed class TweakEntry(ITweak tweak)
{
    public ITweak Tweak { get; } = tweak;
    public bool IsEnabled { get; internal set; }
    public bool NeedsCleanup { get; internal set; }
    public string? Error { get; internal set; }
}

/// <summary>Call from the UI/framework thread. Owns every registered tweak's lifecycle.</summary>
public sealed class TweakManager : IDisposable
{
    private readonly HashSet<string> enabled;
    private readonly Action save;
    private readonly Action<Exception, string> log;
    private readonly List<TweakEntry> entries;
    private bool disposed;

    public IReadOnlyList<TweakEntry> Entries => entries.AsReadOnly();
    public string? SaveError { get; private set; }

    public TweakManager(IEnumerable<ITweak> tweaks, HashSet<string> enabled, Action save,
        Action<Exception, string> log)
    {
        this.enabled = enabled;
        this.save = save;
        this.log = log;
        entries = tweaks.Select(t => new TweakEntry(t)).ToList();
        if (entries.Any(e => string.IsNullOrWhiteSpace(e.Tweak.Id)) ||
            entries.Select(e => e.Tweak.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count)
            throw new ArgumentException("Tweak IDs must be nonempty and unique.", nameof(tweaks));
    }

    public void RestoreEnabled()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (var entry in entries)
            if (enabled.Contains(entry.Tweak.Id)) SetEnabled(entry, true);
    }

    public void SetEnabled(TweakEntry entry, bool value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!entries.Contains(entry)) throw new ArgumentException("Unknown tweak.", nameof(entry));
        if (value && entry.NeedsCleanup) return;
        entry.Error = null;
        if (value && !entry.IsEnabled)
        {
            entry.NeedsCleanup = true;
            try
            {
                entry.Tweak.Enable();
                entry.IsEnabled = true;
                entry.NeedsCleanup = false;
            }
            catch (Exception ex)
            {
                Report(entry, ex, "Enable failed");
                Cleanup(entry);
            }
        }
        else if (!value && (entry.IsEnabled || entry.NeedsCleanup)) Cleanup(entry);

        if (entry.IsEnabled && !entry.NeedsCleanup) enabled.Add(entry.Tweak.Id);
        else enabled.Remove(entry.Tweak.Id);
        Save();
    }

    public void Save()
    {
        try { save(); SaveError = null; }
        catch (Exception ex)
        {
            SaveError = "Settings could not be saved. Your changes may not survive a reload.";
            log(ex, "Saving Aetherworks settings failed");
        }
    }

    private void Cleanup(TweakEntry entry)
    {
        entry.IsEnabled = false;
        entry.NeedsCleanup = true;
        try { entry.Tweak.Disable(); entry.NeedsCleanup = false; }
        catch (Exception ex) { Report(entry, ex, "Cleanup failed; some behavior may still be active"); }
    }

    private void Report(TweakEntry entry, Exception ex, string message)
    {
        entry.Error = entry.Error is null ? message : entry.Error + ". " + message;
        log(ex, $"{entry.Tweak.Id}: {message}");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // Unloading stops behavior without changing the user's saved choices.
        foreach (var entry in entries.AsEnumerable().Reverse())
            if (entry.IsEnabled || entry.NeedsCleanup) Cleanup(entry);
    }
}
