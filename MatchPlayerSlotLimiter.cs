namespace MatchZy;

internal readonly record struct MatchPlayerSlot(int UserId, bool IsAutomated, bool IsReady = false);

internal static class MatchPlayerSlotLimiter
{
    internal static IReadOnlyList<int> GetAutomatedPlayerIdsToRemove(IEnumerable<MatchPlayerSlot> playerSlots, int maxPlayers)
    {
        if (maxPlayers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPlayers), "Maximum players cannot be negative.");
        }

        List<MatchPlayerSlot> trackedPlayers = playerSlots.ToList();
        int currentAutomatedPlayers = trackedPlayers.Count(player => player.IsAutomated);
        int excessAutomatedPlayers = currentAutomatedPlayers - GetDesiredAutomatedPlayerCount(trackedPlayers, maxPlayers);
        if (excessAutomatedPlayers <= 0)
        {
            return Array.Empty<int>();
        }

        return trackedPlayers
            .Where(player => player.IsAutomated)
            .OrderByDescending(player => player.UserId)
            .Take(excessAutomatedPlayers)
            .Select(player => player.UserId)
            .ToArray();
    }

    internal static int GetReadyPlayerCount(IEnumerable<MatchPlayerSlot> playerSlots)
    {
        return playerSlots.Count(player => player.IsAutomated || player.IsReady);
    }

    internal static int GetHumanPlayerCount(IEnumerable<MatchPlayerSlot> playerSlots)
    {
        return playerSlots.Count(player => !player.IsAutomated);
    }

    internal static int GetReadyHumanPlayerCount(IEnumerable<MatchPlayerSlot> playerSlots)
    {
        return playerSlots.Count(player => !player.IsAutomated && player.IsReady);
    }

    internal static int GetDesiredAutomatedPlayerCount(IEnumerable<MatchPlayerSlot> playerSlots, int maxPlayers)
    {
        if (maxPlayers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPlayers), "Maximum players cannot be negative.");
        }

        int humanPlayers = playerSlots.Count(player => !player.IsAutomated);
        return Math.Max(0, maxPlayers - humanPlayers);
    }
}
