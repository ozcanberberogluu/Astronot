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
    public float holdDistance = 3f;
    public float minHoldDistance = 1f;
    public float maxHoldDistance = 8f;
    public float followSpeed = 18f;

    [Header("Ground Check")]
    public float groundPadding = 0.05f;
    public float sphereRadius = 0.25f;      // chunk alt yarıçapı
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

        PhotonView holder = PhotonView.Find(holderViewID);

        if (holder != null)
        {
            var cam = holder.GetComponentInChildren<Camera>(true);
            if (cam != null)
                holdTarget = cam.transform;
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
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            holdDistance = Mathf.Clamp(
                holdDistance + scroll * 2f,
                minHoldDistance,
                maxHoldDistance
            );
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

    // ----------------------------------------------------------
    //              TERRAIN SAFE MOVE SYSTEM
    // ----------------------------------------------------------
    private void MoveGrabbed()
    {
        // 1) Normal hedef pozisyon
        Vector3 targetPos = holdTarget.position + holdTarget.forward * holdDistance;

        // 2) Terrain'e SPHERECAST xuống (down) – EN SAĞLAM YÖNTEM
        Vector3 sphereOrigin = targetPos + Vector3.up * 1.0f;

        if (Physics.SphereCast(
            sphereOrigin,
            sphereRadius,
            Vector3.down,
            out RaycastHit hit,
            2f,
            groundMask,
            QueryTriggerInteraction.Ignore
        ))
        {
            // Eğer hedef pozisyon zeminin altında kalıyorsa → YUKARI CLAMP
            float desiredY = hit.point.y + groundPadding;

            if (targetPos.y < desiredY)
                targetPos.y = desiredY;
        }

        // 3) Collider OVERLAP kontrolü → terrain içine girmeyi önle
        Collider[] cols = Physics.OverlapSphere(targetPos, sphereRadius, groundMask);
        if (cols.Length > 0)
        {
            // içerideyse → 0.05 yukarı iter
            targetPos.y += groundPadding;
        }

        // 4) Smooth movement
        rb.MovePosition(
            Vector3.Lerp(transform.position, targetPos, followSpeed * Time.fixedDeltaTime)
        );
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
