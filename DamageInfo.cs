using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;


namespace MatchZy
{

    public partial class MatchZy
    {

        private void InitPlayerDamageInfo()
        {
            playerDamageInfo.Clear();
            playerRoundHealth.Clear();

            foreach (var key in playerData.Keys) {
                CCSPlayerController player = playerData[key];
                if (!IsPlayerValid(player)) continue;

                int attackerId = key;
                playerRoundHealth[attackerId] = ClampPlayerHealth(player.PlayerPawn.Value!.Health);

                foreach (var key2 in playerData.Keys) {
                    if (key == key2) continue;
                    if (!playerData[key2].IsValid) continue;
                    if (playerData[key].TeamNum == playerData[key2].TeamNum) continue;
                    if (playerData[key].TeamNum == 2) {
                        if (playerData[key2].TeamNum != 3) continue;
                        int targetId = key2;
                        if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                            playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

                        if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                            attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();
                    } else if (playerData[key].TeamNum == 3) {
                        if (playerData[key2].TeamNum != 2) continue;
                        int targetId = key2;
                        if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                            playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

                        if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                            attackerInfo[targetId] = targetInfo = new DamagePlayerInfo(); 
                    }
                }
            }
        }

		public Dictionary<int, Dictionary<int, DamagePlayerInfo>> playerDamageInfo = new Dictionary<int, Dictionary<int, DamagePlayerInfo>>();
        private const int MaxPlayerHealth = 100;
        private readonly Dictionary<int, int> playerRoundHealth = new();
		private void UpdatePlayerDamageInfo(EventPlayerHurt @event, int targetId)
		{
            CCSPlayerController? attacker = @event.Attacker;

            if (!IsPlayerValid(attacker)) return;
			int attackerId = (int)attacker!.UserId!;
			if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
				playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

			if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
				attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();

            int reportedDamage = Math.Max(0, @event.DmgHealth);
            int postDamageHealth = ClampPlayerHealth(@event.Health);
            if (!playerRoundHealth.TryGetValue(targetId, out var previousHealth))
            {
                previousHealth = Math.Min(MaxPlayerHealth, postDamageHealth + reportedDamage);
            }

            int actualDamage = Math.Min(Math.Max(0, ClampPlayerHealth(previousHealth) - postDamageHealth), reportedDamage);
            targetInfo.DamageHP += actualDamage;
            playerRoundHealth[targetId] = postDamageHealth;
			targetInfo.Hits++;
		}

        private void ShowDamageInfo()
        {
            try
            {
                if (!enableDamageReport.Value) return;

                HashSet<(int, int)> processedPairs = new HashSet<(int, int)>();

                foreach (var entry in playerDamageInfo)
                {
                    int attackerId = entry.Key;
                    foreach (var (targetId, targetEntry) in entry.Value)
                    {
                        if (processedPairs.Contains((attackerId, targetId)) || processedPairs.Contains((targetId, attackerId)))
                            continue;

                        // Access and use the damage information as needed.
                        int damageGiven = targetEntry.DamageHP;
                        int hitsGiven = targetEntry.Hits;
                        int damageTaken = 0;
                        int hitsTaken = 0;

                        if (playerDamageInfo.TryGetValue(targetId, out var targetInfo) && targetInfo.TryGetValue(attackerId, out var takenInfo))
                        {
                            damageTaken = takenInfo.DamageHP;
                            hitsTaken = takenInfo.Hits;
                        }

                        if (!playerData.ContainsKey(attackerId) || !playerData.ContainsKey(targetId)) continue;

                        var attackerController = playerData[attackerId];
                        var targetController = playerData[targetId];

                        if (attackerController != null && targetController != null)
                        {
                            if (!attackerController.IsValid || !targetController.IsValid) continue;
                            if (attackerController.Connected != PlayerConnectedState.PlayerConnected && !IsAutomatedMatchPlayer(attackerController)) continue;
                            if (targetController.Connected != PlayerConnectedState.PlayerConnected && !IsAutomatedMatchPlayer(targetController)) continue;
                            if (!attackerController.PlayerPawn.IsValid || !targetController.PlayerPawn.IsValid) continue;
                            if (attackerController.PlayerPawn.Value == null || targetController.PlayerPawn.Value == null) continue;

                            int attackerHP = attackerController.PlayerPawn.Value.Health < 0 ? 0 : attackerController.PlayerPawn.Value.Health;
                            string attackerName = attackerController.PlayerName;

                            int targetHP = targetController.PlayerPawn.Value.Health < 0 ? 0 : targetController.PlayerPawn.Value.Health;
                            string targetName = targetController.PlayerName;

                            PrintToPlayerChat(attackerController, $"{ChatColors.Green}To: [{damageGiven} / {hitsGiven} hits] From: [{damageTaken} / {hitsTaken} hits] - {targetName} - ({targetHP} hp){ChatColors.Default}");
                            PrintToPlayerChat(targetController, $"{ChatColors.Green}To: [{damageTaken} / {hitsTaken} hits] From: [{damageGiven} / {hitsGiven} hits] - {attackerName} - ({attackerHP} hp){ChatColors.Default}");
                        }

                        // Mark this pair as processed to avoid duplicates.
                        processedPairs.Add((attackerId, targetId));
                    }
                }
            }
            catch (Exception e)
            {
                Log($"[ShowDamageInfo FATAL] An error occurred: {e.Message}");
            }
        }

        private static int ClampPlayerHealth(int health)
        {
            return Math.Clamp(health, 0, MaxPlayerHealth);
        }
    }

	public class DamagePlayerInfo
	{
		public int DamageHP { get; set; } = 0;
		public int Hits { get; set; } = 0;
	}
}
