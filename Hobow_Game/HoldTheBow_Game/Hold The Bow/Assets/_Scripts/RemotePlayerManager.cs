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
    public GameObject arrowPrefab;

    private HubConnection connection;
    
    private System.Collections.Generic.Queue<ArrowSpawnedEvent> arrowSpawnedQueue = new System.Collections.Generic.Queue<ArrowSpawnedEvent>();
    private System.Collections.Generic.Queue<ArrowHitEvent> arrowHitQueue = new System.Collections.Generic.Queue<ArrowHitEvent>();
    private Queue<ArrowRemovedEvent> arrowRemovedQueue = new System.Collections.Generic.Queue<ArrowRemovedEvent>();
    private Queue<HeroDamagedEvent> heroDamagedQueue = new System.Collections.Generic.Queue<HeroDamagedEvent>();
    private int localHeroId;

    private readonly Dictionary<int, RemotePlayerController> remotePlayers
        = new Dictionary<int, RemotePlayerController>();

    void Update()
    {
        ProcessArrowEvents();
        ProcessHeroDamagedEvents();
    }
    
    private void ProcessArrowEvents()
    {
        while (arrowSpawnedQueue.Count > 0)
        {
            var spawnedEvent = arrowSpawnedQueue.Dequeue();
            ProcessArrowSpawned(spawnedEvent);
        }
        
        while (arrowHitQueue.Count > 0)
        {
            var hitEvent = arrowHitQueue.Dequeue();
            ProcessArrowHit(hitEvent);
        }
        
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
        connection.On<HeroDamagedEvent>("HeroDamaged", OnHeroDamaged);

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

        var remotePlayer = CreateRemotePlayer(response.HeroId, new Vector3(response.X, response.Y, 0f));
        if(remotePlayer != null)
        {
            int maxHp = response.MaxHealth > 0 ? response.MaxHealth : 100;
            int CurrentHp = response.CurrentHealth > 0 ? response.CurrentHealth : maxHp;
            remotePlayer.Initialize(response.HeroId, maxHp, CurrentHp);
        }
    }

    private void OnRemotePlayerLoggedOut(int heroId)
    {
        RemoveRemotePlayer(heroId);
    }

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
                return null;
            }

            GameObject parent = remoteContainer ? remoteContainer.gameObject : null;
            GameObject remotePlayerObj = Instantiate(remotePlayerPrefab, position, Quaternion.identity,
                                                     remoteContainer ? remoteContainer : null);

            if (remotePlayerObj == null)
            {
                return null;
            }

            var remotePlayer = remotePlayerObj.GetComponent<RemotePlayerController>();
            if (remotePlayer == null)
            {
                if (remotePlayerObj != null)
                {
                    Destroy(remotePlayerObj);
                }
                return null;
            }

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


    private void OnArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        Debug.Log($"[RemotePlayerManager] OnArrowSpawned received - ArrowId: {spawnedEvent.ArrowId}, HeroId: {spawnedEvent.HeroId}");
        
        arrowSpawnedQueue.Enqueue(spawnedEvent);
    }
    
    private void ProcessArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        if (arrowPrefab != null)
        {
            var arrowObj = Instantiate(arrowPrefab, new Vector3(spawnedEvent.StartX, spawnedEvent.StartY, 0f), Quaternion.identity);

            arrowObj.name = $"Arrow_{spawnedEvent.ArrowId}";
            
            var arrowController = arrowObj.GetComponent<ArrowController>();
            if (arrowController != null)
            {
                arrowController.Initialize(spawnedEvent);
            }
        }
        else
        {
            Debug.LogWarning("[RemotePlayerManager] Arrow prefab is not assigned!");
        }
    }


    private void OnArrowHit(ArrowHitEvent hitEvent)
    {
        Debug.Log($"[RemotePlayerManager] OnArrowHit received - ArrowId: {hitEvent.ArrowId}");
        
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

    private void OnArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        Debug.Log($"[RemotePlayerManager] OnArrowRemoved received - ArrowId: {removedEvent.ArrowId}");
        
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

  private void OnHeroDamaged(HeroDamagedEvent e)
  {
    heroDamagedQueue.Enqueue(e);
  }

  private void ProcessHeroDamagedEvents()
  {
    while (heroDamagedQueue.Count > 0)
    {
      var e = heroDamagedQueue.Dequeue();
      if (!remotePlayers.TryGetValue(e.HeroId, out var remote)) continue;

      var hb = remote.GetComponentInChildren<HealthBar>(true);
      if (hb != null)
      {
        hb.SetHealth(Mathf.Max(0, e.NewHealth));
      }

      var sr = remote.GetComponent<SpriteRenderer>();
      if (sr != null)
      {
        StartCoroutine(FlashRemote(sr));
      }
    }
  }

  private System.Collections.IEnumerator FlashRemote(SpriteRenderer sr)
  {
    var original = sr.color;
    sr.color = Color.red;
    yield return new WaitForSeconds(0.2f);
    sr.color = original;
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
