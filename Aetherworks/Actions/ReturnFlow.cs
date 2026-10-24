namespace Aetherworks.Actions;

/// <summary>One user request, one current quote, and at most one paid execution.</summary>
public sealed class ReturnFlow(Func<bool> returnReady, Func<ReturnRequest?> quote, Func<bool> skipConfirmation,
    Func<ReturnRequest, bool> teleport, Func<bool> useReturn, Func<long> now)
{
    private ReturnRequest? pending;
    public bool Enabled { get; set; }
    public event Action? ConfirmationRequested;

    // Null delegates to the original action. False consumes a failed Teleport without retrying it.
    public bool? OnReturn()
    {
        if (!Enabled || returnReady()) { Cancel(); return null; }
        var request = quote();
        if (request is null) { Cancel(); return null; }
        if (skipConfirmation()) { Cancel(); return Execute(request); }
        pending = request;
        ConfirmationRequested?.Invoke();
        return true;
    }

    public ReturnRequest? GetPending()
    {
        if (Enabled && pending is { } request && quote() is { } current &&
            request.IsCurrent(current.Character, current.Territory, current.Home, current.Fare, now()))
            return request;
        Cancel();
        return null;
    }

    public bool Confirm()
    {
        var request = GetPending();
        Cancel();
        return request is not null && Execute(request);
    }

    private bool Execute(ReturnRequest request)
    {
        if (!Enabled || quote() is not { } current ||
            !request.IsCurrent(current.Character, current.Territory, current.Home, current.Fare, now())) return false;
        return returnReady() ? useReturn() : teleport(request);
    }

    public void Cancel() => pending = null;
}
