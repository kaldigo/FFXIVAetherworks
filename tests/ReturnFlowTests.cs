using Aetherworks.Actions;

static class ReturnFlowTests
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var ready = false;
        var skip = false;
        long now = 1_000;
        ReturnRequest? quote = new(123, 100, 8, 999, "Home", 31_000);
        var paid = 0;
        var free = 0;
        var prompts = 0;
        var succeeds = true;
        var flow = new ReturnFlow(() => ready, () => quote, () => skip,
            _ => { paid++; return succeeds; }, () => { free++; return true; }, () => now);
        flow.ConfirmationRequested += () => prompts++;
        Check(flow.OnReturn() is null && paid == 0 && prompts == 0, "Disabled fallback does nothing");
        flow.Enabled = true;
        ready = true;
        Check(flow.OnReturn() is null && paid == 0, "Ready Return goes to the original native action");
        ready = false;
        var original = quote;
        quote = null;
        Check(flow.OnReturn() is null && prompts == 0, "Restricted or unknown destination preserves native behavior");
        quote = original;
        Check(flow.OnReturn() == true && prompts == 1 && paid == 0, "Cooldown requests confirmation without spending");
        Check(flow.GetPending() == quote, "Confirmation contains the current destination and price");
        Check(flow.Confirm() && paid == 1, "Confirm executes one paid Teleport");
        Check(!flow.Confirm() && paid == 1, "Repeated confirm cannot spend again");
        flow.OnReturn(); flow.Cancel();
        Check(!flow.Confirm() && paid == 1, "Cancel spends nothing");
        foreach (var changed in new[] {
            original! with { Character = 124 }, original! with { Territory = 101 },
            original! with { Home = 9 }, original! with { Fare = 1000 } })
        {
            quote = original; flow.OnReturn(); quote = changed;
            Check(flow.GetPending() is null && !flow.Confirm() && paid == 1,
                "Character, territory, home or price change invalidates authorization");
        }
        quote = original; flow.OnReturn(); quote = null;
        Check(flow.GetPending() is null && !flow.Confirm(), "Logout or unavailable travel cancels confirmation");
        quote = original; flow.OnReturn(); now = 31_000;
        Check(flow.GetPending() is null && !flow.Confirm(), "Authorization expires at 30 seconds");
        now = 1000; flow.OnReturn(); flow.Enabled = false;
        Check(!flow.Confirm() && paid == 1, "Disabling cancels an open confirmation");
        flow.Enabled = true; flow.OnReturn(); ready = true;
        Check(flow.Confirm() && free == 1 && paid == 1, "Return becoming ready during confirmation uses free Return");
        ready = false; skip = true;
        var oldPrompts = prompts;
        Check(flow.OnReturn() == true && paid == 2 && prompts == oldPrompts && flow.GetPending() is null,
            "Skip confirmation executes immediately without opening a prompt");
        succeeds = false;
        Check(flow.OnReturn() == false && paid == 3 && flow.GetPending() is null,
            "Native failure is consumed once without an automatic retry");
        quote = original! with { Fare = 0 }; succeeds = true;
        Check(flow.OnReturn() == true && paid == 4, "A free destination remains a valid Teleport");
        var reads = 0;
        var racing = new ReturnFlow(() => false, () => ++reads == 1 ? original : null, () => true,
            _ => throw new Exception("Stale quote was spent"), () => false, () => now) { Enabled = true };
        Check(racing.OnReturn() == false, "Immediate mode revalidates before spending");
        return checks;
    }
}
