namespace MatchZy;

internal readonly record struct MatchPlayerSlot(int UserId, bool IsAutomated, bool IsReady = false);

internal static class MatchPlayerSlotLimiter
{
    internal static int GetHumanPlayerCount(IEnumerable<MatchPlayerSlot> playerSlots)
    {
        return playerSlots.Count(player => !player.IsAutomated);
    }
}
