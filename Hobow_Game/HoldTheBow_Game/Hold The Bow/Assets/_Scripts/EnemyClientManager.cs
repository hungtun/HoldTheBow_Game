using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Responses;
using SharedLibrary.Events;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;

public class EnemyClientManager : MonoBehaviour
{
    public GameObject enemyPrefab;

    private HubConnection _connection;
    private Dictionary<int, GameObject> activeEnemies = new Dictionary<int, GameObject>();
    private HashSet<int> reportedHitbox = new HashSet<int>();

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

        var enemyController = obj.GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = obj.AddComponent<EnemyController>();
        }

        var healthBar = obj.GetComponentInChildren<HealthBar>(true);
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(resp.Health > 0 ? resp.Health : 100);
            healthBar.SetHealth(resp.Health > 0 ? resp.Health : 100);
        }
        StartCoroutine(SendEnemyHitboxNextFrame(resp.EnemyId, obj));


        activeEnemies[resp.EnemyId] = obj;
    }

    private IEnumerator SendEnemyHitboxNextFrame(int enemyId, GameObject obj)
    {
        yield return null; 
        if (reportedHitbox.Contains(enemyId)) yield break;

        var box = obj.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            var b = box.bounds;
            Vector2 centerOffset = (Vector2)(b.center - obj.transform.position);
            Vector2 half = (Vector2)b.extents;

            var task = _connection.InvokeAsync("ReportEnemyHitbox", new EnemyHitboxReportEvent
            {
                EnemyId = enemyId,
                CenterOffsetX = centerOffset.x,
                CenterOffsetY = centerOffset.y,
                HalfSizeX = half.x,
                HalfSizeY = half.y
            });
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                Debug.LogWarning($"[EnemyClientManager] ReportEnemyHitbox failed for {enemyId}: {task.Exception?.GetBaseException().Message}");
                yield break;
            }
            reportedHitbox.Add(enemyId);
        }
    }

 
    private void OnEnemyMoveUpdate(EnemyMoveUpdateEvent updateEvent)
    {
        if (updateEvent.MapId != "Home") return;

        if (!activeEnemies.TryGetValue(updateEvent.EnemyId, out var enemyObj))
        {
            if (enemyPrefab != null)
            {
                enemyObj = Instantiate(enemyPrefab, new Vector3(updateEvent.X, updateEvent.Y, 0f), Quaternion.identity);
                enemyObj.name = $"Enemy_{updateEvent.EnemyId}_{updateEvent.EnemyName}";
                activeEnemies[updateEvent.EnemyId] = enemyObj;
            }
            return;
        }

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
            Debug.LogError($"ClearAllEnemies failed: {ex.Message}");
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
                        enemyController.Die();
                    }
                    else
                    {
                        Destroy(enemyObj);
                    }
                }
                activeEnemies.Remove(enemyId);
                
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnemyClientManager] OnEnemyRemoved failed: {ex.Message}");
        }
    }

    private void OnEnemyHit(ArrowHitEvent hitEvent)
    {
        

        if (hitEvent.HitType == "Enemy" && hitEvent.TargetId.HasValue)
        {
            int enemyId = hitEvent.TargetId.Value;

            if (activeEnemies.ContainsKey(enemyId))
            {
                var enemyObj = activeEnemies[enemyId];
                var enemyController = enemyObj.GetComponent<EnemyController>();

                

                if (enemyController != null)
                {
                    enemyController.FlashRed();

                    var healthBar = enemyObj.GetComponentInChildren<HealthBar>(true);
                    if (healthBar != null && hitEvent.RemainingHealth.HasValue)
                    {
                        healthBar.SetHealth(hitEvent.RemainingHealth.Value);
                    }

                    
                }
                else
                {
                    Debug.LogError($"EnemyController not found on enemy {enemyId}, adding one now...");
                    enemyController = enemyObj.AddComponent<EnemyController>();
                    enemyController.FlashRed();
                    var healthBar = enemyObj.GetComponentInChildren<HealthBar>(true);
                    if (healthBar != null && hitEvent.RemainingHealth.HasValue)
                    {
                        healthBar.SetHealth(hitEvent.RemainingHealth.Value);
                    }
                }
            }
            else
            {
                
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
