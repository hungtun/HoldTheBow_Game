using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Generic;
using SharedLibrary.Requests;
using SharedLibrary.Responses;
using SharedLibrary.DataModels;

public class MapDataManager : MonoBehaviour
{
    [SerializeField] private string defaultMapId = "Home";
    [SerializeField] private GameObject collisionObjectPrefab;
    [SerializeField] private string collisionLayerName = "MapObjects";
    [SerializeField] private MapData currentMapData;

    private HubConnection connection;
    private int heroId;
    private List<GameObject> createdCollisionObjects = new List<GameObject>();

    public void Initialize(HubConnection hubConnection, int playerHeroId)
    {
        connection = hubConnection;
        heroId = playerHeroId;
        
        connection.On<MapDataResponse>("MapDataReceived", OnMapDataReceived);
        
        RequestMapData(defaultMapId);
    }

    public async void RequestMapData(string mapId)
    {
        if (connection?.State != HubConnectionState.Connected)
        {
            return;
        }
        try
        {
            var request = new MapDataRequest { MapId = mapId };
            await connection.InvokeAsync("RequestMapData", request);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapDataManager] Failed to request map data: {ex.Message}");
        }
    }

    public void OnMapDataReceived(MapDataResponse response)
    {
        if (!response.Success) return;

        currentMapData = response.Data;
        CreateCollisionObjects();
    }

    private List<CollisionObject> GetCollisionObjects()
    {
        var collisionObjects = new List<CollisionObject>();

        if (currentMapData == null) return collisionObjects;

        foreach (var layer in currentMapData.Layers)
        {
            if (layer.Type == "objectgroup" && layer.Name.ToLower().Contains("collision"))
            {
                collisionObjects.AddRange(layer.Objects);
            }
        }

        return collisionObjects;
    }

    private void CreateCollisionObjects()
    {
        ClearCollisionObjects();

        int collisionLayer = LayerMask.NameToLayer(collisionLayerName);
        var collisionObjects = GetCollisionObjects();

        foreach (var obj in collisionObjects)
        {
            CreateCollisionObject(obj, collisionLayer);
        }
    }

    public bool IsPositionBlocked(Vector2 position, float radius = 0.25f)
    {
        var collisionObjects = GetCollisionObjects();

        foreach (var obj in collisionObjects)
        {
            if (IsCircleRectCollision(position, radius, new Vector2(obj.X, obj.Y), obj.Width, obj.Height))
            {
                return true;
            }
        }

        return false;
    }

    private void CreateCollisionObject(CollisionObject obj, int layer)
    {
        GameObject collisionObj;

        if (collisionObjectPrefab != null)
        {
            collisionObj = Instantiate(collisionObjectPrefab);
        }
        else
        {
            collisionObj = new GameObject($"Collision_{obj.X}_{obj.Y}");

            var collider = collisionObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(obj.Width, obj.Height);
            collider.offset = Vector2.zero;

        }

        collisionObj.transform.position = new Vector3(obj.X, obj.Y, 0f);
        if (layer != -1)
        {
            collisionObj.layer = layer;
        }

        createdCollisionObjects.Add(collisionObj);
    }


    public void ClearCollisionObjects()
    {
        // Duyệt bản sao để tránh thay đổi collection khi đang iterate
        var snapshot = new List<GameObject>(createdCollisionObjects);
        foreach (var obj in snapshot)
        {
            if (obj != null)
            {
                // Tránh dùng DestroyImmediate trong runtime vì có thể gây crash trên build
                Destroy(obj);
            }
        }
        createdCollisionObjects.Clear();
    }

    private bool IsCircleRectCollision(Vector2 circlePos, float circleRadius, Vector2 rectPos, float rectWidth, float rectHeight)
    {
        float closestX = Mathf.Max(rectPos.x - rectWidth / 2, Mathf.Min(circlePos.x, rectPos.x + rectWidth / 2));
        float closestY = Mathf.Max(rectPos.y - rectHeight / 2, Mathf.Min(circlePos.y, rectPos.y + rectHeight / 2));

        float distanceX = circlePos.x - closestX;
        float distanceY = circlePos.y - closestY;
        float distanceSquared = distanceX * distanceX + distanceY * distanceY;

        return distanceSquared < circleRadius * circleRadius;
    }

    private void OnDestroy()
    {
        ClearCollisionObjects();
    }
}
