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
using Unity.Services.Lobbies.Models;

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
    private RelayServerData storedRelayServerData;
    private bool isJoining;
    private string currentJoinCode; // 🔒 เก็บ joinCode ไว้ใช้ตอน host start / client join

    public TMP_Text Joincodeformhost;
    public TMP_InputField joinCodeInputField;

    //InLobby
    public GameObject playerEntryPrefab; // Prefab สำหรับชื่อผู้เล่น
    public Transform contentParent;      // ScrollView → Content

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
                lobbyUpdateTimer = 1.5f;

                Lobby updated = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = updated;

                ShowPlayersInLobby(joinedLobby); // ✅ อัปเดตรายชื่ออัตโนมัติ
            }
        }
    }

    [Command]
    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "MyLobby " + Random.Range(1, 999);
            int maxPlayers = 5;

            // ✅ ดึงชื่อจาก input field
            if (LoginManagerScipt.Instance != null && LoginManagerScipt.Instance.userNameInputField != null)
            {
                playerName = LoginManagerScipt.Instance.userNameInputField.text;
            }

            // ✅ สร้าง Relay Allocation และรับ Relay Join Code
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // ✅ สร้าง Lobby และแนบ Relay Join Code ลงไป
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                    }
                },
                Data = new Dictionary<string, DataObject>
                {
                    { "JoinCodeKey", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            hostLobby = lobby;
            joinedLobby = hostLobby;

            string lobbyCode = lobby.LobbyCode; // << สำคัญ! ใช้ตัวนี้ให้ client join


            if (Joincodeformhost != null)
            {
                Joincodeformhost.text = $"LobbyCode: {lobbyCode}";
                Joincodeformhost.gameObject.SetActive(true);
            }

            // ✅ ตั้งค่า Relay ให้ NetworkManager
            RelayServerData relayData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);

            //NetworkManager.Singleton.StartHost();

            panelMain.SetActive(false);
            panelRoom.SetActive(true);
            ShowPlayersInLobby(hostLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("❌ CreateLobby failed: " + e);
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
    public async void JoinLobbyByCode()
    {
        if (isJoining) return;
        isJoining = true;

        try
        {
            if (LoginManagerScipt.Instance != null && LoginManagerScipt.Instance.userNameInputField != null)
            {
                playerName = LoginManagerScipt.Instance.userNameInputField.text;
            }

            string inputLobbyCode = joinCodeInputField.text.Trim();
            Debug.Log($"🎯 Trying to join lobby: {inputLobbyCode}");

            // ✅ Join ผ่าน LobbyCode
            JoinLobbyByCodeOptions joinOptions = new JoinLobbyByCodeOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                    }
                }
            };

            Lobby joined = await Lobbies.Instance.JoinLobbyByCodeAsync(inputLobbyCode, joinOptions);
            joinedLobby = joined;

            if (!joined.Data.ContainsKey("JoinCodeKey"))
            {
                Debug.LogError("❌ Relay Join Code (JoinCodeKey) ไม่พบใน Lobby Data");
                return;
            }

            string relayCode = joined.Data["JoinCodeKey"].Value;
            Debug.Log($"✅ Relay Join Code: {relayCode}");

            // ✅ Join Relay
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
            RelayServerData relayData = new RelayServerData(joinAllocation, "dtls");

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);
            NetworkManager.Singleton.StartClient();

            panelMain.SetActive(false);
            panelRoom.SetActive(true);
            ShowPlayersInLobby(joined);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("❌ Relay Join Failed: " + e.Message);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("❌ Lobby Join Failed: " + e.Message);
        }

        isJoining = false;
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
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);

            // 🔄 เคลียร์รายการเก่า
            foreach (Transform child in lobbiesContent)
            {
                Destroy(child.gameObject);
            }

            Debug.Log("Lobbies found: " + queryResponse.Results.Count);

            foreach (Lobby lobby in queryResponse.Results)
            {
                GameObject newEntry = Instantiate(lobbyEntryPrefab, lobbiesContent);

                // ✅ เปลี่ยนชื่อห้องใน Text
                TextMeshProUGUI[] texts = newEntry.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (TextMeshProUGUI txt in texts)
                {
                    if (txt.text == "New Text") // 🧠 ถ้าข้อความเริ่มต้นเป็น "New Text"
                    {
                        txt.text = $"{lobby.Name} - {lobby.Players.Count}/{lobby.MaxPlayers} - {lobby.Data["GameMode"].Value}";
                        break;
                    }
                }

                // ✅ เรียกใช้ LobbyItem (จาก child)
                LobbyItem lobbyItem = newEntry.GetComponentInChildren<LobbyItem>();
                if (lobbyItem != null)
                {
                    lobbyItem.Initialise(this, lobby);
                }
                else
                {
                    Debug.LogError("❌ LobbyItem missing on lobbyEntryPrefab!");
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
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


    public async void JoinAsync(Lobby lobby)
    {
        if (isJoining) return;
        isJoining = true;

        try
        {
            // 🧠 ดึงชื่อจาก InputField ฝั่ง Client ก่อน Join
            if (LoginManagerScipt.Instance != null && LoginManagerScipt.Instance.userNameInputField != null)
            {
                playerName = LoginManagerScipt.Instance.userNameInputField.text;
            }

            // ✅ Join Lobby และเก็บ Lobby ที่ได้
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                    }
                }
            };
            Lobby joined = await Lobbies.Instance.JoinLobbyByIdAsync(lobby.Id, options);
            joinedLobby = joined;

            // ✅ ตรวจสอบว่า Lobby มี JoinCode สำหรับ Relay
            if (!joined.Data.ContainsKey("JoinCodeKey"))
            {
                Debug.LogError("❌ JoinCodeKey not found in lobby data.");
                return;
            }

            string joinCode = joined.Data["JoinCodeKey"].Value;
            Debug.Log("✅ Trying to join with code: " + joinCode);

            // ✅ เชื่อมต่อ Relay ด้วย JoinCode
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();

            panelMain.SetActive(false);
            panelRoom.SetActive(true);

            ShowPlayersInLobby(joined);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("❌ Relay Join Failed: " + e.Message);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("❌ Lobby Join Failed: " + e.Message);
        }

        isJoining = false;
    }


    public void ShowPlayersInLobby(Lobby lobby)
    {
        // 🔄 เคลียร์รายการเก่า
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 🔁 สร้างรายการใหม่จาก lobby.Players
        foreach (Player player in lobby.Players)
        {
            GameObject entry = Instantiate(playerEntryPrefab, contentParent);

            TextMeshProUGUI nameText = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null && player.Data != null && player.Data.ContainsKey("PlayerName"))
            {
                nameText.text = player.Data["PlayerName"].Value;
            }
            else if (nameText != null)
            {
                nameText.text = $"Unknown Player ({player.Id})";
            }
        }
    }

}