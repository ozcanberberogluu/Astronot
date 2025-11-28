using System.Collections;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class StormManager : MonoBehaviourPunCallbacks
{
    public static StormManager Instance { get; private set; }

    [Header("Storm Timing")]
    [Tooltip("Bir fýrtýna döngüsünün toplam süresi (saniye olarak). Örn: 60 = her 60 saniyede bir fýrtýna baþlar.")]
    public float stormInterval = 60f;

    [Tooltip("Fýrtýnanýn aktif kaldýðý süre (saniye). Örn: 10 = 10 saniye fýrtýna.")]
    public float stormDuration = 10f;

    [Tooltip("Oyun baþlarken ilk fýrtýnaya kadar olan bekleme süresi.")]
    public float firstStormDelay = 5f;

    [Header("Wind Settings")]
    [Tooltip("Rüzgarýn minimum þiddeti (yatay hýz).")]
    public float minWindStrength = 8f;

    [Tooltip("Rüzgarýn maksimum þiddeti (yatay hýz).")]
    public float maxWindStrength = 16f;

    [Tooltip("Fýrtýna esnasýnda ekstra yukarý kaldýrma gücü.")]
    public float stormUpwardForce = 2f;

    private bool isStormActive = false;
    private Vector3 windDirection = Vector3.zero;
    private float windStrength = 0f;

    private Coroutine loopRoutine;

    public bool IsStormActive => isStormActive;

    /// <summary> Yatay rüzgar vektörü (yön * þiddet). </summary>
    public Vector3 CurrentWindHorizontal => windDirection * windStrength;

    /// <summary> Fýrtýna sýrasýnda PlayerController'larýn kullanacaðý ekstra yukarý güç. </summary>
    public float CurrentStormUpwardForce => isStormActive ? stormUpwardForce : 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        TryStartStormLoop();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        // Master deðiþtiðinde yeni master fýrtýna döngüsünü devralsýn
        TryStartStormLoop();
    }

    private void TryStartStormLoop()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            return;

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (loopRoutine != null)
            StopCoroutine(loopRoutine);

        loopRoutine = StartCoroutine(StormLoopCoroutine());
    }

    private IEnumerator StormLoopCoroutine()
    {
        // Ýlk fýrtýna öncesi bekleme
        yield return new WaitForSeconds(firstStormDelay);

        while (true)
        {
            // Fýrtýna yokken geçen süre: interval - duration (örn: 60 - 10 = 50 saniye sakin)
            float calmTime = Mathf.Max(0f, stormInterval - stormDuration);
            if (calmTime > 0f)
                yield return new WaitForSeconds(calmTime);

            // Fýrtýna baþlat
            StartStormForAll();

            // Fýrtýna süresi
            yield return new WaitForSeconds(stormDuration);

            // Fýrtýna bitir
            EndStormForAll();
        }
    }

    private void StartStormForAll()
    {
        // Rastgele yatay rüzgar yönü
        Vector2 rand = Random.insideUnitCircle.normalized;
        Vector3 dir = new Vector3(rand.x, 0f, rand.y);
        float strength = Random.Range(minWindStrength, maxWindStrength);

        photonView.RPC(nameof(RPC_StartStorm), RpcTarget.All, dir, strength);
    }

    private void EndStormForAll()
    {
        photonView.RPC(nameof(RPC_EndStorm), RpcTarget.All);
    }

    [PunRPC]
    private void RPC_StartStorm(Vector3 dir, float strength)
    {
        isStormActive = true;
        windDirection = dir.normalized;
        windStrength = strength;

        // Ýstersen burada global bir fýrtýna sesi / ekran efekti vs. de tetikleyebilirsin
        Debug.Log($"[Storm] Fýrtýna baþladý. Yön: {windDirection}, Þiddet: {windStrength}");
    }

    [PunRPC]
    private void RPC_EndStorm()
    {
        isStormActive = false;
        windDirection = Vector3.zero;
        windStrength = 0f;

        Debug.Log("[Storm] Fýrtýna bitti.");
    }
}
