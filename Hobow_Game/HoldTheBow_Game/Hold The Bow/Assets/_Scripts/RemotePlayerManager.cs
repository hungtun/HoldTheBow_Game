using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Responses;
using SharedLibrary.Events;

public class RemotePlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private Transform remoteContainer;
    
    [Header("Arrow Settings")]
    public GameObject arrowPrefab; // Gán Arrow prefab trong Inspector

    private HubConnection connection;
    
    // Arrow event queues for main thread processing
    private System.Collections.Generic.Queue<ArrowSpawnedEvent> arrowSpawnedQueue = new System.Collections.Generic.Queue<ArrowSpawnedEvent>();
    private System.Collections.Generic.Queue<ArrowHitEvent> arrowHitQueue = new System.Collections.Generic.Queue<ArrowHitEvent>();
    private System.Collections.Generic.Queue<ArrowRemovedEvent> arrowRemovedQueue = new System.Collections.Generic.Queue<ArrowRemovedEvent>();
    private int localHeroId;

    private readonly Dictionary<int, RemotePlayerController> remotePlayers
        = new Dictionary<int, RemotePlayerController>();

    void Update()
    {
        ProcessArrowEvents();
    }
    
    private void ProcessArrowEvents()
    {
        // Process arrow spawned events
        while (arrowSpawnedQueue.Count > 0)
        {
            var spawnedEvent = arrowSpawnedQueue.Dequeue();
            ProcessArrowSpawned(spawnedEvent);
        }
        
        // Process arrow hit events
        while (arrowHitQueue.Count > 0)
        {
            var hitEvent = arrowHitQueue.Dequeue();
            ProcessArrowHit(hitEvent);
        }
        
        // Process arrow removed events
        while (arrowRemovedQueue.Count > 0)
        {
            var removedEvent = arrowRemovedQueue.Dequeue();
            ProcessArrowRemoved(removedEvent);
        }
    }

    public void Attach(HubConnection sharedConnection, int localId)
    {
        if (connection != null)
        {
            SafeRemove("HeroMoveUpdate");
            SafeRemove("HeroSpawned");
        }

        connection = sharedConnection;
        localHeroId = localId;

        connection.On<HeroMoveUpdateEvent>("HeroMoveUpdate", OnRemotePlayerMoveUpdate);
        connection.On<HeroSpawnResponse>("HeroSpawned", OnRemotePlayerSpawned);
        connection.On<int>("PlayerLoggedOut", OnRemotePlayerLoggedOut);
        connection.On<int>("HeroRemoved", OnRemoteHeroRemoved);
        connection.On<BowStateUpdateEvent>("BowStateUpdate", OnBowStateUpdate);
        connection.On<ArrowSpawnedEvent>("ArrowSpawned", OnArrowSpawned);
        connection.On<ArrowHitEvent>("ArrowHit", OnArrowHit);
        connection.On<ArrowRemovedEvent>("ArrowRemoved", OnArrowRemoved);

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


    /// <summary>
    /// Handle hero movement update event for remote players (new event-based approach)
    /// </summary>
    private void OnRemotePlayerMoveUpdate(HeroMoveUpdateEvent updateEvent)
    {
        if (updateEvent.HeroId == localHeroId) return;

        if (!remotePlayers.TryGetValue(updateEvent.HeroId, out var remotePlayer))
        {
            remotePlayer = CreateRemotePlayer(updateEvent.HeroId, new Vector3(updateEvent.X, updateEvent.Y, 0f));
            if (remotePlayer == null) return;
        }

        Vector3 newPosition = new Vector3(updateEvent.X, updateEvent.Y, 0f);
        remotePlayer.UpdatePosition(newPosition);
        
        // Update animation based on movement state
        if (remotePlayer.animator != null)
        {
            remotePlayer.animator.SetBool("isMoving", updateEvent.IsMoving);
            if (updateEvent.IsMoving && !string.IsNullOrEmpty(updateEvent.Direction))
            {
                remotePlayer.animator.SetFloat("moveX", updateEvent.Direction == "left" ? -1f : updateEvent.Direction == "right" ? 1f : 0f);
                remotePlayer.animator.SetFloat("moveY", updateEvent.Direction == "up" ? 1f : updateEvent.Direction == "down" ? -1f : 0f);
            }
        }
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

    /// <summary>
    /// Handle bow state update event for remote players (new event-based approach)
    /// </summary>
    private void OnBowStateUpdate(BowStateUpdateEvent updateEvent)
    {
        if (updateEvent.HeroId == localHeroId) return;
        if (!remotePlayers.TryGetValue(updateEvent.HeroId, out var remotePlayer)) return;

        var remoteBow = remotePlayer.GetComponentInChildren<RemotePlayerBowController>(true);
        if (remoteBow != null)
        {
            remoteBow.ApplyBowState(updateEvent.AngleDeg, updateEvent.IsCharging, updateEvent.ChargePercent);
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

    /// <summary>
    /// Handle arrow spawned event
    /// </summary>
    private void OnArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        Debug.Log($"[RemotePlayerManager] OnArrowSpawned received - ArrowId: {spawnedEvent.ArrowId}, HeroId: {spawnedEvent.HeroId}");
        
        // Enqueue for main thread processing
        arrowSpawnedQueue.Enqueue(spawnedEvent);
    }
    
    private void ProcessArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        if (arrowPrefab != null)
        {
            // Calculate rotation based on arrow direction
            Vector2 direction = new Vector2(spawnedEvent.DirectionX, spawnedEvent.DirectionY);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; // Subtract 90 degrees to correct orientation
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            var arrowObj = Instantiate(arrowPrefab, new Vector3(spawnedEvent.StartX, spawnedEvent.StartY, 0f), rotation);
            arrowObj.name = $"Arrow_{spawnedEvent.ArrowId}";
            
            var arrowController = arrowObj.GetComponent<ArrowController>();
            if (arrowController != null)
            {
                arrowController.Initialize(spawnedEvent);
                Debug.Log($"[RemotePlayerManager] Arrow {spawnedEvent.ArrowId} created and initialized with rotation: {angle}°");
            }
        }
        else
        {
            Debug.LogWarning("[RemotePlayerManager] Arrow prefab is not assigned!");
        }
    }

    /// <summary>
    /// Handle arrow hit event
    /// </summary>
    private void OnArrowHit(ArrowHitEvent hitEvent)
    {
        Debug.Log($"[RemotePlayerManager] OnArrowHit received - ArrowId: {hitEvent.ArrowId}");
        
        // Enqueue for main thread processing
        arrowHitQueue.Enqueue(hitEvent);
    }
    
    private void ProcessArrowHit(ArrowHitEvent hitEvent)
    {
        var arrowObj = GameObject.Find($"Arrow_{hitEvent.ArrowId}");
        if (arrowObj != null)
        {
            var arrowController = arrowObj.GetComponent<ArrowController>();
            if (arrowController != null)
            {
                arrowController.HandleHit(hitEvent);
                Debug.Log($"[RemotePlayerManager] Arrow {hitEvent.ArrowId} hit handled");
            }
        }
    }

    /// <summary>
    /// Handle arrow removed event
    /// </summary>
    private void OnArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        Debug.Log($"[RemotePlayerManager] OnArrowRemoved received - ArrowId: {removedEvent.ArrowId}");
        
        // Enqueue for main thread processing
        arrowRemovedQueue.Enqueue(removedEvent);
    }
    
    private void ProcessArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        var arrowObj = GameObject.Find($"Arrow_{removedEvent.ArrowId}");
        if (arrowObj != null)
        {
            Destroy(arrowObj);
            Debug.Log($"[RemotePlayerManager] Arrow {removedEvent.ArrowId} destroyed");
        }
    }

    private void OnDestroy()
    {
        SafeRemove("HeroMoved");
        SafeRemove("HeroSpawned");
        SafeRemove("PlayerLoggedOut");
        SafeRemove("HeroRemoved");
        SafeRemove("BowStateUpdated");
        SafeRemove("ArrowSpawned");
        SafeRemove("ArrowHit");
        SafeRemove("ArrowRemoved");
        RemoveAllRemotePlayers();
    }
}
