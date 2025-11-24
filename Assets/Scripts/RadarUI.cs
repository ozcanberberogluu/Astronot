using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class RadarUI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public RectTransform radarRect;
    public RectTransform blipContainer;
    public RectTransform sweepTransform;
    public CanvasGroup canvasGroup;

    [Header("Blip Prefabs")]
    public GameObject blipPlayerPrefab;
    public GameObject blipShipPrefab;
    public GameObject blipOtherPrefab;

    [Header("Settings")]
    public float radarRange = 100f;
    public float sweepSpeed = 180f;

    private readonly Dictionary<RadarTarget, RectTransform> blipDict =
        new Dictionary<RadarTarget, RectTransform>();

    private PhotonView photonView;
    private bool isLocalPlayer = false;

    private void Awake()
    {
        // Parent'tan PhotonView'ı al (Player root'unda olmalı)
        photonView = GetComponentInParent<PhotonView>();
        
        // Local player kontrolü
        isLocalPlayer = photonView != null && photonView.IsMine;
        
        // Local player değilse, radar canvas'ı tamamen kapat
        if (!isLocalPlayer)
        {
            // Canvas component'ini bul ve devre dışı bırak
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = false;
            }
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            
            // Bu script'i devre dışı bırak (diğer oyuncuların radar'ı çalışmasın)
            enabled = false;
            return;
        }
    }

    void Update()
    {
        // Sadece local player için çalış
        if (!isLocalPlayer) return;

        bool visible = Input.GetKey(KeyCode.Tab);

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;

        if (sweepTransform != null)
            sweepTransform.Rotate(0f, 0f, -sweepSpeed * Time.unscaledDeltaTime);

        if (!visible || player == null)
            return;

        SyncBlips();
        UpdateBlipPositions();
    }



    void SyncBlips()
    {
        foreach (var target in RadarTarget.AllTargets)
        {
            if (target == null) continue;
            if (target.transform == player) continue;
            if (blipDict.ContainsKey(target)) continue;

            GameObject prefab = blipOtherPrefab;

            switch (target.type)
            {
                case RadarTargetType.Player: prefab = blipPlayerPrefab; break;
                case RadarTargetType.Ship: prefab = blipShipPrefab; break;
            }

            GameObject blipGO = Instantiate(prefab, blipContainer);
            RectTransform rt = blipGO.GetComponent<RectTransform>();
            blipDict[target] = rt;
        }


        List<RadarTarget> toRemove = new List<RadarTarget>();

        foreach (var kvp in blipDict)
        {
            if (kvp.Key == null || !RadarTarget.AllTargets.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);

                toRemove.Add(kvp.Key);
            }
        }

        foreach (var t in toRemove)
            blipDict.Remove(t);
    }



    void UpdateBlipPositions()
    {
        float radarRadius = radarRect.rect.width * 0.5f;

        foreach (var kvp in blipDict)
        {
            RadarTarget target = kvp.Key;
            RectTransform blipRect = kvp.Value;

            if (target == null || blipRect == null) continue;

            // Hedefin player'a göre offset'i (3D uzayda)
            Vector3 offset3D = target.transform.position - player.position;
            
            // XZ düzleminde offset (Y eksenini göz ardı et)
            Vector3 offsetXZ = new Vector3(offset3D.x, 0f, offset3D.z);
            
            float distance = offsetXZ.magnitude;

            if (distance > radarRange)
            {
                blipRect.gameObject.SetActive(false);
                continue;
            }

            blipRect.gameObject.SetActive(true);

            // Player'ın forward vektörünü XZ düzleminde al
            Vector3 playerForwardXZ = new Vector3(player.forward.x, 0f, player.forward.z).normalized;
            
            // Player'ın bakış açısını hesapla (radyan cinsinden)
            // Unity'de forward Z ekseninde, ama radar UI'da Y ekseni yukarı
            float playerAngle = Mathf.Atan2(playerForwardXZ.x, playerForwardXZ.z) * Mathf.Rad2Deg;
            
            // Offset'i player'ın bakış açısına göre döndür
            // Radar'da üst taraf forward yönü olacak şekilde
            float offsetAngle = Mathf.Atan2(offsetXZ.x, offsetXZ.z) * Mathf.Rad2Deg;
            float relativeAngle = (offsetAngle - playerAngle) * Mathf.Deg2Rad;
            
            // Radar koordinat sistemine çevir (Y ekseni yukarı, X ekseni sağa)
            float radarX = Mathf.Sin(relativeAngle) * distance;
            float radarY = Mathf.Cos(relativeAngle) * distance;
            
            // Radar menziline normalize et ve radar radius'una göre ölçekle
            float normalizedDistance = distance / radarRange;
            Vector2 radarPos = new Vector2(radarX, radarY).normalized * (normalizedDistance * radarRadius);

            blipRect.anchoredPosition = radarPos;
        }
    }
}
