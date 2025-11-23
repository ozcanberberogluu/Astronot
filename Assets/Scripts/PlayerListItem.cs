using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text pingText;
    [SerializeField] private Button kickButton;

    public int ActorNumber { get; private set; }

    private Player playerRef;
    private LobbyManager lobbyManager;

    // LobbyManager RefreshPlayerList'ten çaðýracak
    public void Setup(Player player, int ping, LobbyManager manager)
    {
        playerRef = player;
        ActorNumber = player.ActorNumber;
        lobbyManager = manager;

        playerNameText.text = string.IsNullOrEmpty(player.NickName)
            ? $"Player {player.ActorNumber}"
            : player.NickName;

        UpdatePing(ping);

        // Kick butonu sadece master'da ve kendi satýrý için görünmesin
        if (kickButton != null)
        {
            bool showKick = PhotonNetwork.IsMasterClient && !player.IsLocal;
            kickButton.gameObject.SetActive(showKick);

            kickButton.onClick.RemoveAllListeners();
            if (showKick)
            {
                kickButton.onClick.AddListener(OnKickButtonClicked);
            }
        }
    }

    public void UpdatePing(int ping)
    {
        if (pingText != null)
            pingText.text = $"{ping} ms";
    }

    private void OnKickButtonClicked()
    {
        if (lobbyManager != null)
        {
            lobbyManager.KickPlayer(ActorNumber);
        }
    }
}
