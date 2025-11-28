using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class PlayerMining : MonoBehaviourPun
{
    [Header("UI")]
    public TMP_Text bagPercentText;   // Artık kullanılmıyor, istersen Inspector'da boş bırak
    public TMP_Text bagInfoText;      // Info mesajları için kullanıyoruz

    [Header("Input / Mining")]
    public KeyCode interactKey = KeyCode.E;
    public float mineInterval = 3f;   // E'yi basılı tutunca her 3 saniyede bir kaz

    [Header("Grab Settings")]
    public float grabRange = 8f;
    public float grabSphereRadius = 0.5f;
    public float maxGrabDistance = 12f;

    private MineableResource currentResource;
    private bool isInBaseZone = false; // Şimdilik kullanılmıyor, sepet sisteminde kullanacağız

    private Animator animator;
    private static readonly int IsMiningHash = Animator.StringToHash("IsMining");

    private float mineTimer = 0f;
    private Coroutine infoCoroutine;

    private GameManager gameManager;
    private Camera playerCamera;
    private OreChunk grabbedChunk;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        gameManager = FindObjectOfType<GameManager>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
    }

    private void Start()
    {
        if (!photonView.IsMine)
        {
            if (bagPercentText != null) bagPercentText.gameObject.SetActive(false);
            if (bagInfoText != null) bagInfoText.gameObject.SetActive(false);
        }
        else
        {
            // Eski çanta UI'sini sıfırla
            if (bagPercentText != null)
                bagPercentText.text = "";
        }
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        HandleMiningInput();
        HandleGrabInput();
        // HandleBaseInput();  // çanta sistemi kalktığı için şimdilik kullanmıyoruz
    }

    // --------- MADEN KAZMA ---------

    private void HandleMiningInput()
    {
        bool playMiningAnim = false;

        if (currentResource != null)
        {
            bool holdingE = Input.GetKey(interactKey);

            if (holdingE)
            {
                playMiningAnim = true;
                mineTimer += Time.deltaTime;

                if (mineTimer >= mineInterval)
                {
                    mineTimer -= mineInterval;
                    DoMineTick();
                }
            }
            else
            {
                mineTimer = 0f;
            }
        }
        else
        {
            mineTimer = 0f;
        }

        SetMiningAnimation(playMiningAnim);
    }

    private void DoMineTick()
    {
        if (currentResource == null)
            return;

        // Çanta sistemi yok, sadece maden parçası üretilecek
        if (currentResource.TryMineOnce(out int chunkValue, out float _))
        {
            ShowInfo($"+{chunkValue} değerinde maden parçası");
        }
        else
        {
            // Maden bitmiş olabilir
            currentResource.ShowPrompt(false);
            currentResource = null;
        }
    }

    private void SetMiningAnimation(bool active)
    {
        if (animator == null) return;

        bool current = animator.GetBool(IsMiningHash);
        if (current == active) return;

        animator.SetBool(IsMiningHash, active);
        // IsMining parametresi PhotonAnimatorView'da Discrete olarak sync edilmeli
    }

    // --------- MADEN PARÇASINI TUTMA (GRAB) ---------

    private void HandleGrabInput()
    {
        if (playerCamera == null)
            return;

        bool holdingMouse = Input.GetMouseButton(0);

        if (holdingMouse)
        {
            if (grabbedChunk == null)
            {
                // İlk kez basıldıysa, bakılan maden parçasını bul
                TryStartGrab();
            }
        }
        else
        {
            if (grabbedChunk != null)
            {
                // Mouse bırakıldı, chunk'ı bırak
                grabbedChunk.EndGrab(photonView);
                grabbedChunk = null;
            }
        }
        // Hareket ettirme işini OreChunk kendi Update'inde yapıyor
    }

    private void TryStartGrab()
    {
        // 1) Önce raycast
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.SphereCast(ray, grabSphereRadius, out RaycastHit hit, grabRange))
        {
            OreChunk chunk = hit.collider.GetComponentInParent<OreChunk>();
            if (chunk != null)
            {
                grabbedChunk = chunk;
                grabbedChunk.BeginGrab(photonView);
                return;
            }
        }

        // 2) Raycast olmadı → OverlapSphere ile bul
        Collider[] cols = Physics.OverlapSphere(
            playerCamera.transform.position + playerCamera.transform.forward * 2f,
            1f
        );

        float bestDist = float.MaxValue;
        OreChunk best = null;

        foreach (var c in cols)
        {
            OreChunk chunk = c.GetComponentInParent<OreChunk>();
            if (chunk == null) continue;

            float d = Vector3.Distance(transform.position, chunk.transform.position);
            if (d < bestDist && d < maxGrabDistance)
            {
                bestDist = d;
                best = chunk;
            }
        }

        if (best != null)
        {
            grabbedChunk = best;
            grabbedChunk.BeginGrab(photonView);
        }
    }


    // --------- BASE / DEPOSIT ---------
    // Çanta sistemi kalktığı için şimdilik base'de bir şey yapmıyoruz.
    // Market arabası sistemini eklerken burayı sepet boşaltma için kullanacağız.

    /*
    private void HandleBaseInput()
    {
        if (!isInBaseZone)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            // İleride: market arabasını boşalt
        }
    }
    */

    // --------- UI YARDIMCI ---------

    private void ShowInfo(string msg)
    {
        if (bagInfoText == null) return;

        bagInfoText.text = msg;

        if (infoCoroutine != null)
            StopCoroutine(infoCoroutine);

        infoCoroutine = StartCoroutine(ClearInfoAfterDelay());
    }

    private System.Collections.IEnumerator ClearInfoAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (bagInfoText != null)
            bagInfoText.text = "";

        infoCoroutine = null;
    }

    // -------- TRIGGERLAR --------

    private void OnTriggerEnter(Collider other)
    {
        if (!photonView.IsMine)
            return;

        // Maden
        MineableResource res = other.GetComponent<MineableResource>();
        if (res != null)
        {
            currentResource = res;
            currentResource.ShowPrompt(true);
            return;
        }

        // Base (ileride sepet için kullanacağız)
        DepositZone depot = other.GetComponent<DepositZone>();
        if (depot != null)
        {
            isInBaseZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!photonView.IsMine)
            return;

        MineableResource res = other.GetComponent<MineableResource>();
        if (res != null && res == currentResource)
        {
            currentResource.ShowPrompt(false);
            currentResource = null;
            return;
        }

        DepositZone depot = other.GetComponent<DepositZone>();
        if (depot != null)
        {
            isInBaseZone = false;
        }
    }
}
