using System.Collections;
using UnityEngine;
using Photon.Pun;

public class MeteorSpawner : MonoBehaviourPunCallbacks
{
    [Header("General")]
    [Tooltip("Meteor yaðmuru aktif olsun mu?")]
    public bool enableMeteorRain = true;

    [Tooltip("Meteor prefab'inin Resources içindeki adý.")]
    public string meteorPrefabName = "Meteor";

    [Tooltip("Ýki meteor arasýndaki süre (saniye).")]
    public float meteorInterval = 50f;

    [Tooltip("Oyuna girince ilk meteor için gecikme.")]
    public float firstMeteorDelay = 10f;

    [Header("Spawn Area")]
    [Tooltip("Meteorlarýn düþeceði alanýn merkezi.")]
    public Transform areaCenter;

    [Tooltip("X/Z düzleminde yarýçap (daire) veya yarý boyut (kare gibi düþünebilirsin).")]
    public float areaRadius = 50f;

    [Tooltip("Meteorun spawn yüksekliði (yukarýda ne kadar dursun).")]
    public float spawnHeight = 40f;

    private Coroutine spawnRoutine;

    private void Start()
    {
        TryStartSpawning();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        // Master deðiþirse yeni master meteor yaðmurunu devralsýn
        TryStartSpawning();
    }

    private void TryStartSpawning()
    {
        if (!enableMeteorRain) return;

        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            return;

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(MeteorSpawnLoop());
    }

    private IEnumerator MeteorSpawnLoop()
    {
        // Ýlk gecikme
        yield return new WaitForSeconds(firstMeteorDelay);

        while (enableMeteorRain)
        {
            SpawnMeteor();
            yield return new WaitForSeconds(meteorInterval);
        }
    }

    private void SpawnMeteor()
    {
        if (areaCenter == null)
        {
            Debug.LogWarning("MeteorSpawner: areaCenter atanmadý!");
            return;
        }

        // Harita içinde rastgele bir XZ koordinatý
        Vector2 randCircle = Random.insideUnitCircle * areaRadius;
        Vector3 spawnPos = new Vector3(
            areaCenter.position.x + randCircle.x,
            areaCenter.position.y + spawnHeight,
            areaCenter.position.z + randCircle.y
        );

        // Meteorun düþeceði hedef nokta: ayný XZ, merkezin yüksekliði (yaklaþýk yer seviyesi)
        Vector3 targetPos = new Vector3(
            spawnPos.x,
            areaCenter.position.y,
            spawnPos.z
        );

        Vector3 dir = (targetPos - spawnPos).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        PhotonNetwork.Instantiate(meteorPrefabName, spawnPos, rot);
    }
}
