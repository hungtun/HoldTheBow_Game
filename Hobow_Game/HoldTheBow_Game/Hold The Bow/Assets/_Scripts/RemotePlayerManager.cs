using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Responses;

public class RemotePlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private Transform remoteContainer;

    private HubConnection connection;
    private int localHeroId;

    private readonly Dictionary<int, RemotePlayerController> remotePlayers
        = new Dictionary<int, RemotePlayerController>();

    public void Attach(HubConnection sharedConnection, int localId)
    {
        if (connection != null)
        {
            SafeRemove("HeroMoved");
            SafeRemove("HeroSpawned");
        }

        connection = sharedConnection;
        localHeroId = localId;

        connection.On<HeroMoveResponse>("HeroMoved", OnRemotePlayerMoved);
        connection.On<HeroSpawnResponse>("HeroSpawned", OnRemotePlayerSpawned);
        connection.On<int>("PlayerLoggedOut", OnRemotePlayerLoggedOut);
        connection.On<int>("HeroRemoved", OnRemoteHeroRemoved);
        connection.On<BowStateResponse>("BowStateUpdated", OnBowStateUpdated);

        _ = RequestCurrentPlayersSafe();
    }

    private void SafeRemove(string methodName)
    {
        if (connection == null || connection.State != HubConnectionState.Connected)
        {
            return;
        }
        try
        {
            connection.Remove(methodName);
        }
        catch (System.ObjectDisposedException)
        {
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemotePlayerManager] Remove handler '{methodName}' failed: {ex.Message}");
        }
    }

    private async Task RequestCurrentPlayersSafe()
    {
        if (connection?.State != HubConnectionState.Connected) return;
        try
        {
            await connection.InvokeAsync("RequestCurrentPlayers");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemotePlayerManager] Request current players failed: {ex.Message}");
        }
    }

    private void OnRemotePlayerMoved(HeroMoveResponse response)
    {
        if (response.HeroId == localHeroId) return;

        if (!remotePlayers.TryGetValue(response.HeroId, out var remotePlayer))
        {
            remotePlayer = CreateRemotePlayer(response.HeroId, new Vector3(response.X, response.Y, 0f));
            if (remotePlayer == null) return;
        }

        Vector3 newPosition = new Vector3(response.X, response.Y, 0f);
        Vector3 oldPosition = remotePlayer.transform.position;
        
        remotePlayer.UpdatePosition(newPosition);
        

    }

    private void OnRemotePlayerSpawned(HeroSpawnResponse response)
    {
        if (response.HeroId == localHeroId) return;

        if (remotePlayers.ContainsKey(response.HeroId)) return; 

        CreateRemotePlayer(response.HeroId, new Vector3(response.X, response.Y, 0f));
    }

    private void OnRemotePlayerLoggedOut(int heroId)
    {
        RemoveRemotePlayer(heroId);
    }

    private void OnBowStateUpdated(BowStateResponse response)
    {
        if (response.HeroId == localHeroId) return;
        if (!remotePlayers.TryGetValue(response.HeroId, out var remotePlayer)) return;

        var remoteBow = remotePlayer.GetComponentInChildren<RemotePlayerBowController>(true);
        if (remoteBow != null)
        {
            remoteBow.ApplyBowState(response.AngleDeg, response.IsCharging, response.ChargePercent);
        }
    }

    private void OnRemoteHeroRemoved(int removedHeroId)
    {
        try
        {
            if (removedHeroId == localHeroId) return;
            RemoveRemotePlayer(removedHeroId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RemotePlayerManager] OnRemoteHeroRemoved failed: {ex.Message}");
        }
    }

    private RemotePlayerController CreateRemotePlayer(int playerId, Vector3 position)
    {
        try
        {
            if (remotePlayerPrefab == null)
            {
                Debug.LogError("[RemotePlayerManager] remotePlayerPrefab is null");
                return null;
            }

            GameObject parent = remoteContainer ? remoteContainer.gameObject : null;
            GameObject remotePlayerObj = Instantiate(remotePlayerPrefab, position, Quaternion.identity,
                                                     remoteContainer ? remoteContainer : null);

            if (remotePlayerObj == null)
            {
                Debug.LogError("[RemotePlayerManager] Failed to instantiate remotePlayerPrefab");
                return null;
            }

            var remotePlayer = remotePlayerObj.GetComponent<RemotePlayerController>();
            if (remotePlayer == null)
            {
                Debug.LogError("[RemotePlayerManager] RemotePlayerController component not found on prefab");
                if (remotePlayerObj != null)
                {
                    Destroy(remotePlayerObj);
                }
                return null;
            }

            remotePlayer.Initialize(playerId);
            remotePlayers[playerId] = remotePlayer;

            return remotePlayer;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RemotePlayerManager] CreateRemotePlayer failed: {ex.Message}");
            return null;
        }
    }

    public void RemoveRemotePlayer(int playerId)
    {
        try
        {
            if (remotePlayers.TryGetValue(playerId, out var remotePlayer))
            {
                if (remotePlayer != null)
                {
                    remotePlayer.Dispose();
                }
                remotePlayers.Remove(playerId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RemotePlayerManager] RemoveRemotePlayer failed: {ex.Message}");
        }
    }

    public void RemoveAllRemotePlayers()
    {
        try
        {
            foreach (var kvp in remotePlayers)
            {
                if (kvp.Value != null) 
                {
                    kvp.Value.Dispose();
                }
            }
            remotePlayers.Clear();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RemotePlayerManager] RemoveAllRemotePlayers failed: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        SafeRemove("HeroMoved");
        SafeRemove("HeroSpawned");
        SafeRemove("PlayerLoggedOut");
        SafeRemove("HeroRemoved");
        SafeRemove("BowStateUpdated");
        RemoveAllRemotePlayers();
    }
}
