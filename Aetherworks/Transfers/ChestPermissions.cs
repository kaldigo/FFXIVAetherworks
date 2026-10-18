namespace Aetherworks.Transfers;

public static class ChestPermissions
{
    // FC rank permission bytes: three base-access bits per item tab, with
    // deposit-only flags stored separately. Zero/unrecognized data is unknown.
    public static bool? CanDeposit(ReadOnlySpan<byte> permissions, uint page)
    {
        if (permissions.Length < 6 || page is < 20000 or > 20004) return null;
        var tab = (int)(page - 20000);
        var packed = permissions[1] | permissions[2] << 8 | permissions[3] << 16;
        var access = (packed >> (7 + tab * 3)) & 7;
        var depositOnly = tab < 4 ? (permissions[4] & (1 << (4 + tab))) != 0 : (permissions[5] & 1) != 0;
        if (access is not (0 or 1 or 2 or 4)) return null;
        if (depositOnly) return true;
        return access switch { 4 => true, 1 or 2 => false, _ => null };
    }
}
