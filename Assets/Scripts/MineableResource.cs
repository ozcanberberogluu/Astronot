using System.Collections;
using UnityEngine;
using Photon.Pun;

public enum OreType
{
    Diamond,
    Gold,
    Silver,
    Iron
}

public class MineableResource : MonoBehaviourPun
{
    public OreType oreType;

    [Header("Amounts")]
    [Tooltip("Bu damarýn toplam para deðeri (tamamen bitene kadar).")]
    public int totalValue = 65;

    [Tooltip("Her kazma tick'inde üretilecek maden parçasýnýn deðeri.")]
    public int valuePerChunk = 7;

    [Header("Chunk Spawn")]
    [Tooltip("Bu damar kýrýldýðýnda düþecek parça prefabý (Resources klasöründe olmalý).")]
    public GameObject oreChunkPrefab;

    [Tooltip("Parçalarýn saçýlacaðý yarýçap.")]
    public float scatterRadius = 1.5f;

    [Tooltip("Parçalar spawnlanýrken yukarýya verilecek küçük hýz.")]
    public float spawnUpImpulse = 2f;

    [Header("UI")]
    public Canvas worldCanvas;             // [E] Topla canvas'ý

    [Header("FX & Audio")]
    [Tooltip("FX'in çýkacaðý nokta. Boþ býrakýlýrsa madenin kendi pozisyonu kullanýlýr.")]
    public Transform fxSpawnPoint;
    public GameObject mineFxPrefab;
    public AudioClip mineAudioClip;
    public float shakeDuration = 0.15f;
    public float shakeMagnitude = 0.05f;
    public float soundMinDistance = 3f;
    public float soundMaxDistance = 30f;

    private int remainingValue;
    private bool isDepleted = false;

    private void Awake()
    {
        remainingValue = totalValue;

        if (worldCanvas != null)
            worldCanvas.enabled = false;
    }

    // Eski sistemden kalan, þimdilik çanta ile iþimiz yok ama çaðrýlan yerde sorun çýkarmasýn diye býrakýyoruz.
    public float GetBagUsagePercent()
    {
        // Artýk çanta doluluðu yok, istersen burada 0 dönebiliriz.
        return 0f;
    }

    /// <summary>
    /// Bir kazma tick'i. Baþarýlýysa true döner ve üretilen parça deðeri valueOut'a yazýlýr.
    /// Maden tamamen bittiyse kendi içinde disable olur.
    /// </summary>
    public bool TryMineOnce(out int valueOut, out float percentOut)
    {
        valueOut = 0;
        percentOut = 0f;

        if (isDepleted || remainingValue <= 0)
            return false;

        int chunkValue = Mathf.Min(valuePerChunk, remainingValue);
        remainingValue -= chunkValue;

        valueOut = chunkValue;

        SpawnOreChunk(chunkValue);

        if (remainingValue <= 0)
        {
            MineAndDisable();
        }

        // Her baþarýlý tick'te FX/ses/titreþim oynat
        PlayMineFeedback();

        return true;
    }

    private void SpawnOreChunk(int chunkValue)
    {
        if (oreChunkPrefab == null)
        {
            Debug.LogWarning("MineableResource: oreChunkPrefab atanmadý!");
            return;
        }

        // Rastgele yanlara saçýlma
        Vector2 rand = Random.insideUnitCircle * scatterRadius;
        Vector3 spawnPos = transform.position + new Vector3(rand.x, 0.5f, rand.y);

        GameObject obj;

        if (PhotonNetwork.IsConnected)
        {
            // Resources klasöründe olmalý
            obj = PhotonNetwork.Instantiate(oreChunkPrefab.name, spawnPos, Quaternion.identity);
        }
        else
        {
            obj = Instantiate(oreChunkPrefab, spawnPos, Quaternion.identity);
        }

        OreChunk chunk = obj.GetComponent<OreChunk>();
        if (chunk != null)
        {
            chunk.Initialize(oreType, chunkValue);
        }

        // Hafif yukarý fýrlatma
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 upImpulse = Vector3.up * spawnUpImpulse;
            rb.AddForce(upImpulse, ForceMode.Impulse);
        }
    }

    public void ShowPrompt(bool show)
    {
        if (worldCanvas != null && !isDepleted)
            worldCanvas.enabled = show;
    }

    public void MineAndDisable()
    {
        if (isDepleted) return;

        isDepleted = true;

        if (photonView != null)
            photonView.RPC(nameof(RPC_DisableResource), RpcTarget.AllBuffered);
        else
            RPC_DisableResource();
    }

    [PunRPC]
    private void RPC_DisableResource()
    {
        if (worldCanvas != null)
            worldCanvas.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        // Ýstersen tamamen yok etmek için:
        // Destroy(gameObject);
    }

    // -------- FX + SES + SHAKE --------

    /// <summary>
    /// Tüm client'larda FX + ses + titreþim oynat.
    /// </summary>
    public void PlayMineFeedback()
    {
        if (photonView != null)
        {
            photonView.RPC(nameof(RPC_PlayMineFeedback), RpcTarget.All);
        }
        else
        {
            StartCoroutine(PlayMineFeedbackRoutine());
        }
    }

    [PunRPC]
    private void RPC_PlayMineFeedback()
    {
        StartCoroutine(PlayMineFeedbackRoutine());
    }

    private IEnumerator PlayMineFeedbackRoutine()
    {
        Vector3 pos = fxSpawnPoint != null ? fxSpawnPoint.position : transform.position;

        // FX
        if (mineFxPrefab != null)
        {
            GameObject fx = Instantiate(mineFxPrefab, pos, Quaternion.identity);
            Destroy(fx, 3f); // FX kendi kendini yok etmiyorsa
        }

        // SES (3D)
        if (mineAudioClip != null)
        {
            GameObject go = new GameObject("MineOneShotAudio");
            go.transform.position = pos;

            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = mineAudioClip;
            source.spatialBlend = 1f;             // %100 3D
            source.minDistance = soundMinDistance;
            source.maxDistance = soundMaxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();

            Destroy(go, mineAudioClip.length + 0.1f);
        }

        // TÝTREÞÝM
        if (shakeDuration > 0f && shakeMagnitude > 0f)
        {
            Vector3 originalLocalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                Vector3 randomOffset = Random.insideUnitSphere * shakeMagnitude;
                randomOffset.y = 0f; // Ýstersen Y'i sabit býrak
                transform.localPosition = originalLocalPos + randomOffset;
                yield return null;
            }

            transform.localPosition = originalLocalPos;
        }
    }
}
