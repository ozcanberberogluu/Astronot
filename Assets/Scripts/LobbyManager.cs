using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Photon'un Hashtable'ýný kullanmak için alias
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main UI")]
    [SerializeField] private TMP_Text statusText;

    [Header("Create Room UI")]
    [SerializeField] private TMP_InputField createRoom_RoomNameInput;
    [SerializeField] private TMP_InputField createRoom_NicknameInput;

    [Header("Join Room UI")]
    [SerializeField] private TMP_InputField joinRoom_RoomNameInput;
    [SerializeField] private TMP_InputField joinRoom_NicknameInput;

    [Header("Lobby UI")]
    [SerializeField] private TMP_Text lobby_RoomNameText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button lobbySettingsButton;
    [SerializeField] private Button leaveRoomButton;

    private const string PingKey = "ping";
    private Coroutine pingCoroutine;

    private readonly Dictionary<int, PlayerListItem> playerListItems =
        new Dictionary<int, PlayerListItem>();

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = "1.0"; // Ýstersen deðiþtir

        // Baþlangýçta sadece main panel açýk olsun
        ShowOnlyPanel(mainPanel);
        settingsPanel.SetActive(false);

        statusText.text = "Connecting to server...";
        PhotonNetwork.ConnectUsingSettings();
    }

    #region PUN Callbacks

    public override void OnConnectedToMaster()
    {
        statusText.text = "Connected. Joining lobby...";
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        statusText.text = "In lobby.";
        ShowOnlyPanel(mainPanel);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        statusText.text = $"Disconnected: {cause}";
        ShowOnlyPanel(mainPanel);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        statusText.text = $"Create room failed: {message}";
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        statusText.text = $"Join room failed: {message}";
    }

    public override void OnJoinedRoom()
    {
        statusText.text = $"Joined room: {PhotonNetwork.CurrentRoom.Name}";
        ShowOnlyPanel(lobbyPanel);

        lobby_RoomNameText.text = PhotonNetwork.CurrentRoom.Name;

        // Start Game butonu sadece master client'ta aktif
        startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);

        RefreshPlayerList();

        if (pingCoroutine != null)
            StopCoroutine(pingCoroutine);
        pingCoroutine = StartCoroutine(UpdatePingCoroutine());
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        statusText.text = $"{newPlayer.NickName} joined.";
        RefreshPlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        statusText.text = $"{otherPlayer.NickName} left.";
        RefreshPlayerList();
    }

    public override void OnLeftRoom()
    {
        statusText.text = "Left room.";

        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }

        ClearPlayerList();
        ShowOnlyPanel(mainPanel);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Master deðiþtiyse Start Game butonunu güncelle
        startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps == null)
            return;

        if (changedProps.ContainsKey(PingKey))
        {
            int ping = (int)changedProps[PingKey];

            if (playerListItems.TryGetValue(targetPlayer.ActorNumber, out var item))
            {
                item.UpdatePing(ping);
            }
        }
    }

    #endregion

    #region UI Button Methods

    // MAIN
    public void OnClick_OpenCreateRoomPanel()
    {
        ShowOnlyPanel(createRoomPanel);
        statusText.text = "";
    }

    public void OnClick_OpenJoinRoomPanel()
    {
        ShowOnlyPanel(joinRoomPanel);
        statusText.text = "";
    }

    public void OnClick_OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void OnClick_QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // CREATE ROOM PANEL
    public void OnClick_CreateRoom_Confirm()
    {
        string roomName = createRoom_RoomNameInput.text;
        string nickname = createRoom_NicknameInput.text;

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Room name cannot be empty.";
            return;
        }

        if (string.IsNullOrEmpty(nickname))
        {
            statusText.text = "Nickname cannot be empty.";
            return;
        }

        PhotonNetwork.NickName = nickname; // Ýleride buraya Steam adýný verebilirsin

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 4,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(roomName, options);
        statusText.text = "Creating room...";
    }

    public void OnClick_CreateRoom_Back()
    {
        ShowOnlyPanel(mainPanel);
    }

    // JOIN ROOM PANEL
    public void OnClick_JoinRoom_Confirm()
    {
        string roomName = joinRoom_RoomNameInput.text;
        string nickname = joinRoom_NicknameInput.text;

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Room number/name cannot be empty.";
            return;
        }

        if (string.IsNullOrEmpty(nickname))
        {
            statusText.text = "Nickname cannot be empty.";
            return;
        }

        PhotonNetwork.NickName = nickname;

        PhotonNetwork.JoinRoom(roomName);
        statusText.text = "Joining room...";
    }

    public void OnClick_JoinRoom_Back()
    {
        ShowOnlyPanel(mainPanel);
    }

    // LOBBY PANEL
    public void OnClick_StartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // AutomaticallySyncScene = true olduðu için tüm client'lar bu sahneye geçer
        PhotonNetwork.LoadLevel("GameScene");
    }

    public void OnClick_LobbySettings()
    {
        settingsPanel.SetActive(true);
    }

    public void OnClick_LeaveRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Odayý kuran çýkarken diðerlerini de at
            photonView.RPC(nameof(KickAllPlayers), RpcTarget.Others);
        }

        PhotonNetwork.LeaveRoom();
    }

    public void OnClick_CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    #endregion

    #region Helper Methods

    private void ShowOnlyPanel(GameObject panelToShow)
    {
        if (mainPanel != null)
            mainPanel.SetActive(panelToShow == mainPanel);

        if (createRoomPanel != null)
            createRoomPanel.SetActive(panelToShow == createRoomPanel);

        if (joinRoomPanel != null)
            joinRoomPanel.SetActive(panelToShow == joinRoomPanel);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(panelToShow == lobbyPanel);
        // settingsPanel ayrý açýlýp kapanýyor
    }

    private void RefreshPlayerList()
    {
        ClearPlayerList();

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject itemObj = Instantiate(playerListItemPrefab, playerListContainer);
            var item = itemObj.GetComponent<PlayerListItem>();

            int ping = 0;
            if (player.CustomProperties != null && player.CustomProperties.ContainsKey(PingKey))
                ping = (int)player.CustomProperties[PingKey];

            item.Setup(player, ping);

            playerListItems[player.ActorNumber] = item;
        }
    }

    private void ClearPlayerList()
    {
        foreach (var kvp in playerListItems)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }

        playerListItems.Clear();
    }

    private IEnumerator UpdatePingCoroutine()
    {
        var hash = new Hashtable();

        while (true)
        {
            int ping = PhotonNetwork.GetPing();
            hash[PingKey] = ping;
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            yield return new WaitForSeconds(1f);
        }
    }

    [PunRPC]
    private void KickAllPlayers()
    {
        PhotonNetwork.LeaveRoom();
    }

    #endregion
}
