using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerCartPush : MonoBehaviourPun
{
    [Header("Referanslar")]
    public Camera playerCamera;
    [SerializeField] private PlayerMining playerMining;   // ELİNDE CHUNK VAR MI KONTROLÜ

    [Header("Push Ray Ayarları")]
    public float pushRayRange = 3f;
    public float pushSphereRadius = 0.25f;

    [Tooltip("Sepet / sepet tutma için kullanılacak layer mask. Boş bırakılırsa 'Sepet' layer'ını otomatik kullanır.")]
    public LayerMask pushMask;

    public bool IsPushing { get; private set; }

    private PhotonView pv;
    private ShoppingCart pushedCart;

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

        if (playerMining == null)
        {
            playerMining = GetComponent<PlayerMining>();
        }

        if (pushMask == 0)
        {
            // Prefab sepeti 'Sepet' layer'ına koymuşsun, onu default olarak kullanıyoruz
            pushMask = LayerMask.GetMask("Sepet");
        }
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        bool holdingMouse = Input.GetMouseButton(0);

        // 🔒 Elinde maden parçası varsa sepeti ASLA tutma
        if (playerMining != null && playerMining.IsHoldingChunk)
        {
            // Her ihtimale karşı, o anda push aktifse de bırakalım
            if (IsPushing)
            {
                StopPush();
            }
            return;
        }

        if (holdingMouse)
        {
            if (!IsPushing)
            {
                TryStartPush();
            }
            // IsPushing true ise hareket hesaplamasını ShoppingCart yapıyor
        }
        else
        {
            if (IsPushing)
            {
                StopPush();
            }
        }
    }

    private void TryStartPush()
    {
        if (playerCamera == null)
            return;

        // Elinde chunk varken buraya gelmemesi gerekiyor ama ekstra koruma:
        if (playerMining != null && playerMining.IsHoldingChunk)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // Tüm çarpanları al, en yakın ShoppingCartHandle'ı seç
        RaycastHit[] hits = Physics.SphereCastAll(
            ray,
            pushSphereRadius,
            pushRayRange,
            pushMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return;

        float closest = float.MaxValue;
        ShoppingCartHandle bestHandle = null;

        foreach (var hit in hits)
        {
            var handle = hit.collider.GetComponent<ShoppingCartHandle>();
            if (handle == null || handle.cart == null)
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                bestHandle = handle;
            }
        }

        if (bestHandle == null)
            return;

        pushedCart = bestHandle.cart;
        pushedCart.BeginPush(pv);
        IsPushing = true;
    }

    private void StopPush()
    {
        if (pushedCart != null)
        {
            pushedCart.EndPush(pv);
            pushedCart = null;
        }

        IsPushing = false;
    }
}
