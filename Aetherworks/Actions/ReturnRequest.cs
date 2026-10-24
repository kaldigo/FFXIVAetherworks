namespace Aetherworks.Actions;

// A confirmation authorizes one fare for one destination and character, for at most 30 seconds.
public sealed record ReturnRequest(ulong Character, uint Territory, uint Home, uint Fare, string Destination, long Expires)
{
    public bool IsCurrent(ulong character, uint territory, uint home, uint fare, long now) =>
        Character != 0 && Home != 0 && Character == character && Territory == territory &&
        Home == home && Fare == fare && now < Expires;
}
