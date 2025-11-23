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
    }

    private void SpawnPlayer()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("No spawn points assigned in GameManager!");
            return;
        }

        // Her oyuncuya actorNumber'a göre farklý spawn
        int index = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
        Transform spawn = spawnPoints[index];

        PhotonNetwork.Instantiate(playerPrefabName, spawn.position, spawn.rotation);
    }
}
