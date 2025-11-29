using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerMining : MonoBehaviourPun
{
    [Header("Genel Referanslar")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Animator animator;

    [Header("Mining Ayarları")]
    [SerializeField] private KeyCode mineKey = KeyCode.E;

    [Header("Ore Grab Ayarları")]
    [SerializeField] private float grabRange = 4f;
    [SerializeField] private float grabSphereRadius = 0.4f;
    [SerializeField] private float maxGrabDistance = 8f;

    [Header("Sepet Layer (SepetArkaCollider için)")]
    [Tooltip("Sepetin layer'ı (SepetArkaCollider bu layer'da olmalı).")]
    [SerializeField] private LayerMask cartLayer;

    [Header("Diğer Sistemler")]
    [SerializeField] private PlayerCartPush cartPush;   // sepet iterken mining kapansın

    // --- Mining state ---
    private MineableResource currentResource;   // Trigger'a girdiğimiz maden
    private bool isMining;
    private int hashIsMining;

    // --- Grab state ---
    private OreChunk grabbedChunk;
    public bool IsHoldingChunk => grabbedChunk != null;

    private PhotonView pv;

    private void Awake()
    {
        pv = GetComponent<PhotonView>();

        if (!pv.IsMine)
        {
            enabled = false;
            return;
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (cartPush == null)
        {
            cartPush = GetComponent<PlayerCartPush>();
        }

        if (cartLayer == 0)
        {
            // Varsayılan olarak "Sepet" layer'ını kullan
            cartLayer = LayerMask.GetMask("Sepet");
        }

        hashIsMining = Animator.StringToHash("IsMining");
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        // Sepeti iterken: mining + chunk grab tamamen kapalı
        if (cartPush != null && cartPush.IsPushing)
        {
            StopMining();
            StopGrab();
            return;
        }

        HandleMining();
        HandleGrab();
    }

    // ===================== MINING ======================

    private void HandleMining()
    {
        bool mineHeld = Input.GetKey(mineKey);

        if (mineHeld && currentResource != null)
        {
            // Animasyon
            if (!isMining)
            {
                isMining = true;
                if (animator != null)
                    animator.SetBool(hashIsMining, true);
            }

            // Tüm tick / titreme / FX / ses işini MineableResource hallediyor
            currentResource.Mine(Time.deltaTime, this);
        }
        else
        {
            StopMining();
        }
    }

    private void StopMining()
    {
        if (!isMining) return;

        isMining = false;
        if (animator != null)
            animator.SetBool(hashIsMining, false);
    }

    // ===================== ORE GRAB ====================

    private void HandleGrab()
    {
        bool holdingMouse = Input.GetMouseButton(0);

        if (holdingMouse)
        {
            if (grabbedChunk == null)
            {
                TryStartGrab();
            }
            // grabbedChunk hareketini kendi OreChunk scriptin yapıyor
        }
        else
        {
            if (grabbedChunk != null)
            {
                StopGrab();
            }
        }
    }

    private void TryStartGrab()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // 🔒 1) Eğer crosshair SEPETE (SepetArkaCollider) bakıyorsa,
        //      chunk grab DENEME sakın yapma -> öncelik sepet push'ta
        if (cartLayer != 0)
        {
            if (Physics.SphereCast(ray, grabSphereRadius, out RaycastHit cartHit, grabRange, cartLayer,
                    QueryTriggerInteraction.Ignore))
            {
                // Bu collider'ın üzerinde ShoppingCartHandle var mı?
                if (cartHit.collider.GetComponent<ShoppingCartHandle>() != null)
                {
                    // Sepete bakıyoruz, grab iptal, PlayerCartPush bu frame'de devreye girecek
                    return;
                }
            }
        }

        // 🔍 2) SphereCast ile bakılan chunk'ı bul
        if (Physics.SphereCast(ray, grabSphereRadius, out RaycastHit hit, grabRange,
                ~cartLayer, QueryTriggerInteraction.Ignore)) // cartLayer hariç her şey
        {
            OreChunk chunk = hit.collider.GetComponentInParent<OreChunk>();
            if (chunk != null)
            {
                grabbedChunk = chunk;
                grabbedChunk.BeginGrab(pv);
                return;
            }
        }

        // 3) Bakılan yönün etrafında en yakın chunk'ı ara (optionel yardımcı)
        Vector3 center = playerCamera.transform.position + playerCamera.transform.forward * 2f;
        Collider[] cols = Physics.OverlapSphere(center, 1.2f, ~cartLayer, QueryTriggerInteraction.Ignore);

        float bestDist = float.MaxValue;
        OreChunk best = null;

        foreach (var col in cols)
        {
            OreChunk c = col.GetComponentInParent<OreChunk>();
            if (c == null) continue;

            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < bestDist && d <= maxGrabDistance)
            {
                bestDist = d;
                best = c;
            }
        }

        if (best != null)
        {
            grabbedChunk = best;
            grabbedChunk.BeginGrab(pv);
        }
    }

    private void StopGrab()
    {
        if (grabbedChunk == null) return;

        grabbedChunk.EndGrab(pv);
        grabbedChunk = null;
    }

    // ===================== TRIGGERLAR ==================

    private void OnTriggerEnter(Collider other)
    {
        if (!pv.IsMine) return;

        // Maden alanına girdiysek
        MineableResource res = other.GetComponentInParent<MineableResource>();
        if (res != null)
        {
            if (currentResource != null && currentResource != res)
            {
                currentResource.SetPromptVisible(false);
            }

            currentResource = res;
            currentResource.SetPromptVisible(true);   // [E] TOPLA aç
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!pv.IsMine) return;

        MineableResource res = other.GetComponentInParent<MineableResource>();
        if (res != null && res == currentResource)
        {
            currentResource.SetPromptVisible(false);  // [E] TOPLA kapan
            currentResource = null;
            StopMining();
        }
    }
}
