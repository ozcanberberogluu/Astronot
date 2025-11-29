using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class OreChunk : MonoBehaviourPun, IPunObservable
{
    [Header("Data")]
    public OreType oreType;
    public int value;

    [Header("World Text")]
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Canvas valueCanvas;   // textin olduğu world-space canvas
    [Tooltip("Text'in chunk merkezinin ne kadar ÜSTÜNDE duracağını belirler.")]
    [SerializeField] private float textHeightOffset = 0.35f;

    [Header("Grab Settings")]
    [Tooltip("Chunk tutulurken kamera önündeki varsayılan mesafe.")]
    public float holdDistance = 2f;

    public float minHoldDistance = 1f;
    public float maxHoldDistance = 6f;

    [Tooltip("Chunk'ın hedef pozisyona yaklaşma hızı.")]
    public float followSpeed = 12f;

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

    // Billboard için
    private Camera mainCam;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.drag = dropDrag;
        rb.angularDrag = dropAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (groundMask == 0)
            groundMask = LayerMask.GetMask("Default", "Terrain");

        // TMP referanslarını otomatik bul
        if (valueText == null)
            valueText = GetComponentInChildren<TMP_Text>(true);

        if (valueCanvas == null && valueText != null)
            valueCanvas = valueText.GetComponentInParent<Canvas>(true);

        // Oyuna spawn olduğunda text gizli olsun
        SetTextVisible(false);

        // Değer sadece owner tarafında randomlanır,
        // diğer client'lara OnPhotonSerializeView ile aktarılır.
        if (photonView.IsMine && value == 0)
        {
            value = GetRandomValueForType(oreType);
        }

        UpdateValueString();

        mainCam = Camera.main;
    }

    private void Start()
    {
        if (mainCam == null)
            mainCam = Camera.main;
    }

    // Fiyat aralıkları
    private int GetRandomValueForType(OreType type)
    {
        switch (type)
        {
            case OreType.Diamond:
                // 20 - 50 €
                return Random.Range(20, 51);
            case OreType.Iron:
                // 15 - 25 €
                return Random.Range(15, 26);
            default:
                // fallback
                return Random.Range(10, 21);
        }
    }

    private void UpdateValueString()
    {
        if (valueText != null)
            valueText.text = value.ToString() + "€";
    }

    private void SetTextVisible(bool visible)
    {
        if (valueCanvas != null)
            valueCanvas.enabled = visible;
    }

    public void Initialize(OreType type, int chunkValue)
    {
        oreType = type;
        value = chunkValue;
        UpdateValueString();
    }

    // ------------ GRAB API ------------

    public void BeginGrab(PhotonView holder)
    {
        if (holder == null || !holder.IsMine)
            return;

        if (!photonView.IsMine)
            photonView.RequestOwnership();   // join oyuncu da full kontrol alabilsin

        photonView.RPC(nameof(RPC_BeginGrab), RpcTarget.All, holder.ViewID);
    }

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

                float dist = Vector3.Distance(holdTarget.position, transform.position);
                holdDistance = Mathf.Clamp(dist, minHoldDistance, maxHoldDistance);
            }
        }

        // Tutulurken text HERKES için görünür
        SetTextVisible(true);
        UpdateValueString();
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

        // Bırakıldığında text gizlenir
        SetTextVisible(false);
    }

    // ------------ UPDATE / PHYSICS ------------

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        if (isGrabbed && holdTarget != null)
        {
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

    // 🔁 Text için billboard + sabit yükseklik
    private void LateUpdate()
    {
        if (valueCanvas == null || !valueCanvas.enabled)
            return;

        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return;
        }

        Transform t = valueCanvas.transform;

        // 1) Pozisyonu: chunk merkezinin DÜNYA yukarısında sabit bir offset
        t.position = transform.position + Vector3.up * textHeightOffset;

        // 2) Rotasyonu: kameraya baksın, yukarı ekseni dünya up olsun
        Vector3 dir = t.position - mainCam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            t.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    private void MoveGrabbed()
    {
        Vector3 targetPos = holdTarget.position + holdTarget.forward * holdDistance;

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

            if (targetPos.y < groundY)
                targetPos.y = groundY;
        }

        Collider[] cols = Physics.OverlapSphere(
            targetPos,
            sphereRadius,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (cols.Length > 0)
        {
            targetPos.y += groundPadding;
        }

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

    // ------------- PHOTON SYNC (Value senkron) -------------
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(value);
        }
        else
        {
            int newVal = (int)stream.ReceiveNext();
            if (newVal != value)
            {
                value = newVal;
                UpdateValueString();
            }
        }
    }
}
