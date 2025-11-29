using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;

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

    [Header("Value / UI")]
    public int totalValue;
    [SerializeField] private TMP_Text valueText;   // Sepetin önündeki "100€" yazýsý

    private Rigidbody rb;

    // Push state
    private bool isPushed = false;
    private int pusherViewId = -1;
    private Transform pusherTransform;

    private float bobTime;

    // Sepetin içinde duran OreChunk'lar
    private readonly HashSet<OreChunk> containedChunks = new HashSet<OreChunk>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (groundMask == 0)
            groundMask = LayerMask.GetMask("Default", "Terrain");

        if (valueText == null)
            valueText = GetComponentInChildren<TMP_Text>(true);

        UpdateValueText();
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine)
            return;

        Vector3 targetPos = transform.position;

        if (isPushed && pusherTransform != null)
        {
            Vector3 forward = pusherTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;

            targetPos = pusherTransform.position + forward * pushDistanceFromPlayer;
        }

        targetPos = GetHoverPosition(targetPos);

        bobTime += Time.fixedDeltaTime * bobbingSpeed;
        float bobOffset = Mathf.Sin(bobTime) * bobbingAmplitude;
        targetPos.y += bobOffset;

        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);

        if (isPushed && pusherTransform != null)
        {
            Vector3 dir = pusherTransform.forward;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);

                if (Mathf.Abs(modelForwardOffsetY) > 0.01f)
                    targetRot *= Quaternion.Euler(0f, modelForwardOffsetY, 0f);

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

    // --------- PUSH API ---------

    public void BeginPush(PhotonView holder)
    {
        if (holder == null || !holder.IsMine)
            return;

        if (!photonView.IsMine)
            photonView.RequestOwnership();

        // Sepeti iten oyuncuya içindeki tüm chunk'larýn ownerlýðýný ver
        TransferContainedChunksOwnership(holder);

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
            pusherTransform = pv.transform;
    }

    [PunRPC]
    private void RPC_EndPush()
    {
        isPushed = false;
        pusherViewId = -1;
        pusherTransform = null;
    }

    private void TransferContainedChunksOwnership(PhotonView newOwner)
    {
        if (newOwner == null || !newOwner.IsMine)
            return;

        foreach (OreChunk chunk in containedChunks)
        {
            if (chunk == null) continue;

            PhotonView chunkView = chunk.GetComponent<PhotonView>();
            if (chunkView == null) continue;

            if (!chunkView.IsMine)
                chunkView.RequestOwnership();
        }
    }

    // --------- VALUE / UI ---------

    private void ChangeTotalValue(int delta)
    {
        if (!photonView.IsMine)
            return;

        totalValue += delta;
        if (totalValue < 0) totalValue = 0;

        photonView.RPC(nameof(RPC_SyncTotalValue), RpcTarget.All, totalValue);
    }

    [PunRPC]
    private void RPC_SyncTotalValue(int newTotal)
    {
        totalValue = newTotal;
        UpdateValueText();
    }

    private void UpdateValueText()
    {
        if (valueText != null)
            valueText.text = totalValue.ToString() + "€";
    }

    // --------- CHUNK LÝSTESÝ API ---------

    public void RegisterChunk(OreChunk chunk)
    {
        if (chunk == null) return;

        // HashSet ayný chunk'ý iki kez eklemeyi engeller
        bool added = containedChunks.Add(chunk);
        if (added)
        {
            ChangeTotalValue(chunk.value);
        }
    }

    public void UnregisterChunk(OreChunk chunk)
    {
        if (chunk == null) return;

        bool removed = containedChunks.Remove(chunk);
        if (removed)
        {
            ChangeTotalValue(-chunk.value);
        }
    }
}
