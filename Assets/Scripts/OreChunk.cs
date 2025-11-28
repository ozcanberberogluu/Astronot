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
    public float holdDistance = 2f;          // default daha yakın
    public float minHoldDistance = 1f;
    public float maxHoldDistance = 6f;
    public float followSpeed = 12f;         // biraz düşürdük, daha smooth

    [Header("Ground Check")]
    [Tooltip("Chunk ile terrain arasında kalmasını istediğimiz minimum boşluk.")]
    public float groundPadding = 0.08f;

    [Tooltip("Terrain / zemin çarpışma testleri için yarıçap.")]
    public float sphereRadius = 0.35f;

    public LayerMask groundMask;

    [Header("Drop Physics")]
    public float dropDrag = 4f;
    public float dropAngularDrag = 2f;
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
            groundMask = LayerMask.GetMask("Default", "Terrain");
    }

    public void Initialize(OreType type, int chunkValue)
    {
        oreType = type;
        value = chunkValue;
    }

    public void BeginGrab(PhotonView holder)
    {
        if (!holder.IsMine) return;
        if (!photonView.IsMine) photonView.RequestOwnership();

        photonView.RPC(nameof(RPC_BeginGrab), RpcTarget.AllBuffered, holder.ViewID);
    }

    public void EndGrab(PhotonView holder)
    {
        if (!holder.IsMine) return;
        photonView.RPC(nameof(RPC_EndGrab), RpcTarget.AllBuffered);
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

                // *** ÖNEMLİ: İlk tutuş mesafesini gerçek mesafeden ayarla ***
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
        if (!photonView.IsMine) return;

        if (isGrabbed && holdTarget != null)
        {
            // Scroll yönü: ileri çevirince uzaklaşsın, geri çevirince yaklaşsın
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
        if (!photonView.IsMine) return;

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
        Collider[] cols = Physics.OverlapSphere(targetPos, sphereRadius, groundMask, QueryTriggerInteraction.Ignore);
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
