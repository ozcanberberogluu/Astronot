using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerCartPush : MonoBehaviourPun
{
    [Header("Referanslar")]
    public Camera playerCamera;

    [Header("Push Ray Ayarlarý")]
    public float pushRayRange = 3f;
    public float pushSphereRadius = 0.25f;

    [Tooltip("Sepet / sepet tutma için kullanýlacak layer mask. Boþ býrakýlýrsa 'Sepet' layer'ýný otomatik kullanýr.")]
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

        if (pushMask == 0)
        {
            // Prefab sepeti 'Sepet' layer'ýna koymuþsun, onu default olarak kullanýyoruz
            pushMask = LayerMask.GetMask("Sepet");
        }
    }

    private void Update()
    {
        if (!pv.IsMine) return;

        bool holdingMouse = Input.GetMouseButton(0);

        if (holdingMouse)
        {
            if (!IsPushing)
            {
                TryStartPush();
            }
            // IsPushing true ise hareket hesabýný ShoppingCart zaten yapýyor
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

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // Tüm çarpanlarý al, en yakýn ShoppingCartHandle'ý seç
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
