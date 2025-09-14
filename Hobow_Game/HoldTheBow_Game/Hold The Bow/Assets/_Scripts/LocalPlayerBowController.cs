using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using SharedLibrary.Requests;
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

    void Start()
    {
        animator = GetComponent<Animator>();
        localPlayer = GetComponentInParent<LocalPlayerController>();

        var heroConnectionField = typeof(LocalPlayerController).GetField("connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var heroIdField = typeof(LocalPlayerController).GetField("heroId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (heroConnectionField != null && heroIdField != null)
        {
            heroConnection = (HubConnection)heroConnectionField.GetValue(localPlayer);
            heroId = (int)heroIdField.GetValue(localPlayer);

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
        var heroConnectionField = typeof(LocalPlayerController).GetField("connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var heroIdField = typeof(LocalPlayerController).GetField("heroId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (heroConnectionField != null && heroIdField != null)
        {
            var newHeroConnection = (HubConnection)heroConnectionField.GetValue(localPlayer);
            var newHeroId = (int)heroIdField.GetValue(localPlayer);

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
            SendBowStateImmediate();
        }

        if (isCharging && Input.GetMouseButton(0))
        {
            chargeTime += Time.deltaTime;
            chargeTime = Mathf.Clamp(chargeTime, 0, maxChargeTime);
            
            if (Time.time - lastSendTime >= sendInterval)
            {
                SendBowStateImmediate();
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
            SendStopChargingState();
        }
    }



    private void UpdateVisualFeedback()
    {
        if (animator)
        {
            animator.SetFloat("chargePercent", chargeTime / maxChargeTime);
        }
    }


    private void SendBowStateImmediate()
    {
        if (heroConnection != null && heroConnection.State == HubConnectionState.Connected)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector3 dir = (mousePos - transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float chargePercent = chargeTime / maxChargeTime;

            var request = new BowStateRequest
            {
                HeroId = heroId,
                AngleDeg = angle,
                IsCharging = isCharging,
                ChargePercent = chargePercent
            };

            heroConnection.InvokeAsync("UpdateBowState", request);
            lastSendTime = Time.time;
        }
    }

    private void SendStopChargingState()
    {
        if (heroConnection != null && heroConnection.State == HubConnectionState.Connected)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector3 dir = (mousePos - transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float chargePercent = chargeTime / maxChargeTime;

            var request = new BowStateRequest
            {
                HeroId = heroId,
                AngleDeg = angle,
                IsCharging = false,
                ChargePercent = chargePercent
            };

            heroConnection.InvokeAsync("UpdateBowState", request);
        }
    }
}
