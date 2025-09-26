using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Events;
using System.Reflection;

public class LocalPlayerBowController : MonoBehaviour
{
    [Header("Bow Settings")]
    public float maxChargeTime = 1f;

    [Header("Accuracy Settings")]
    public float minAccuracyAngle = 30f;
    public float maxAccuracyAngle = 0f;
    public float accuracyChargeThreshold = 0.3f;

    private Animator animator;
    private LocalPlayerController localPlayer;
    private float chargeTime;
    private bool isCharging;
    private HubConnection heroConnection;
    private int heroId;
    public Transform firePoint;

    private float lastSendTime;
    private float sendInterval = 0.05f;
    
    private bool lastWasCharging = false;
    private float lastAngle = 0f;
    private float lastChargePercent = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        localPlayer = GetComponentInParent<LocalPlayerController>();

        if (localPlayer != null)
        {
            heroConnection = localPlayer.connection;
            heroId = localPlayer.heroId;
            Debug.Log($"[LocalPlayerBowController] Initialized - heroConnection: {heroConnection != null}, heroId: {heroId}");
        }
        else
        {
            Debug.LogError("[LocalPlayerBowController] LocalPlayerController not found!");
        }
 
    }

    void Update()
    {
        if (localPlayer == null) return;
        
        if (heroConnection == null || heroId == 0)
        {
            TryGetConnections();
        }
        
        AimAtMouse();
        HandleCharge();
        UpdateVisualFeedback();
    }
    
    private void TryGetConnections()
    {
        if (localPlayer != null)
        {
            var newHeroConnection = localPlayer.connection;
            var newHeroId = localPlayer.heroId;

            if (newHeroConnection != null && newHeroId > 0)
            {
                heroConnection = newHeroConnection;
                heroId = newHeroId;
            }
        }
    }

    void AimAtMouse()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector3 dir = (mousePos - firePoint.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0, 0, angle + 90);
    }

    void HandleCharge()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isCharging = true;
            chargeTime = 0f;
            if (animator) animator.SetBool("isCharging", true);
            lastSendTime = 0f;
            SendBowStateIntent();
        }

        if (isCharging && Input.GetMouseButton(0))
        {
            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Clamp(chargeTime, 0, maxChargeTime);
            
            if (Time.time - lastSendTime >= sendInterval)
            {
                SendBowStateIntent();
            }
        }

        if (isCharging && Input.GetMouseButtonUp(0))
        {
            isCharging = false;
            if (animator)
            {
                animator.SetBool("isCharging", false);
                animator.SetTrigger("shoot");
            }
            
            SendBowStateIntent();
            SendArrowShootIntent();
        }
    }

    private void UpdateVisualFeedback()
    {
        if (animator)
        {
            animator.SetFloat("chargePercent", chargeTime / maxChargeTime);
        }
    }


    private void SendBowStateIntent()
    {
        if (heroConnection != null && heroConnection.State == HubConnectionState.Connected)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector3 dir = (mousePos - transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float chargePercent = chargeTime / maxChargeTime;

            bool chargingChanged = (isCharging != lastWasCharging);
            bool angleChanged = Mathf.Abs(angle - lastAngle) > 5f; 
            bool chargeChanged = Mathf.Abs(chargePercent - lastChargePercent) > 0.05f; 

            if (chargingChanged || angleChanged || chargeChanged)
            {
                var bowStateIntent = new BowStateIntentEvent
                {
                    HeroId = heroId,
                    AngleDeg = angle,
                    IsCharging = isCharging,
                    ChargePercent = chargePercent,
                    ClientTimestampMs = (long)(Time.realtimeSinceStartup * 1000f)
                };

                heroConnection.InvokeAsync("OnBowStateIntent", bowStateIntent);
                lastSendTime = Time.time;
                
                lastWasCharging = isCharging;
                lastAngle = angle;
                lastChargePercent = chargePercent;
            }
        }
    }

 
    private void SendArrowShootIntent()
    {
        Debug.Log($"[LocalPlayerBowController] SendArrowShootIntent called - heroConnection: {heroConnection != null}, State: {heroConnection?.State}");
        
        if (heroConnection != null && heroConnection.State == HubConnectionState.Connected)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector3 dir = (mousePos - transform.position).normalized;
            
            var shootIntent = new ArrowShootIntentEvent
            {
                HeroId = heroId,
                StartX = transform.position.x,
                StartY = transform.position.y,
                DirectionX = dir.x,
                DirectionY = dir.y,
                ChargePercent = chargeTime / maxChargeTime,
                ChargeTime = chargeTime,
                ClientTimestampMs = (long)(Time.realtimeSinceStartup * 1000f)
            };

            Debug.Log($"[LocalPlayerBowController] Sending arrow shoot intent - HeroId: {shootIntent.HeroId}, Charge: {shootIntent.ChargePercent:F2}");
            heroConnection.InvokeAsync("OnArrowShootIntent", shootIntent);
        }
        else
        {
            Debug.LogWarning($"[LocalPlayerBowController] Cannot send arrow shoot intent - heroConnection: {heroConnection != null}, State: {heroConnection?.State}");
        }
    }
}
