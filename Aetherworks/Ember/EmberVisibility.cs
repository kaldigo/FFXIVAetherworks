namespace Aetherworks.Ember;

internal sealed class EmberVisibility
{
    private bool inCombat;
    private long? combatEnd;

    public bool Update(bool on, bool automatic, bool fighting, int delaySeconds, long now)
    {
        if (fighting != inCombat)
        {
            inCombat = fighting;
            combatEnd = fighting ? null : now;
        }
        bool withinDelay = combatEnd is long end && now - end < delaySeconds * 1000L;
        return on && (!automatic || fighting || withinDelay);
    }
}
