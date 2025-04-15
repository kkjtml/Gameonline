using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFSW.QC;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

public class LobbyManagerScript : Singleton<LobbyManagerScript>
{
    private Lobby hostLobby;
    private Lobby joinedLobby;
    private string playerName;
    private float lobbyUpdateTimer;
    public Button listLobbiesButton; // ปุ่มกดเพื่อเรียกดู Lobby
    public GameObject lobbyEntryPrefab; // Prefab ที่ใช้แสดงรายการ Lobby
    public Transform lobbiesContent; // Content ของ Scroll View
                                     // public TMP_InputField joinCodeInputField; // ช่องใส่รหัส
                                     // public Button joinByCodeButton; // ปุ่มกด Join Lobby
    
    public GameObject panelMain;
    public GameObject panelRoom;

    private void Start()
    {
        playerName = "myName " + Random.Range(1, 999);
        Debug.Log("Player name : " + playerName);
        listLobbiesButton.onClick.AddListener(() => ListLobbies());
    }

    private void Update()
    {
        HandleLobbyPollForUpdates();
    }

    private async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer <= 0f)
            {
                float lobbyUpdateTimerMax = 1.1f;
                lobbyUpdateTimer = lobbyUpdateTimerMax;
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;
            }
        }
    }

    [Command]
    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "MyLobby " + Random.Range(1, 999);
            int maxPlayer = 5;

            // ✅ ขอ Allocation จาก Relay
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayer);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // ✅ ใส่ข้อมูล Player + Relay Join Code เข้าไปใน Lobby
            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {"PlayerName",
                            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
                    }
                },
                Data = new Dictionary<string, DataObject>
                {
                    {"GameMode", new DataObject(DataObject.VisibilityOptions.Public, "DeathMatch") },
                    {"JoinCodeKey", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayer, createLobbyOptions);
            hostLobby = lobby;
            joinedLobby = hostLobby;

            StartCoroutine(HeartbeatLobbyCoroutine(hostLobby.Id, 15));

            // ✅ ตั้งค่า Relay Server ให้กับ NetworkManager
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>()
                .SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();  // เริ่ม Host เกม
            Debug.Log("Join Code = " + joinCode);

            PrintPlayers(hostLobby);

            // ✅ สลับ Panel
            panelMain.SetActive(false);
            panelRoom.SetActive(true);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("CreateLobby failed: " + e);
        }
    }

    public void PrintPlayers(Lobby lobby)
    {
        Debug.Log("Players in Lobby: " + lobby.Name);
        foreach (var player in lobby.Players)
        {
            if (player.Data != null && player.Data.ContainsKey("PlayerName"))
            {
                Debug.Log(player.Id + " : " + player.Data["PlayerName"].Value);
            }
        }
    }

    [Command]   
    private async void JoinLobby()
    {
        try
        {
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            await Lobbies.Instance.JoinLobbyByIdAsync(queryResponse.Results[0].Id);
            Debug.Log("Joined Lobby : " + queryResponse.Results[0].Name + "," +
                      queryResponse.Results[0].AvailableSlots);
        }
        catch (LobbyServiceException e) { Debug.Log(e); }
    }

    [Command]
    public async void JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {"PlayerName",
                            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member,playerName)}
                    }
                }
            };
            Lobby lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions);
            joinedLobby = lobby;
            Debug.Log("Joined Lobby with code : " + lobbyCode);
            PrintPlayers(joinedLobby);
        }
        catch (LobbyServiceException e) { Debug.Log(e); }
    }

    [Command]
    public async void QuickJoinLobby()
    {
        try
        {
            Lobby lobby = await Lobbies.Instance.QuickJoinLobbyAsync();
            Debug.Log(lobby.Name + "," + lobby.AvailableSlots);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private static IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    [Command]
    private async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;
            // Filter for open lobbies only
            options.Filters = new List<QueryFilter>(){
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };
            // Order by newest lobbies first
            options.Order = new List<QueryOrder>(){
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);
            // ลบรายการเก่าทั้งหมดก่อนแสดงผลใหม่
            foreach (Transform child in lobbiesContent)
            {
                Destroy(child.gameObject);
            }

            Debug.Log("Lobbies found : " + queryResponse.Results.Count);
            foreach (Lobby lobby in queryResponse.Results)
            {
                GameObject newEntry = Instantiate(lobbyEntryPrefab, lobbiesContent);
                TextMeshProUGUI entryText = newEntry.GetComponentInChildren<TextMeshProUGUI>();

                if (entryText != null)
                {
                    entryText.text = $"{lobby.Name} - {lobby.Players.Count}/{lobby.MaxPlayers} - {lobby.Data["GameMode"].Value}";
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void UpdateLobbyGameMode(string gameMode)
    {
        try
        {
            hostLobby = await Lobbies.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>{
                    {"GameMode", new DataObject(DataObject.VisibilityOptions.Public, gameMode)}
                }
            });
            joinedLobby = hostLobby;
            PrintPlayers(hostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void UpdatePlayerName(string newPlayerName)
    {
        try
        {
            playerName = newPlayerName;
            await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id,
            AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject> {
                    {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member,playerName)}
                }
            });
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void LeaveLobby()
    {
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void KickPlayer()
    {
        try
        {
            string playerId = joinedLobby.Players[1].Id;
            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void MigrateLobbyHost()
    {
        try
        {
            hostLobby = await Lobbies.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
            {
                HostId = joinedLobby.Players[1].Id
            });
            joinedLobby = hostLobby;
            PrintPlayers(hostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    private async void DeleteLobby()
    {
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
}
