using UnityEngine;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Requests;
using SharedLibrary.Responses;

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

    private HubConnection connection;
    private string serverBaseUrl;
    private string jwtToken;
    private int heroId;
    private float lastSendMs;
    private Vector3 change;
    private Vector3 moveDirection;
    private string lastDirection = "";
    private string lastSentDirection = "";
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

            connection.On<HeroMoveResponse>("HeroMoved", OnServerPositionUpdate);
            connection.On<HeroSpawnResponse>("HeroSpawned", OnServerSpawnUpdate);
            connection.On<int>("HeroRemoved", OnHeroRemoved);
            connection.On<string>("Kicked", OnKicked);

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

            lastSentDirection = "";
            _ = SendMoveToServerImmediate(newDirection);
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
            _ = SendMoveToServer();
        }
    }

    private async Task SendMoveToServerImmediate(string direction)
    {
        if (connection?.State != HubConnectionState.Connected) return;
        try
        {

            if (direction == lastSentDirection) return;
            lastSentDirection = direction;

            var request = new HeroMoveRequest
            {
                HeroId = heroId,
                Direction = direction,
                Speed = moveSpeed
            };
            await connection.InvokeAsync("Move", request);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalPlayerController] Send immediate move failed: {ex.Message}");
        }
    }

    private async Task SendMoveToServer()
    {
        if (connection?.State != HubConnectionState.Connected) return;
        if (!isMoving) return;

        try
        {
            var request = new HeroMoveRequest
            {
                HeroId = heroId,
                Direction = lastDirection,
                Speed = moveSpeed
            };

            await connection.InvokeAsync("Move", request);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalPlayerController] Send move failed: {ex.Message}");
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

    private void OnServerPositionUpdate(HeroMoveResponse response)
    {
        if (response.HeroId == heroId)
        {
            Vector3 serverPosition = new Vector3(response.X, response.Y, 0f);
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

    private void OnDestroy()
    {
        connection?.DisposeAsync();
    }
}
