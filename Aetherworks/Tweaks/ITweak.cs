namespace Aetherworks.Tweaks;

/// <summary>Constructors must be inert. All feature behavior belongs to Enable/Disable.</summary>
public interface ITweak
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; }
    void Enable();
    // Must be safe after partial Enable and repeated calls; release all hooks/subscriptions.
    void Disable();
}
