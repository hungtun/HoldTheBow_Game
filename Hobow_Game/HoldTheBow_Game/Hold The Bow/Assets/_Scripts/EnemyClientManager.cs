using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Responses;
using SharedLibrary.Events;
using System.Collections.Generic;
using System.Threading.Tasks;

public class EnemyClientManager : MonoBehaviour
{
    public GameObject enemyPrefab;

    private HubConnection _connection;
    private Dictionary<int, GameObject> activeEnemies = new Dictionary<int, GameObject>();

    public async void Initialize(string baseUrl, string jwtToken)
    {
        ClearAllEnemies();
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/enemy", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(jwtToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<EnemySpawnResponse>("EnemySpawned", OnEnemySpawned);
        _connection.On<EnemyMoveUpdateEvent>("EnemyMoveUpdate", OnEnemyMoveUpdate);
        _connection.On<int>("EnemyRemoved", OnEnemyRemoved);
        _connection.On<ArrowHitEvent>("ArrowHit", OnEnemyHit);     
        await _connection.StartAsync();
    }

    private void OnEnemySpawned(EnemySpawnResponse resp)
    {
        if (resp.MapId != "Home") return;

        if (enemyPrefab == null) return;

        if (activeEnemies.ContainsKey(resp.EnemyId))
        {
            return;
        }
        
        var obj = Instantiate(enemyPrefab, new Vector3(resp.X, resp.Y, 0f), Quaternion.identity);
        obj.name = $"Enemy_{resp.EnemyId}_{resp.EnemyName}";

        // Ensure EnemyController is attached
        var enemyController = obj.GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = obj.AddComponent<EnemyController>();
            Debug.Log($"[EnemyClientManager] Added EnemyController to enemy {resp.EnemyId}");
        }

        activeEnemies[resp.EnemyId] = obj;
    }

    /// <summary>
    /// Handle enemy movement update event (new event-based approach)
    /// </summary>
    private void OnEnemyMoveUpdate(EnemyMoveUpdateEvent updateEvent)
    {
        if (updateEvent.MapId != "Home") return;

        if (!activeEnemies.TryGetValue(updateEvent.EnemyId, out var enemyObj))
        {
            // If enemy doesn't exist, create it
            if (enemyPrefab != null)
            {
                enemyObj = Instantiate(enemyPrefab, new Vector3(updateEvent.X, updateEvent.Y, 0f), Quaternion.identity);
                enemyObj.name = $"Enemy_{updateEvent.EnemyId}_{updateEvent.EnemyName}";
                activeEnemies[updateEvent.EnemyId] = enemyObj;
            }
            return;
        }

        // Update enemy position
        var logEnemyController = enemyObj.GetComponent<LogEnemyController>();
        if (logEnemyController != null)
        {
            logEnemyController.SetTargetPosition(updateEvent.X, updateEvent.Y);
        }
    }

    private void ClearAllEnemies()
    {
        try
        {
            var existingEnemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in existingEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy);
                }
            }

            activeEnemies.Clear();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnemyClientManager] ClearAllEnemies failed: {ex.Message}");
        }
    }


    private void OnEnemyRemoved(int enemyId)
    {
        try
        {
            if (activeEnemies.ContainsKey(enemyId))
            {
                var enemyObj = activeEnemies[enemyId];
                if (enemyObj != null)
                {
                    var enemyController = enemyObj.GetComponent<EnemyController>();
                    if (enemyController != null)
                    {
                        // Play death effect before destroying
                        enemyController.Die();
                    }
                    else
                    {
                        Destroy(enemyObj);
                    }
                }
                activeEnemies.Remove(enemyId);
                Debug.Log($"[EnemyClientManager] Enemy {enemyId} died and removed");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnemyClientManager] OnEnemyRemoved failed: {ex.Message}");
        }
    }

    private void OnEnemyHit(ArrowHitEvent hitEvent)
    {
        Debug.Log($"[EnemyClientManager] OnEnemyHit received - HitType: {hitEvent.HitType}, TargetId: {hitEvent.TargetId}, RemainingHealth: {hitEvent.RemainingHealth}");
        
        if (hitEvent.HitType == "Enemy" && hitEvent.TargetId.HasValue)
        {
            int enemyId = hitEvent.TargetId.Value;
            
            if (activeEnemies.ContainsKey(enemyId))
            {
                var enemyObj = activeEnemies[enemyId];
                var enemyController = enemyObj.GetComponent<EnemyController>();
                
                Debug.Log($"[EnemyClientManager] Enemy {enemyId} found, EnemyController: {enemyController != null}");
                
                if (enemyController != null)
                {
                    // Trigger red flash effect
                    enemyController.FlashRed();
                    
                    Debug.Log($"[EnemyClientManager] Enemy {enemyId} hit! Remaining health: {hitEvent.RemainingHealth}");
                }
                else
                {
                    Debug.LogError($"[EnemyClientManager] EnemyController not found on enemy {enemyId}, adding one now...");
                    enemyController = enemyObj.AddComponent<EnemyController>();
                    enemyController.FlashRed();
                }
            }
            else
            {
                Debug.LogWarning($"[EnemyClientManager] Enemy {enemyId} not found in activeEnemies");
            }
        }
    }

    private void OnDestroy()
    {
        _connection?.DisposeAsync();
    }
}

public class EnemyDamageData
{
    public int EnemyId { get; set; }
    public int Damage { get; set; }
    public int NewHealth { get; set; }
}
