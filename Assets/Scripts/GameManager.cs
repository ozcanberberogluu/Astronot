using UnityEngine;
using Photon.Pun;

public class GameManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private string playerPrefabName = "Player";

    private void Start()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("Not connected to a room. Cannot spawn player.");
            return;
        }

        SpawnPlayer();
        SetupSpaceshipRadarTarget();
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

        // Her oyuncuya actorNumber'a g�re farkl� spawn
        int index = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
        Transform spawn = spawnPoints[index];

        PhotonNetwork.Instantiate(playerPrefabName, spawn.position, spawn.rotation);
    }
}
