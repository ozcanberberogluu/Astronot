using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// E'ye basýlý tutarak kazýlabilen maden.
/// Tick baþýna OreChunk spawn eder, FX/ses/titreme oynatýr.
/// Tick hesabý sadece MasterClient'te yapýlýr, ama tüm oyuncular kazabilir.
/// </summary>
public class MineableResource : MonoBehaviourPun
{
    [Header("UI")]
    [SerializeField] private Canvas promptCanvas;   // [E] Topla canvas'ý

    [Header("Mining Ayarlarý")]
    [SerializeField] private float tickInterval = 2f;   // 2 sn'de bir tick
    [SerializeField] private int maxTicks = 5;
    [SerializeField] private GameObject oreChunkPrefab;
    [SerializeField] private int chunksPerTick = 1;
    [SerializeField] private float spawnRadius = 0.5f;
    [SerializeField] private Transform spawnOrigin;      // boþsa kendi transformu

    [Header("Model / Depletion")]
    [SerializeField] private Transform model;            // scale + shake için
    [SerializeField] private Vector3 depletedScale = new Vector3(0.2f, 0.2f, 0.2f);
    [SerializeField] private float shrinkSpeed = 4f;

    [Header("FX")]
    [Tooltip("Tick olduðunda instantiate edilecek particle prefabý.")]
    [SerializeField] private GameObject fxPrefab;
    [Tooltip("FX objesini kaç saniye sonra yok edelim? (FX kendi kendini destroy ediyorsa 0 býrak)")]
    [SerializeField] private float fxAutoDestroyTime = 3f;

    [Header("Ses")]
    [SerializeField] private AudioSource audioSource;    // maden üzerindeki AudioSource
    [SerializeField] private AudioClip mineTickClip;     // her tick'te çalýnacak ses
    [Tooltip("Yakýn mesafede tam ses için min distance.")]
    [SerializeField] private float minDistance = 4f;
    [Tooltip("Bu mesafeden sonra ses tamamen kýsýlýr.")]
    [SerializeField] private float maxDistance = 25f;

    [Header("Titreme")]
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeStrength = 0.03f;

    // STATE
    private float tickTimer;
    private int currentTicks;
    private bool depleted;

    private Coroutine shakeCoroutine;

    private void Awake()
    {
        // Spawn origin yoksa kendini kullan
        if (spawnOrigin == null)
            spawnOrigin = transform;

        // Prompt canvas'ý atlamýþsan, çocuklarda otomatik bul
        if (promptCanvas == null)
            promptCanvas = GetComponentInChildren<Canvas>(true);

        // Oyun baþýnda [E] TOPLA kapalý olsun
        if (promptCanvas != null)
            promptCanvas.enabled = false;

        // Model yoksa kendi transformunu kullan
        if (model == null)
            model = transform;

        // AudioSource atlanmýþsa üstünde ara
        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>();

        // 3D ses ayarlarý
        if (audioSource != null)
        {
            audioSource.spatialBlend = 1f; // tam 3D
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
        }
    }

    private void Update()
    {
        // Maden bitmiþse yavaþça küçült ve sonra yok et
        if (depleted && model != null)
        {
            model.localScale = Vector3.Lerp(model.localScale, depletedScale,
                shrinkSpeed * Time.deltaTime);

            if (model.localScale.magnitude <= depletedScale.magnitude + 0.01f)
            {
                if (photonView.IsMine)
                    PhotonNetwork.Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// PlayerMining her frame burayý çaðýrýr, E basýlýyken.
    /// Tick zamanlamasý sadece MasterClient'te tutulur.
    /// </summary>
    public void Mine(float deltaTime, PlayerMining miner)
    {
        // Master deðilsek, mining isteðini MasterClient'e gönder
        if (!PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(nameof(RPC_MineRequest), RpcTarget.MasterClient, deltaTime);
            return;
        }

        // MasterClient'teysek direkt iþleyelim
        ProcessMine(deltaTime);
    }

    /// <summary>
    /// Client'larýn MasterClient'e gönderdiði mining isteði.
    /// </summary>
    [PunRPC]
    private void RPC_MineRequest(float deltaTime, PhotonMessageInfo _info)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        ProcessMine(deltaTime);
    }

    /// <summary>
    /// Asýl tick / chunk / FX hesaplarýnýn yapýldýðý yer (sadece MasterClient).
    /// </summary>
    private void ProcessMine(float deltaTime)
    {
        if (depleted) return;
        if (oreChunkPrefab == null) return;

        tickTimer += deltaTime;

        if (tickTimer < tickInterval)
            return;

        tickTimer = 0f;
        currentTicks++;

        // Her tick'te herkese FX+ses+shake oynat
        photonView.RPC(nameof(RPC_OnTick), RpcTarget.All);

        // Chunk spawn sadece MasterClient'te
        SpawnChunks();

        if (currentTicks >= maxTicks)
        {
            photonView.RPC(nameof(RPC_OnDepleted), RpcTarget.AllBuffered);
        }
    }

    private void SpawnChunks()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        for (int i = 0; i < chunksPerTick; i++)
        {
            Vector3 rand = Random.insideUnitSphere;
            rand.y = Mathf.Abs(rand.y);
            rand *= spawnRadius;

            Vector3 spawnPos = spawnOrigin.position + rand;
            spawnPos.y = spawnOrigin.position.y + 0.2f;

            PhotonNetwork.Instantiate(oreChunkPrefab.name, spawnPos, Quaternion.identity);
        }
    }

    [PunRPC]
    private void RPC_OnTick()
    {
        PlayTickEffects();
    }

    [PunRPC]
    private void RPC_OnDepleted()
    {
        depleted = true;

        if (promptCanvas != null)
            promptCanvas.enabled = false;
    }

    /// <summary>
    /// [E] TOPLA world-space canvas'ýný aç/kapat.
    /// PlayerMining trigger enter/exit'te çaðýrýyor.
    /// </summary>
    public void SetPromptVisible(bool visible)
    {
        if (depleted) visible = false; // bitmiþ maden için asla gösterme

        if (promptCanvas != null)
            promptCanvas.enabled = visible;
    }

    // ================== FX / SES / SHAKE ==================

    private void PlayTickEffects()
    {
        // Ses
        if (audioSource != null && mineTickClip != null)
            audioSource.PlayOneShot(mineTickClip);

        // FX prefab
        if (fxPrefab != null && spawnOrigin != null)
        {
            GameObject fx = Instantiate(fxPrefab, spawnOrigin.position, spawnOrigin.rotation);

            if (fxAutoDestroyTime > 0f)
                Destroy(fx, fxAutoDestroyTime);
        }

        // Titreme
        if (model != null)
        {
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(ShakeRoutine());
        }
    }

    private IEnumerator ShakeRoutine()
    {
        Vector3 originalPos = model.localPosition;
        float t = 0f;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            Vector3 offset = Random.insideUnitSphere * shakeStrength;
            offset.y = Mathf.Abs(offset.y); // çok aþaðý sallanmasýn
            model.localPosition = originalPos + offset;
            yield return null;
        }

        model.localPosition = originalPos;
        shakeCoroutine = null;
    }
}
