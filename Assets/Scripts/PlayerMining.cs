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

    [Header("Diğer Sistemler")]
    [SerializeField] private PlayerCartPush cartPush;   // sepet iterken mining kapansın

    // --- Mining state ---
    private MineableResource currentResource;   // Trigger'a girdiğimiz maden
    private bool isMining;
    private int hashIsMining;

    // --- Grab state ---
    private OreChunk grabbedChunk;

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

        // 1) SphereCast ile bakılan chunk'ı bul
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.SphereCast(ray, grabSphereRadius, out RaycastHit hit, grabRange))
        {
            OreChunk chunk = hit.collider.GetComponentInParent<OreChunk>();
            if (chunk != null)
            {
                grabbedChunk = chunk;
                grabbedChunk.BeginGrab(pv);
                return;
            }
        }

        // 2) Bakılan yönün etrafında en yakın chunk'ı ara
        Vector3 center = playerCamera.transform.position + playerCamera.transform.forward * 2f;
        Collider[] cols = Physics.OverlapSphere(center, 1.2f);

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

        // 1) Maden alanına girdiysek
        MineableResource res = other.GetComponentInParent<MineableResource>();
        if (res != null)
        {
            // Eski madenin UI'ını kapat
            if (currentResource != null && currentResource != res)
            {
                currentResource.SetPromptVisible(false);
            }

            currentResource = res;
            currentResource.SetPromptVisible(true);   // [E] TOPLA aç
        }

        // 2) Deposit / base vs. varsa, eski kodların varsa buraya eklenebilir
        // DepositZone depot = other.GetComponent<DepositZone>();
        // ...
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

        // DepositZone çıkış vs. burada ele alınabilir
    }
}
