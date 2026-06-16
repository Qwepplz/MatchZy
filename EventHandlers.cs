
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;
public partial class MatchZy
{
    public HookResult EventPlayerConnectFullHandler(EventPlayerConnectFull @event, GameEventInfo info)
    {
        try
        {
            CCSPlayerController? player = @event.Userid;

            if (!IsPlayerValid(player)) return HookResult.Continue;
            Log($"[FULL CONNECT] Player ID: {player!.UserId}, Name: {player.PlayerName} has connected!");
            if (!ShouldTrackPlayer(player)) return HookResult.Continue;

            // Handling whitelisted players
            if (!player.IsBot && !player.IsHLTV)
            {
                var steamId = player.SteamID;

                bool kicked = HandlePlayerWhitelist(player, steamId.ToString());
                if (kicked) return HookResult.Continue;

                if (isMatchSetup || matchModeOnly || arePugTeamsLocked)
                {
                    CsTeam team = GetPlayerTeam(player);
                    if (team == CsTeam.None)
                    {
                        Log($"[EventPlayerConnectFull] KICKING PLAYER STEAMID: {steamId}, Name: {player.PlayerName} (NOT ALLOWED!)");
                        PrintToAllChat($"Kicking player {player.PlayerName} - Not a player in this game.");
                        KickPlayer(player);
                        return HookResult.Continue;
                    }
                }
            }

            if (player.UserId.HasValue)
            {
                playerData[player.UserId.Value] = player;
                connectedPlayers++;
                // Ready system removed: every connected player is treated as ready.
                playerReadyStatus[player.UserId.Value] = true;
            }
            // Keep existing first-player warmup behavior before refreshing the full controller map.

            if (readyAvailable && !matchStarted)
            {
                // Start Warmup when first player connect and match is not started.
                if (GetRealPlayersCount() == 1)
                {
                    Log($"[FULL CONNECT] First player has connected, starting warmup!");
                    ExecUnpracCommands();
                    AutoStart();
                }
                UpdatePlayersMap();
                CheckLiveRequired();
            }
            else
            {
                UpdatePlayersMap();
            }
            return HookResult.Continue;

        }
        catch (Exception e)
        {
            Log($"[EventPlayerConnectFull FATAL] An error occurred: {e.Message}");
            return HookResult.Continue;
        }

    }
    public HookResult EventPlayerDisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
    {
        try
        {
            CCSPlayerController? player = @event.Userid;

            if (!IsPlayerValid(player)) return HookResult.Continue;
            if (!player!.UserId.HasValue) return HookResult.Continue;
            int userId = player.UserId.Value;

            if (playerReadyStatus.ContainsKey(userId))
            {
                playerReadyStatus.Remove(userId);
                connectedPlayers--;
            }
            playerData.Remove(userId);

            noFlashList.Remove(userId);
            lastGrenadesData.Remove(userId);
            nadeSpecificLastGrenadeData.Remove(userId);

            if (isPaused && pauseTeamName != "Admin")
            {
                TryUnpauseIfAllHumanPlayersVoted();
            }

            return HookResult.Continue;
        }
        catch (Exception e)
        {
            Log($"[EventPlayerDisconnect FATAL] An error occurred: {e.Message}");
            return HookResult.Continue;
        }
    }

    public HookResult EventCsWinPanelRoundHandler(EventCsWinPanelRound @event, GameEventInfo info)
    {
        // EventCsWinPanelRound has stopped firing after Arms Race update, hence we handle knife round winner in EventRoundEnd.

        // Log($"[EventCsWinPanelRound PRE] finalEvent: {@event.FinalEvent}");
        // if (isKnifeRound && matchStarted)
        // {
        //     HandleKnifeWinner(@event);
        // }
        return HookResult.Continue;
    }

    public HookResult EventCsWinPanelMatchHandler(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        try
        {
            HandleMatchEnd();
            // ResetMatch();
            return HookResult.Continue;
        }
        catch (Exception e)
        {
            Log($"[EventCsWinPanelMatch FATAL] An error occurred: {e.Message}");
            return HookResult.Continue;
        }
    }

    public HookResult EventRoundStartHandler(EventRoundStart @event, GameEventInfo info)
    {
        try
        {
            HandlePostRoundStartEvent(@event);
            return HookResult.Continue;
        }
        catch (Exception e)
        {
            Log($"[EventRoundStart FATAL] An error occurred: {e.Message}");
            return HookResult.Continue;
        }
    }

    public void OnEntitySpawnedHandler(CEntityInstance entity)
    {
        try
        {
            if (!isPractice || entity == null || entity.Entity == null) return;
            if (!Constants.ProjectileTypeMap.ContainsKey(entity.Entity.DesignerName)) return;

            Server.NextFrame(() => {
                CBaseCSGrenadeProjectile projectile = new CBaseCSGrenadeProjectile(entity.Handle);

                if (!projectile.IsValid ||
                    !projectile.Thrower.IsValid ||
                    projectile.Thrower.Value == null ||
                    projectile.Thrower.Value.Controller.Value == null ||
                    projectile.Globalname == "custom"
                ) return;

                CCSPlayerController player = new(projectile.Thrower.Value.Controller.Value.Handle);
                if(!player.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.IsValid) return;
                int client = player.UserId!.Value;
                
                Vector position = new(projectile.AbsOrigin!.X, projectile.AbsOrigin.Y, projectile.AbsOrigin.Z);
                QAngle angle = new(projectile.AbsRotation!.X, projectile.AbsRotation.Y, projectile.AbsRotation.Z);
                Vector velocity = new(projectile.AbsVelocity.X, projectile.AbsVelocity.Y, projectile.AbsVelocity.Z);
                string nadeType = Constants.ProjectileTypeMap[entity.Entity.DesignerName];

                if (!lastGrenadesData.ContainsKey(client)) {
                    lastGrenadesData[client] = new();
                }

                if (!nadeSpecificLastGrenadeData.ContainsKey(client))
                {
                    nadeSpecificLastGrenadeData[client] = new(){};
                }

                GrenadeThrownData lastGrenadeThrown = new(
                    position, 
                    angle, 
                    velocity, 
                    player.PlayerPawn.Value.CBodyComponent!.SceneNode!.AbsOrigin, 
                    player.PlayerPawn.Value.EyeAngles,
                    nadeType,
                    DateTime.Now,
                    projectile.ItemIndex
                );

                nadeSpecificLastGrenadeData[client][nadeType] = lastGrenadeThrown;
                lastGrenadesData[client].Add(lastGrenadeThrown);

                if (maxLastGrenadesSavedLimit != 0 && lastGrenadesData[client].Count > maxLastGrenadesSavedLimit)
                {
                    lastGrenadesData[client].RemoveAt(0);
                }

                lastGrenadeThrownTime[(int)projectile.Index] = DateTime.Now;
                if (smokeColorEnabled.Value && nadeType == "smoke")
                {
                    CSmokeGrenadeProjectile smokeProjectile = new(entity.Handle);
                    smokeProjectile.SmokeColor.X = GetPlayerTeammateColor(player).R;
                    smokeProjectile.SmokeColor.Y = GetPlayerTeammateColor(player).G;
                    smokeProjectile.SmokeColor.Z = GetPlayerTeammateColor(player).B;
                }
            });
        }
        catch (Exception e)
        {
            Log($"[OnEntitySpawnedHandler FATAL] An error occurred: {e.Message}");
        }
    }

    public HookResult EventSmokegrenadeDetonateHandler(EventSmokegrenadeDetonate @event, GameEventInfo info)
    {
        if (!isPractice || isDryRun) return HookResult.Continue;
        CCSPlayerController? player = @event.Userid;
        if (!IsPlayerValid(player)) return HookResult.Continue;
        if(lastGrenadeThrownTime.TryGetValue(@event.Entityid, out var thrownTime)) 
        {
            PrintToPlayerChat(player!, Localizer["matchzy.pracc.smoke", player!.PlayerName, $"{(DateTime.Now - thrownTime).TotalSeconds:0.00}"]);
            lastGrenadeThrownTime.Remove(@event.Entityid);
        }
        return HookResult.Continue;
    }

    public HookResult EventFlashbangDetonateHandler(EventFlashbangDetonate @event, GameEventInfo info)
    {
        if (!isPractice || isDryRun) return HookResult.Continue;
        CCSPlayerController? player = @event.Userid;
        if (!IsPlayerValid(player)) return HookResult.Continue;
        if(lastGrenadeThrownTime.TryGetValue(@event.Entityid, out var thrownTime)) 
        {
            PrintToPlayerChat(player!, Localizer["matchzy.pracc.flash", player!.PlayerName, $"{(DateTime.Now - thrownTime).TotalSeconds:0.00}"]);
            lastGrenadeThrownTime.Remove(@event.Entityid);
        }
        return HookResult.Continue;
    }

    public HookResult EventHegrenadeDetonateHandler(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        if (!isPractice || isDryRun) return HookResult.Continue;
        CCSPlayerController? player = @event.Userid;
        if (!IsPlayerValid(player)) return HookResult.Continue;
        if(lastGrenadeThrownTime.TryGetValue(@event.Entityid, out var thrownTime)) 
        {
            PrintToPlayerChat(player!, Localizer["matchzy.pracc.grenade", player!.PlayerName, $"{(DateTime.Now - thrownTime).TotalSeconds:0.00}"]);
            lastGrenadeThrownTime.Remove(@event.Entityid);
        }
        return HookResult.Continue;
    }

    public HookResult EventMolotovDetonateHandler(EventMolotovDetonate @event, GameEventInfo info)
    {
        if (!isPractice || isDryRun) return HookResult.Continue;
        CCSPlayerController? player = @event.Userid;
        if (!IsPlayerValid(player)) return HookResult.Continue;
        if(lastGrenadeThrownTime.TryGetValue(@event.Get<int>("entityid"), out var thrownTime)) 
        {
            PrintToPlayerChat(player!, Localizer["matchzy.pracc.molotov", player!.PlayerName, $"{(DateTime.Now - thrownTime).TotalSeconds:0.00}"]);
        }
        return HookResult.Continue;
    }

    public HookResult EventDecoyDetonateHandler(EventDecoyStarted @event, GameEventInfo info)
    {
        if (!isPractice || isDryRun) return HookResult.Continue;
        CCSPlayerController? player = @event.Userid;
        if (!IsPlayerValid(player)) return HookResult.Continue;
        if(lastGrenadeThrownTime.TryGetValue(@event.Entityid, out var thrownTime)) 
        {
            PrintToPlayerChat(player!, Localizer["matchzy.pracc.decoy", player!.PlayerName, $"{(DateTime.Now - thrownTime).TotalSeconds:0.00}"]);
            lastGrenadeThrownTime.Remove(@event.Entityid);
        }
        return HookResult.Continue;
    }
}
