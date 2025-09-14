using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Responses;
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
        _connection.On<object>("EnemyPositionUpdated", OnEnemyPositionUpdated);
        _connection.On<int>("EnemyRemoved", OnEnemyRemoved);
        // _connection.On<object>("EnemyDamaged", OnEnemyDamaged);     
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

        activeEnemies[resp.EnemyId] = obj;
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

    private void OnEnemyPositionUpdated(object data)
    {
        var json = data.ToString();
        var positionData = Newtonsoft.Json.JsonConvert.DeserializeObject<EnemyPositionData>(json);
        
        if (!activeEnemies.TryGetValue(positionData.EnemyId, out var enemyObj))
        {
            Debug.LogWarning($"[EnemyClientManager] Enemy {positionData.EnemyId} not found for position update");
            return;
        }
        
        var logEnemyController = enemyObj.GetComponent<LogEnemyController>();
        if (logEnemyController != null)
        {
            logEnemyController.SetTargetPosition(positionData.X, positionData.Y);
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
                    Destroy(enemyObj);
                }
                activeEnemies.Remove(enemyId);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnemyClientManager] OnEnemyRemoved failed: {ex.Message}");
        }
    }

    // private void OnEnemyDamaged(object data)
    // {
    //     var json = data.ToString();
    //     var damageData = Newtonsoft.Json.JsonConvert.DeserializeObject<EnemyDamageData>(json);

    //     int enemyId = damageData.EnemyId;
    //     int damage = damageData.Damage;
    //     int newHealth = damageData.NewHealth;

    //     if (activeEnemies.ContainsKey(enemyId))
    //     {
    //         var enemyObj = activeEnemies[enemyId];
    //     }
    // }

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

public class EnemyPositionData
{
    public int EnemyId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}