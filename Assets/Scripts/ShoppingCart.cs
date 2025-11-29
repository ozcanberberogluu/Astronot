using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class ShoppingCart : MonoBehaviourPun
{
    [Header("Hover / Terrain")]
    [Tooltip("Sepetin zemin üstünde istediðin yükseklik.")]
    public float hoverHeight = 0.4f;

    [Tooltip("Terrain / zemin testleri için sphere yarýçapý.")]
    public float sphereRadius = 0.6f;

    [Tooltip("Hover raycast'inde kullanýlacak katmanlar (Terrain, Ground vs).")]
    public LayerMask groundMask;

    [Tooltip("Hafif yukarý aþaðý bobbing efekti genliði.")]
    public float bobbingAmplitude = 0.05f;

    [Tooltip("Bobbing hýz çarpaný.")]
    public float bobbingSpeed = 1.5f;

    [Header("Push Ayarlarý")]
    [Tooltip("Oyuncu ile sepet arasýndaki mesafe (el arabasý mesafesi).")]
    public float pushDistanceFromPlayer = 1.2f;

    [Tooltip("Sepetin pozisyonuna LERP hýz çarpaný.")]
    public float moveSpeed = 6f;

    [Tooltip("Dönüþ hýz çarpaný.")]
    public float rotationSpeed = 8f;

    [Header("Model Orientation")]
    [Tooltip("Modelin ön yüzünü düzeltmek için Y ekseninde ekstra derece. Örn: 180")]
    public float modelForwardOffsetY = 180f;

    private Rigidbody rb;

    // network state
    private bool isPushed = false;
    private int pusherViewId = -1;
    private Transform pusherTransform;

    private float bobTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (groundMask == 0)
        {
            groundMask = LayerMask.GetMask("Default", "Terrain");
        }
    }

    private void FixedUpdate()
    {
        // Hareketi sadece owner hesaplasýn, diðerleri PhotonTransformView'dan alýr
        if (!photonView.IsMine)
            return;

        Vector3 targetPos = transform.position;

        // PUSH VARSA: oyuncunun önünde hizala
        if (isPushed && pusherTransform != null)
        {
            Vector3 forward = pusherTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;

            targetPos = pusherTransform.position + forward * pushDistanceFromPlayer;
        }

        // Her durumda terrain üzerinde hover et
        targetPos = GetHoverPosition(targetPos);

        // Bobbing
        bobTime += Time.fixedDeltaTime * bobbingSpeed;
        float bobOffset = Mathf.Sin(bobTime) * bobbingAmplitude;
        targetPos.y += bobOffset;

        // Smooth pozisyon
        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        // Smooth rotasyon (sadece push varken oyuncuya bak)
        if (isPushed && pusherTransform != null)
        {
            Vector3 dir = pusherTransform.forward;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);

                if (Mathf.Abs(modelForwardOffsetY) > 0.01f)
                {
                    targetRot *= Quaternion.Euler(0f, modelForwardOffsetY, 0f);
                }

                Quaternion newRot = Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(newRot);
            }
        }
    }

    private Vector3 GetHoverPosition(Vector3 basePos)
    {
        Vector3 origin = basePos + Vector3.up * 2f;

        if (Physics.SphereCast(origin, sphereRadius, Vector3.down,
            out RaycastHit hit, 5f, groundMask, QueryTriggerInteraction.Ignore))
        {
            basePos.y = hit.point.y + hoverHeight;
        }

        return basePos;
    }

    // --------- PUSH API (PlayerCartPush bunu çaðýracak) ---------

    public void BeginPush(PhotonView holder)
    {
        if (holder == null || !holder.IsMine)
            return;

        if (!photonView.IsMine)
            photonView.RequestOwnership();

        photonView.RPC(nameof(RPC_BeginPush), RpcTarget.AllBuffered, holder.ViewID);
    }

    public void EndPush(PhotonView holder)
    {
        if (holder == null || !holder.IsMine)
            return;

        photonView.RPC(nameof(RPC_EndPush), RpcTarget.AllBuffered);
    }

    [PunRPC]
    private void RPC_BeginPush(int holderViewID)
    {
        pusherViewId = holderViewID;
        isPushed = true;

        PhotonView pv = PhotonView.Find(pusherViewId);
        if (pv != null)
        {
            pusherTransform = pv.transform;
        }
    }

    [PunRPC]
    private void RPC_EndPush()
    {
        isPushed = false;
        pusherViewId = -1;
        pusherTransform = null;
    }
}
