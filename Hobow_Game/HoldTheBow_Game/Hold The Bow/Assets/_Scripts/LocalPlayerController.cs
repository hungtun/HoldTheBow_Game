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
    [Header("UI References")]
    private HealthBar healthBar;


    private Animator animator;
    private Rigidbody2D rb;
    private RemotePlayerManager remoteManager;
    private LogoutManager logoutManager;
    private MapDataManager mapDataManager;
    private EnemyClientManager enemyClientManager;
    private float heroRadius = 0.25f;

    [Header("Arrow Settings")]
    public GameObject arrowPrefab;

    public HubConnection connection;

    private System.Collections.Generic.Queue<ArrowSpawnedEvent> arrowSpawnedQueue = new System.Collections.Generic.Queue<ArrowSpawnedEvent>();
    private System.Collections.Generic.Queue<ArrowHitEvent> arrowHitQueue = new System.Collections.Generic.Queue<ArrowHitEvent>();
    private System.Collections.Generic.Queue<ArrowRemovedEvent> arrowRemovedQueue = new System.Collections.Generic.Queue<ArrowRemovedEvent>();
    private System.Collections.Generic.Queue<HeroDamagedEvent> heroDamagedQueue = new System.Collections.Generic.Queue<HeroDamagedEvent>();
    private System.Collections.Generic.Queue<int> heroRemovedQueue = new System.Collections.Generic.Queue<int>();
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
    public int currentHealth;
    public int maxHealth;
    
    private SpriteRenderer spriteRenderer;
    private bool isDamageFlashing = false;
    private Color originalColor = Color.white;
    private bool isDead = false;


    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
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
        
        if (healthBar == null)
        {
            var go = GameObject.FindGameObjectWithTag("LocalHealthBar");
            if (go != null)
            {
                healthBar = go.GetComponent<HealthBar>();
            }
        }
        if (maxHealth <= 0) maxHealth = 100;
        if (currentHealth <= 0) currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
            healthBar.SetHealth(currentHealth);
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateAnimation();
        ProcessArrowEvents();
        ProcessHeroRemovedEvents();
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
            connection.On<HeroDamagedEvent>("HeroDamaged", OnHeroDamaged);
            connection.On<HeroDamagedEvent>("HeroDamaged", OnHeroDamaged);

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
        if(isDead) return;
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
            _ = SendMoveIntentToServer();
        }
    }

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

        float outHeroRadius = 0.25f;
        float outProbeOffsetY = -0.3f;
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            var bounds = col.bounds;
            outHeroRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            outProbeOffsetY = bounds.center.y - transform.position.y;
        }

        var box = GetComponent<BoxCollider2D>();
        Vector2 centerOffset = Vector2.zero;
        Vector2 half = new Vector2(outHeroRadius, outHeroRadius);
        if (box != null)
        {
            var b = box.bounds; 
            centerOffset = (Vector2)(b.center - transform.position);
            half = (Vector2)b.extents;
        }

        var request = new HeroSpawnRequest
        {
            HeroId = heroId,
            X = transform.position.x,
            Y = transform.position.y,
            HeroRadius = outHeroRadius,
            ProbeOffsetY = outProbeOffsetY,
            HitboxCenterOffsetX = centerOffset.x,
            HitboxCenterOffsetY = centerOffset.y,
            HitboxHalfSizeX = half.x,
            HitboxHalfSizeY = half.y
        };

        await connection.InvokeAsync("Spawn", request);
    }

    private void OnHeroMoveUpdate(HeroMoveUpdateEvent updateEvent)
    {
        if (updateEvent.HeroId == heroId)
        {
            Vector3 serverPosition = new Vector3(updateEvent.X, updateEvent.Y, 0f);
            Vector3 localPosition = transform.position;

            float distance = Vector3.Distance(serverPosition, localPosition);

            if (distance >= reconciliationHardSnapThreshold)
            {
                rb.position = serverPosition;
                return;
            }

            if (distance > reconciliationThreshold)
            {
                Vector3 smoothPos = Vector3.Lerp(localPosition, serverPosition, serverLerpFactor);
                rb.position = smoothPos;
            }
        }
    }

    private void OnServerSpawnUpdate(HeroSpawnResponse response)
    {
        if (response.HeroId == heroId)
        {
            Vector3 serverPosition = new Vector3(response.X, response.Y, 0f);
            rb.position = serverPosition;

            if (response.MaxHealth > 0)
            {
                maxHealth = response.MaxHealth;
                if (currentHealth <= 0 || currentHealth > maxHealth)
                {
                    currentHealth = maxHealth;
                }
                if (healthBar != null)
                {
                    healthBar.SetMaxHealth(maxHealth);
                    healthBar.SetHealth(currentHealth);
                }
            }
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
        heroRemovedQueue.Enqueue(removedHeroId);
    }

    private void ProcessHeroRemovedEvents()
    {
        while (heroRemovedQueue.Count > 0)
        {
            var removedHeroId = heroRemovedQueue.Dequeue();

            try
            {
                Debug.LogWarning($"[LocalPlayerController] Hero {removedHeroId} was removed from game");

                if (removedHeroId == heroId)
                {
                    Debug.LogWarning("[LocalPlayerController] My hero was removed, returning to hero selection");

                    if (connection != null && connection.State == HubConnectionState.Connected)
                    {
                        connection.StopAsync();
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
                Debug.LogError($"[LocalPlayerController] ProcessHeroRemovedEvents failed: {ex.Message}");
            }
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

 
    private void OnArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        

        arrowSpawnedQueue.Enqueue(spawnedEvent);
    }

    private void ProcessArrowSpawned(ArrowSpawnedEvent spawnedEvent)
    {
        try
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
                else
                {
                    Debug.LogError("ArrowController component not found on arrow prefab!");
                }
            }
            else
            {
                
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error in ProcessArrowSpawned: {ex.Message}\n{ex.StackTrace}");
        }
    }

  
    private void OnArrowHit(ArrowHitEvent hitEvent)
    {
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
                
            }
        }
    }

    private void OnArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        arrowRemovedQueue.Enqueue(removedEvent);
    }

    private void ProcessArrowRemoved(ArrowRemovedEvent removedEvent)
    {
        var arrowObj = GameObject.Find($"Arrow_{removedEvent.ArrowId}");
        if (arrowObj != null)
        {
            Destroy(arrowObj);
            
        }
    }

    private void OnHeroDamaged(HeroDamagedEvent dmg)
    {
        if (dmg == null) return;
        heroDamagedQueue.Enqueue(dmg);
    }

    private void ProcessHeroDamagedEvents()
    {
        while (heroDamagedQueue.Count > 0)
        {
            var dmg = heroDamagedQueue.Dequeue();
            if (dmg.HeroId != heroId) continue;

            currentHealth = Mathf.Max(0, dmg.NewHealth);
            if (healthBar != null)
            {
                healthBar.SetHealth(currentHealth);
            }

            if (spriteRenderer != null && !isDamageFlashing)
            {
                StartCoroutine(FlashLocalHero());
            }
            

            if (!isDead && currentHealth <= 0)
            {
                isDead = true;
                StartCoroutine(HandleLocalHeroDeath());
            }
        }
    }

    private System.Collections.IEnumerator FlashLocalHero()
    {
        isDamageFlashing = true;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(1f);
        spriteRenderer.color = originalColor;
        isDamageFlashing = false;
    }

    private System.Collections.IEnumerator HandleLocalHeroDeath()
    {
        if (animator != null)
        {
            animator.SetBool("moving", false);
            animator.SetTrigger("dying");
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        yield return new WaitForSeconds(3f);

        try
        {
            if (connection != null && connection.State == HubConnectionState.Connected)
            {
                connection.StopAsync();
            }
        }
        catch {}

        Session.JwtToken = null;
        Session.SelectedHeroId = 0;
        UnityEngine.SceneManagement.SceneManager.LoadScene("HeroSelection");
    }

    private void OnDestroy()
    {
        connection?.DisposeAsync();
    }
}
