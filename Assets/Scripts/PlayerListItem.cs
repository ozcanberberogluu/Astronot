using TMPro;
using UnityEngine;
using Photon.Realtime;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text pingText;

    public int ActorNumber { get; private set; }

    public void Setup(Player player, int ping)
    {
        ActorNumber = player.ActorNumber;
        playerNameText.text = string.IsNullOrEmpty(player.NickName)
            ? $"Player {player.ActorNumber}"
            : player.NickName;

        UpdatePing(ping);
    }

    public void UpdatePing(int ping)
    {
        if (pingText != null)
            pingText.text = $"{ping} ms";
    }
}
