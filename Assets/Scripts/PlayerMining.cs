using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class PlayerMining : MonoBehaviourPun
{
    [Header("Inventory")]
    public float maxBagPercent = 100f;
    public float currentBagPercent = 0f;
    public int currentBagValue = 0; // çantadaki para

    [Header("UI")]
    public TMP_Text bagPercentText;   // "Çanta: %32"
    public TMP_Text bagInfoText;      // "Çanta doldu!", "+7 para" vs.

    [Header("Input / Mining")]
    public KeyCode interactKey = KeyCode.E;
    public float mineInterval = 3f;   // E'yi basýlý tutunca her 3 saniyede bir kaz

    private MineableResource currentResource;
    private bool isInBaseZone = false;

    private Animator animator;
    private static readonly int IsMiningHash = Animator.StringToHash("IsMining");

    private float mineTimer = 0f;
    private Coroutine infoCoroutine;

    private GameManager gameManager;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        gameManager = FindObjectOfType<GameManager>();
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
            UpdateBagUI();
        }
    }

    private void Update()
    {
        if (!photonView.IsMine)
            return;

        HandleMiningInput();
        HandleBaseInput();
    }

    private void HandleMiningInput()
    {
        bool playMiningAnim = false;

        if (currentResource != null && !IsBagFull())
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

    private void HandleBaseInput()
    {
        if (!isInBaseZone)
            return;

        if (currentBagValue <= 0)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            if (gameManager != null)
            {
                gameManager.AddCash(currentBagValue);
            }

            currentBagValue = 0;
            currentBagPercent = 0f;
            ShowInfo("Çanta boþaltýldý.");
            UpdateBagUI();
        }
    }

    private void DoMineTick()
    {
        if (currentResource == null)
            return;

        float usage = currentResource.GetBagUsagePercent();

        // Çanta doluluðunu kontrol et
        if (currentBagPercent + usage > maxBagPercent + 0.01f)
        {
            currentBagPercent = maxBagPercent;
            ShowInfo("Çanta doldu!");
            UpdateBagUI();
            return;
        }

        // Maden tarafýnda stoðu düþür (tick)
        if (currentResource.TryMineOnce(out int gainedValue, out float _))
        {
            currentBagPercent += usage;
            currentBagPercent = Mathf.Clamp(currentBagPercent, 0f, maxBagPercent);

            currentBagValue += gainedValue;
            UpdateBagUI();
            ShowInfo($"+{gainedValue} para");

            // TryMineOnce içinde:
            // - remainingValue düþüyor
            // - bittiðinde MineAndDisable
            // - her seferinde PlayMineFeedback => FX + ses + shake (RPC ile herkese)
        }
        else
        {
            // Maden zaten bitmiþ olabilir
            currentResource.ShowPrompt(false);
            currentResource = null;
        }
    }

    private bool IsBagFull()
    {
        return currentBagPercent >= maxBagPercent - 0.01f;
    }

    private void SetMiningAnimation(bool active)
    {
        if (animator == null) return;

        bool current = animator.GetBool(IsMiningHash);
        if (current == active) return;

        animator.SetBool(IsMiningHash, active);
        // IsMining parametresi PhotonAnimatorView'da Discrete olarak sync edilmeli
    }

    private void UpdateBagUI()
    {
        if (bagPercentText != null)
        {
            int percentInt = Mathf.RoundToInt(currentBagPercent);
            bagPercentText.text = $"Çanta: %{percentInt}";
        }
    }

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

        // Base
        DepositZone depot = other.GetComponent<DepositZone>();
        if (depot != null)
        {
            isInBaseZone = true;
            if (currentBagValue > 0)
                ShowInfo("[E] Çantayý boþalt");
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
