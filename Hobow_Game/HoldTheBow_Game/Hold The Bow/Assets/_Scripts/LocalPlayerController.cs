using UnityEngine;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Requests;
using SharedLibrary.Responses;
using SharedLibrary.Events;

public class LocalPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private int sendIntervalMs = 30;
    [SerializeField] private float reconciliationThreshold = 0.02f;
    [SerializeField] private float reconciliationHardSnapThreshold = 0.4f;
    [SerializeField] private float serverLerpFactor = 0.75f;

    private Animator animator;
    private Rigidbody2D rb;
    private RemotePlayerManager remoteManager;
    private LogoutManager logoutManager;
    private MapDataManager mapDataManager;
    private EnemyClientManager enemyClientManager;
    private float heroRadius = 0.25f;
    
    [Header("Arrow Settings")]
    public GameObject arrowPrefab; // Gán Arrow prefab trong Inspector

    public HubConnection connection;
    
    // Arrow event queues for main thread processing
    private System.Collections.Generic.Queue<ArrowSpawnedEvent> arrowSpawnedQueue = new System.Collections.Generic.Queue<ArrowSpawnedEvent>();
    private System.Collections.Generic.Queue<ArrowHitEvent> arrowHitQueue = new System.Collections.Generic.Queue<ArrowHitEvent>();
    private System.Collections.Generic.Queue<ArrowRemovedEvent> arrowRemovedQueue = new System.Collections.Generic.Queue<ArrowRemovedEvent>();
    private string serverBaseUrl;
    private string jwtToken;
    public int heroId;
    private float lastSendMs;
    private Vector3 change;
    private Vector3 moveDirection;
    private string lastDirection = "";
    private bool isMoving = false;
    private bool lastWasMoving = false;
    private float inputStartMs = 0f;


    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        remoteManager = FindObjectOfType<RemotePlayerManager>();
        logoutManager = FindObjectOfType<LogoutManager>();
        mapDataManager = FindObjectOfType<MapDataManager>();
        enemyClientManager = FindObjectOfType<EnemyClientManager>();


        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            var bounds = collider.bounds;
            heroRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        }

        gameObject.layer = LayerMask.NameToLayer("LocalPlayer");
        serverBaseUrl = Session.ServerBaseUrl;
        jwtToken = Session.JwtToken;
        heroId = Session.SelectedHeroId;
    }

    private async void Start()
    {
        await ConnectToServer();
        if (enemyClientManager != null)
        {
            enemyClientManager.Initialize(serverBaseUrl, jwtToken);
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateAnimation();
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

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private async Task ConnectToServer()
    {
        try
        {
            if (string.IsNullOrEmpty(serverBaseUrl) || string.IsNullOrEmpty(jwtToken))
            {
                return;
            }
            connection = new HubConnectionBuilder()
                .WithUrl($"{serverBaseUrl}/hubs/hero", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(jwtToken);
                })
                .WithAutomaticReconnect()
                .Build();

            connection.On<HeroMoveUpdateEvent>("HeroMoveUpdate", OnHeroMoveUpdate);
            connection.On<HeroSpawnResponse>("HeroSpawned", OnServerSpawnUpdate);
            connection.On<int>("HeroRemoved", OnHeroRemoved);
            connection.On<string>("Kicked", OnKicked);
            connection.On<ArrowSpawnedEvent>("ArrowSpawned", OnArrowSpawned);
            connection.On<ArrowHitEvent>("ArrowHit", OnArrowHit);
            connection.On<ArrowRemovedEvent>("ArrowRemoved", OnArrowRemoved);

            await connection.StartAsync();

            if (remoteManager != null)
            {
                remoteManager.Attach(connection, heroId);
            }


            if (logoutManager != null)
            {
                logoutManager.Initialize(connection, heroId);

            }

            if (mapDataManager != null)
            {
                mapDataManager.Initialize(connection, heroId);
            }


            var enemyClientMgr = FindObjectOfType<EnemyClientManager>();
            if (enemyClientMgr != null)
            {
                enemyClientMgr.Initialize(serverBaseUrl, jwtToken);
            }

            await SendSpawnPosition();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalPlayerController] Connection failed: {ex.Message}");
        }
    }

    private float NowMs() => Time.realtimeSinceStartup * 1000f;

    private void HandleInput()
    {
        change = Vector3.zero;
        change.x = Input.GetAxisRaw("Horizontal");
        change.y = Input.GetAxisRaw("Vertical");

        bool movingThisFrame = false;
        string newDirection = lastDirection;

        if (change.x != 0)
        {
            moveDirection = (change.x > 0) ? Vector3.right : Vector3.left;
            newDirection = (change.x > 0) ? "right" : "left";
            movingThisFrame = true;
        }
        else if (change.y != 0)
        {
            moveDirection = (change.y > 0) ? Vector3.up : Vector3.down;
            newDirection = (change.y > 0) ? "up" : "down";
            movingThisFrame = true;
        }
        else
        {
            moveDirection = Vector3.zero;
            movingThisFrame = false;
        }


        if (movingThisFrame && (!lastWasMoving || newDirection != lastDirection))
        {
            inputStartMs = NowMs();

            _ = SendMoveIntentToServer();
        }

        lastWasMoving = isMoving;
        isMoving = movingThisFrame;
        lastDirection = newDirection;
    }

    private void UpdateAnimation()
    {
        if (isMoving)
        {
            animator.SetFloat("moveX", moveDirection.x);
            animator.SetFloat("moveY", moveDirection.y);
            animator.SetBool("moving", true);
        }
        else
        {
            animator.SetBool("moving", false);
        }
    }

    

    private void MovePlayer()
    {
        if (isMoving)
        {
            Vector2 dir = new Vector2(moveDirection.x, moveDirection.y);
            if (dir.sqrMagnitude > 1e-4f)
            {
                dir = dir.normalized;
            }
            rb.velocity = dir * moveSpeed;
        }
        else
        {
            rb.velocity = Vector2.zero;
        }


        float nowMs = NowMs();
        if (nowMs - lastSendMs >= sendIntervalMs)
        {
            lastSendMs = nowMs;
            // Use new event-based approach instead of request/response
            _ = SendMoveIntentToServer();
        }
    }


    /// <summary>
    /// Send hero movement intent event (new event-based approach)
    /// </summary>
    private async Task SendMoveIntentToServer()
    {
        if (connection?.State != HubConnectionState.Connected) return;

        try
        {
            var moveIntent = new HeroMoveIntentEvent
            {
                HeroId = heroId,
                Direction = isMoving ? lastDirection : "",
                Speed = isMoving ? moveSpeed : 0f,
                ClientTimestampMs = (long)(Time.realtimeSinceStartup * 1000f)
            };

            await connection.InvokeAsync("OnHeroMoveIntent", moveIntent);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalPlayerController] Send move intent failed: {ex.Message}");
        }
    }

    private async Task SendSpawnPosition()
    {
        if (connection?.State != HubConnectionState.Connected) return;


        try
        {


            var collider = GetComponent<Collider2D>();
            float heroRadius = 0.25f;
            float probeOffsetY = -0.3f;

            if (collider != null)
            {
                var bounds = collider.bounds;
                heroRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                probeOffsetY = bounds.center.y - transform.position.y;
            }
            var request = new HeroSpawnRequest
            {
                HeroId = heroId,
                X = transform.position.x,
                Y = transform.position.y,
                HeroRadius = heroRadius,
                ProbeOffsetY = probeOffsetY
            };

            await connection.InvokeAsync("Spawn", request);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalPlayerController] Send spawn failed: {ex.Message}");
        }
    }


    /// <summary>
    /// Handle hero movement update event (new event-based approach)
    /// </summary>
    private void OnHeroMoveUpdate(HeroMoveUpdateEvent updateEvent)
    {
        if (updateEvent.HeroId == heroId)
        {
            Vector3 serverPosition = new Vector3(updateEvent.X, updateEvent.Y, 0f);
            Vector3 localPosition = transform.position;

            float distance = Vector3.Distance(serverPosition, localPosition);

            // Hard snap if position is too far off
            if (distance >= reconciliationHardSnapThreshold)
            {
                rb.position = serverPosition;
                return;
            }

            // Smooth reconciliation if position is slightly off
            if (distance > reconciliationThreshold)
            {
                Vector3 smoothPos = Vector3.Lerp(localPosition, serverPosition, serverLerpFactor);
                rb.position = smoothPos;
            }

            // Update animation based on movement state
            if (animator != null)
            {
                animator.SetBool("isMoving", updateEvent.IsMoving);
                if (updateEvent.IsMoving && !string.IsNullOrEmpty(updateEvent.Direction))
                {
                    animator.SetFloat("moveX", updateEvent.Direction == "left" ? -1f : updateEvent.Direction == "right" ? 1f : 0f);
                    animator.SetFloat("moveY", updateEvent.Direction == "up" ? 1f : updateEvent.Direction == "down" ? -1f : 0f);
                }
            }
        }
    }

    private void OnServerSpawnUpdate(HeroSpawnResponse response)
    {
        if (response.HeroId == heroId)
        {
            Vector3 serverPosition = new Vector3(response.X, response.Y, 0f);
            rb.position = serverPosition;
        }
    }



    private async void OnDisable()
    {
        if (connection != null)
        {
            try
            {
                if (connection.State == HubConnectionState.Connected)
                {
                    await connection.StopAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerController] Stop failed: {ex.Message}");
            }

            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalPlayerController] Dispose failed: {ex.Message}");
            }
        }
    }

    private void OnHeroRemoved(int removedHeroId)
    {
        try
        {
            Debug.LogWarning($"[LocalPlayerController] Hero {removedHeroId} was removed from game");

            if (removedHeroId == heroId)
            {
                Debug.LogWarning("[LocalPlayerController] My hero was removed, returning to hero selection");

                if (connection != null && connection.State == HubConnectionState.Connected)
                {
                    _ = connection.StopAsync();
                }

                Session.JwtToken = null;
                Session.SelectedHeroId = 0;

                UnityEngine.SceneManagement.SceneManager.LoadScene("HeroSelection");
            }
            else
            {
                Debug.Log($"[LocalPlayerController] Remote hero {removedHeroId} was removed");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LocalPlayerController] OnHeroRemoved failed: {ex.Message}");
        }
    }

    private void OnKicked(string reason)
    {

        if (connection != null)
        {
            connection.StopAsync();
        }

        Session.JwtToken = null;
        Session.SelectedHeroId = 0;

        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
    }

    /// <summary>
    /// Handle arrow spawned event (for local player's own arrows)
    /// </summary>
    private void OnArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        Debug.Log($"[LocalPlayerController] OnArrowSpawned received - ArrowId: {spawnedEvent.ArrowId}, HeroId: {spawnedEvent.HeroId}");
        
        // Enqueue for main thread processing
        arrowSpawnedQueue.Enqueue(spawnedEvent);
    }
    
    private void ProcessArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        try
        {
            Debug.Log($"[LocalPlayerController] Processing arrow spawned - ArrowId: {spawnedEvent.ArrowId}, arrowPrefab: {arrowPrefab != null}");
            
            if (arrowPrefab != null)
            {
                Debug.Log($"[LocalPlayerController] Instantiating arrow at position ({spawnedEvent.StartX}, {spawnedEvent.StartY})");
                
                // Calculate rotation based on arrow direction
                Vector2 direction = new Vector2(spawnedEvent.DirectionX, spawnedEvent.DirectionY);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; // Subtract 90 degrees to correct orientation
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                
                var arrowObj = Instantiate(arrowPrefab, new Vector3(spawnedEvent.StartX, spawnedEvent.StartY, 0f), rotation);
                arrowObj.name = $"Arrow_{spawnedEvent.ArrowId}";
                Debug.Log($"[LocalPlayerController] Arrow object created: {arrowObj.name} with rotation: {angle}°");
                
                var arrowController = arrowObj.GetComponent<ArrowController>();
                if (arrowController != null)
                {
                    Debug.Log($"[LocalPlayerController] Initializing ArrowController for arrow {spawnedEvent.ArrowId}");
                    arrowController.Initialize(spawnedEvent);
                    Debug.Log($"[LocalPlayerController] Arrow {spawnedEvent.ArrowId} created and initialized successfully");
                }
                else
                {
                    Debug.LogError($"[LocalPlayerController] ArrowController component not found on arrow prefab!");
                }
            }
            else
            {
                Debug.LogWarning("[LocalPlayerController] Arrow prefab is not assigned!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LocalPlayerController] Error in ProcessArrowSpawned: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Handle arrow hit event
    /// </summary>
    private void OnArrowHit(ArrowHitEvent hitEvent)
    {
        Debug.Log($"[LocalPlayerController] OnArrowHit received - ArrowId: {hitEvent.ArrowId}");
        
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
                Debug.Log($"[LocalPlayerController] Arrow {hitEvent.ArrowId} hit handled");
            }
        }
    }

    /// <summary>
    /// Handle arrow removed event
    /// </summary>
    private void OnArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        Debug.Log($"[LocalPlayerController] OnArrowRemoved received - ArrowId: {removedEvent.ArrowId}");
        
        // Enqueue for main thread processing
        arrowRemovedQueue.Enqueue(removedEvent);
    }
    
    private void ProcessArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        var arrowObj = GameObject.Find($"Arrow_{removedEvent.ArrowId}");
        if (arrowObj != null)
        {
            Destroy(arrowObj);
            Debug.Log($"[LocalPlayerController] Arrow {removedEvent.ArrowId} destroyed");
        }
    }

    private void OnDestroy()
    {
        connection?.DisposeAsync();
    }
}
