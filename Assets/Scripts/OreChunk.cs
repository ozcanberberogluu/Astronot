using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class OreChunk : MonoBehaviourPun
{
    [Header("Data")]
    public OreType oreType;
    public int value;

    [Header("Grab Settings")]
    [Tooltip("Chunk tutulurken kamera önündeki varsayılan mesafe.")]
    public float holdDistance = 2f;          // default daha yakın

    public float minHoldDistance = 1f;
    public float maxHoldDistance = 6f;

    [Tooltip("Chunk'ın hedef pozisyona yaklaşma hızı.")]
    public float followSpeed = 12f;         // biraz düşük, daha smooth

    [Header("Ground Check")]
    [Tooltip("Chunk ile terrain arasında kalmasını istediğimiz minimum boşluk.")]
    public float groundPadding = 0.08f;

    [Tooltip("Terrain / zemin çarpışma testleri için yarıçap.")]
    public float sphereRadius = 0.35f;

    [Tooltip("Terrain ve zemin layer'larını buraya atamalısın.")]
    public LayerMask groundMask;

    [Header("Drop Physics")]
    [Tooltip("Chunk yere düştükten sonraki drag.")]
    public float dropDrag = 4f;

    [Tooltip("Chunk yere düştükten sonraki angular drag.")]
    public float dropAngularDrag = 2f;

    [Tooltip("Aşağı doğru maksimum düşüş hızı.")]
    public float maxFallSpeed = -6f;

    private Rigidbody rb;
    private bool isGrabbed = false;
    private Transform holdTarget;
    private int holderId = -1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.drag = dropDrag;
        rb.angularDrag = dropAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (groundMask == 0)
        {
            // Varsayılan: Default + Terrain
            groundMask = LayerMask.GetMask("Default", "Terrain");
        }
    }

    public void Initialize(OreType type, int chunkValue)
    {
        oreType = type;
        value = chunkValue;
    }

    /// <summary>
    /// Oyuncu chunk'ı tutmaya başladığında PlayerMining burayı çağırır.
    /// Sadece local holder çağırabilir.
    /// </summary>
    public void BeginGrab(PhotonView holder)
    {
        if (holder == null || !holder.IsMine)
            return;

        // 🔑 OWNER’I AL: join oyuncu chunk'ı gerçekten kontrol edebilsin
        if (!photonView.IsMine)
        {
            photonView.RequestOwnership();
        }

        // Grab state'i herkese duyur
        photonView.RPC(nameof(RPC_BeginGrab), RpcTarget.All, holder.ViewID);
    }

    /// <summary>
    /// Oyuncu chunk'ı bırakmak istediğinde çağrılır.
    /// </summary>
    public void EndGrab(PhotonView holder)
    {
        if (holder == null || !holder.IsMine)
            return;

        photonView.RPC(nameof(RPC_EndGrab), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_BeginGrab(int holderViewID)
    {
        holderId = holderViewID;
        isGrabbed = true;

        rb.isKinematic = true;
        rb.useGravity = false;

        PhotonView holder = PhotonView.Find(holderId);
        if (holder != null)
        {
            var cam = holder.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                holdTarget = cam.transform;

                // İlk tutuşta aradaki gerçek mesafeyi baz al
                float dist = Vector3.Distance(holdTarget.position, transform.position);
                holdDistance = Mathf.Clamp(dist, minHoldDistance, maxHoldDistance);
            }
        }
    }

    [PunRPC]
    private void RPC_EndGrab()
    {
        isGrabbed = false;
        holderId = -1;
        holdTarget = null;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.drag = dropDrag;
        rb.angularDrag = dropAngularDrag;
    }

    private void Update()
    {
        // Input sadece owner'da işlesin
        if (!photonView.IsMine)
            return;

        if (isGrabbed && holdTarget != null)
        {
            // Scroll ile ileri/geri mesafeyi ayarla
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                holdDistance = Mathf.Clamp(
                    holdDistance + scroll * 2f,
                    minHoldDistance,
                    maxHoldDistance
                );
            }
        }
    }

    private void FixedUpdate()
    {
        // Fizik sadece owner tarafından hesaplanmalı
        if (!photonView.IsMine)
            return;

        if (isGrabbed && holdTarget != null)
        {
            MoveGrabbed();
        }
        else
        {
            LimitFallSpeed();
        }
    }

    // ---------------- TERRAIN SAFE MOVE ----------------
    private void MoveGrabbed()
    {
        // 1) Kamera önünde hedef pozisyon
        Vector3 targetPos = holdTarget.position + holdTarget.forward * holdDistance;

        // 2) SphereCast ile zemini yakala (terrain için daha güvenli)
        Vector3 sphereOrigin = targetPos + Vector3.up * 1.0f;

        if (Physics.SphereCast(
            sphereOrigin,
            sphereRadius,
            Vector3.down,
            out RaycastHit hit,
            2f,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            float groundY = hit.point.y + groundPadding;

            // Hedef zeminin ALTINDA ise Y'yi yukarı çek
            if (targetPos.y < groundY)
                targetPos.y = groundY;
        }

        // 3) Eğer hala terrain ile overlap varsa hafif yukarı it
        Collider[] cols = Physics.OverlapSphere(
            targetPos,
            sphereRadius,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (cols.Length > 0)
        {
            targetPos.y += groundPadding;
        }

        // 4) Smooth hareket
        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    private void LimitFallSpeed()
    {
        if (rb.isKinematic) return;

        Vector3 v = rb.velocity;
        if (v.y < maxFallSpeed)
        {
            v.y = maxFallSpeed;
            rb.velocity = v;
        }
    }
}
