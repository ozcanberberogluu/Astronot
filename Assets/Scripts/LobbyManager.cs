using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Photon Hashtable alias
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
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
        PhotonNetwork.GameVersion = "1.0";

        ShowOnlyPanel(mainPanel);
        settingsPanel.SetActive(false);

        statusText.text = "Connecting to server...";
        PhotonNetwork.ConnectUsingSettings();
    }

    #region PUN CALLBACKS

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
        if (returnCode == (short)ErrorCode.GameIdAlreadyExists)
        {
            statusText.text = "Code conflict. Creating new code...";
            CreateRoomWithRandomCode();
        }
        else
        {
            statusText.text = $"Create room failed: {message}";
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (returnCode == (short)ErrorCode.GameDoesNotExist)
        {
            statusText.text = "Room not found. Check the code.";
        }
        else
        {
            statusText.text = $"Join room failed: {message}";
        }
    }

    public override void OnJoinedRoom()
    {
        ShowOnlyPanel(lobbyPanel);

        string roomCode = PhotonNetwork.CurrentRoom.Name;
        lobby_RoomNameText.text = $"Room: {roomCode}";

        startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);

        RefreshPlayerList();

        if (pingCoroutine != null)
            StopCoroutine(pingCoroutine);

        pingCoroutine = StartCoroutine(UpdatePingCoroutine());
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
    }

    public override void OnLeftRoom()
    {
        if (pingCoroutine != null)
            StopCoroutine(pingCoroutine);

        ClearPlayerList();
        ShowOnlyPanel(mainPanel);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        // master deðiþtiyse KickButton görünürlüklerini de güncelle
        RefreshPlayerList();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PingKey) &&
            playerListItems.TryGetValue(targetPlayer.ActorNumber, out var item))
        {
            item.UpdatePing((int)changedProps[PingKey]);
        }
    }

    #endregion

    #region BUTTON METHODS

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

    public void OnClick_CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void OnClick_QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // CREATE ROOM
    public void OnClick_CreateRoom_Confirm()
    {
        string nickname = createRoom_NicknameInput.text;

        if (string.IsNullOrEmpty(nickname))
        {
            statusText.text = "Nickname cannot be empty.";
            return;
        }

        PhotonNetwork.NickName = nickname;
        CreateRoomWithRandomCode();
    }

    public void OnClick_CreateRoom_Back()
    {
        ShowOnlyPanel(mainPanel);
    }

    // JOIN ROOM
    public void OnClick_JoinRoom_Confirm()
    {
        string roomCode = joinRoom_RoomNameInput.text;
        string nickname = joinRoom_NicknameInput.text;

        if (!IsValidRoomCode(roomCode))
        {
            statusText.text = "Room code must be 7 digits.";
            return;
        }

        if (string.IsNullOrEmpty(nickname))
        {
            statusText.text = "Nickname cannot be empty.";
            return;
        }

        PhotonNetwork.NickName = nickname;
        PhotonNetwork.JoinRoom(roomCode);

        statusText.text = $"Joining room {roomCode}...";
    }

    public void OnClick_JoinRoom_Back()
    {
        ShowOnlyPanel(mainPanel);
    }

    // LOBBY
    public void OnClick_StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("GameScene");
        }
    }

    public void OnClick_LobbySettings()
    {
        settingsPanel.SetActive(true);
    }

    public void OnClick_LeaveRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(nameof(KickAllPlayers), RpcTarget.Others);
        }

        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region HELPERS

    private void ShowOnlyPanel(GameObject panelToShow)
    {
        mainPanel.SetActive(panelToShow == mainPanel);
        createRoomPanel.SetActive(panelToShow == createRoomPanel);
        joinRoomPanel.SetActive(panelToShow == joinRoomPanel);
        lobbyPanel.SetActive(panelToShow == lobbyPanel);
    }

    private string GenerateRoomCode()
    {
        return Random.Range(1000000, 10000000).ToString(); // 7 haneli
    }

    private void CreateRoomWithRandomCode()
    {
        string code = GenerateRoomCode();

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 4,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.CreateRoom(code, options);
        statusText.text = $"Creating room... Code: {code}";
    }

    private bool IsValidRoomCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 7)
            return false;

        foreach (char c in code)
            if (!char.IsDigit(c)) return false;

        return true;
    }

    private IEnumerator UpdatePingCoroutine()
    {
        var hash = new Hashtable();

        while (true)
        {
            hash[PingKey] = PhotonNetwork.GetPing();
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);

            yield return new WaitForSeconds(1f);
        }
    }

    private void RefreshPlayerList()
    {
        ClearPlayerList();

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject obj = Instantiate(playerListItemPrefab, playerListContainer);
            var item = obj.GetComponent<PlayerListItem>();

            int ping = player.CustomProperties.ContainsKey(PingKey)
                ? (int)player.CustomProperties[PingKey]
                : 0;

            // BURADA this geçiriyoruz ki KickPlayer çaðrýlabilsin
            item.Setup(player, ping, this);

            playerListItems[player.ActorNumber] = item;
        }
    }

    private void ClearPlayerList()
    {
        foreach (var kv in playerListItems)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }

        playerListItems.Clear();
    }

    // Master'ýn tek bir oyuncuyu atmasý için
    public void KickPlayer(int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out Player targetPlayer))
        {
            photonView.RPC(nameof(ForceKick), targetPlayer);
        }
    }

    [PunRPC]
    private void ForceKick()
    {
        PhotonNetwork.LeaveRoom();
    }

    [PunRPC]
    private void KickAllPlayers()
    {
        PhotonNetwork.LeaveRoom();
    }

    #endregion
}
