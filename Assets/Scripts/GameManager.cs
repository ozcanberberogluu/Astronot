using UnityEngine;
using Photon.Pun;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private string playerPrefabName = "Player";

    [Header("Mining / Cash")]
    [SerializeField] private TMP_Text totalCashText; // "Kasa: X para"
    private int totalCash = 0;

    [Header("Cart")]
    [SerializeField] private string cartPrefabName = "SepetModel";
    [SerializeField] private Transform cartSpawnPoint;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("Not connected to a room. Cannot spawn player.");
            return;
        }

        SpawnPlayer();

        // Oda başına sadece 1 sepet: MasterClient oluşturur
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnCart();
        }

        SetupSpaceshipRadarTarget();
        UpdateTotalCashUI();
    }

    private void SetupSpaceshipRadarTarget()
    {
        // Sahnedeki uzay mekiğini bul ve RadarTarget ekle
        GameObject spaceship = GameObject.Find("Mekik");
        if (spaceship != null)
        {
            RadarTarget radarTarget = spaceship.GetComponent<RadarTarget>();
            if (radarTarget == null)
            {
                radarTarget = spaceship.AddComponent<RadarTarget>();
                radarTarget.type = RadarTargetType.Ship;
            }
        }
    }

    private void SpawnPlayer()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("No spawn points assigned in GameManager!");
            return;
        }

        int index = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
        Transform spawn = spawnPoints[index];

        PhotonNetwork.Instantiate(playerPrefabName, spawn.position, spawn.rotation);
    }

    private void SpawnCart()
    {
        if (string.IsNullOrEmpty(cartPrefabName))
            return;

        Vector3 pos;
        Quaternion rot;

        if (cartSpawnPoint != null)
        {
            pos = cartSpawnPoint.position;
            rot = cartSpawnPoint.rotation;
        }
        else
        {
            // Eğer özel spawn point atamazsan, ilk oyuncu spawn noktasının önüne koy
            Transform spawn = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[0]
                : null;

            if (spawn != null)
            {
                pos = spawn.position + spawn.forward * 4f;
                rot = spawn.rotation;
            }
            else
            {
                pos = Vector3.zero;
                rot = Quaternion.identity;
            }
        }

        PhotonNetwork.Instantiate(cartPrefabName, pos, rot);
    }

    // ---------- KASA SİSTEMİ ----------

    [PunRPC]
    private void RPC_AddCash(int amount)
    {
        if (amount <= 0) return;

        totalCash += amount;
        UpdateTotalCashUI();
    }

    private void UpdateTotalCashUI()
    {
        if (totalCashText != null)
        {
            totalCashText.text = $"Kasa: {totalCash} para";
        }
    }

    // Oyuncular burayı çağırıyor
    public void AddCash(int amount)
    {
        if (amount <= 0) return;

        // BURADA null alan aslında photonView'du, şimdi bu script üzerinde PhotonView olduğundan sorun kalmıyor
        photonView.RPC(nameof(RPC_AddCash), RpcTarget.All, amount);
    }
}
