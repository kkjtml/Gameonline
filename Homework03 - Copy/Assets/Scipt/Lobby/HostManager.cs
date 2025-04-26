using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using TMPro;

public class HostManager : MonoBehaviour
{
    [Header("Lobby Settings")]
    [SerializeField] private int maxConnections = 4;
    [SerializeField] private string characterSelectSceneName = "LobbyRoom";   // ✅ Scene ที่ 2
    [SerializeField] private string gameplaySceneName = "SampleScene";        // ✅ Scene ที่ 3

    public TMP_InputField userNameInputField; // ✅ เพิ่มการระบุก InputField ชื่อ

    public static HostManager Instance { get; private set; }

    public string JoinCode { get; private set; }
    private string lobbyId;
    private bool gameHasStarted;

    public Dictionary<ulong, ClientData> ClientData { get; private set; }

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            await InitializeServices(); // ⭐ initialize ทันทีตอนเปิด
        }
    }

    private async Task InitializeServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("✅ Signed in anonymously");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ UnityServices init failed: {e.Message}");
        }
    }

    public async void StartHost()
    {
        Allocation allocation;

        try
        {
            // 📡 ขอเซิร์ฟเวอร์จาก Relay
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Relay create allocation request failed: {e.Message}");
            throw;
        }

        Debug.Log($"✅ Relay Allocation Success: {allocation.AllocationId}");

        try
        {
            // 🔑 ขอ JoinCode จาก Allocation
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch
        {
            Debug.LogError("❌ Relay get join code request failed");
            throw;
        }

        var relayServerData = new RelayServerData(allocation, "dtls");
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        try
        {
            // 📝 ดึงชื่อห้องจาก InputField ถ้าไม่ได้ใส่ ให้ตั้งว่า "Unnamed Lobby"
            string lobbyName = userNameInputField != null && !string.IsNullOrWhiteSpace(userNameInputField.text)
                ? userNameInputField.text
                : "Unnamed Lobby";

            // 📦 ตั้งค่า Data ของล็อบบี้: JoinCode + GameMode
            var createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>()
                {
                    {
                        "JoinCode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: JoinCode
                        )
                    },
                    {
                        "GameMode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Public,
                            value: "Deathmatch" // 📌 กำหนดโหมดตายตัว
                        )
                    }
                }
            };

            // 🏠 สร้าง Lobby จริงๆ
            Lobby lobby = await Lobbies.Instance.CreateLobbyAsync(lobbyName, maxConnections, createLobbyOptions);
            lobbyId = lobby.Id;
            Debug.Log($"✅ Lobby Created: {lobbyName}");

            StartCoroutine(HeartbeatLobbyCoroutine(15)); // 💓 Ping lobby ทุกๆ 15 วินาที
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"❌ Lobby create failed: {e}");
            throw;
        }

        // 🖥️ เซ็ต Callback ของ Network
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += OnNetworkReady;

        ClientData = new Dictionary<ulong, ClientData>();

        // 🚀 Start Host Server จริงๆ
        NetworkManager.Singleton.StartHost();
    }


    private IEnumerator HeartbeatLobbyCoroutine(float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (ClientData.Count >= maxConnections || gameHasStarted)
        {
            response.Approved = false;
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = false;
        response.Pending = false;

        ClientData[request.ClientNetworkId] = new ClientData(request.ClientNetworkId);
        Debug.Log($"✅ Client {request.ClientNetworkId} approved.");
    }

    private void OnNetworkReady()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        NetworkManager.Singleton.SceneManager.LoadScene(characterSelectSceneName, LoadSceneMode.Single);
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (ClientData.ContainsKey(clientId))
        {
            ClientData.Remove(clientId);
            Debug.Log($"❌ Client {clientId} disconnected.");
        }
    }

    public void SetCharacter(ulong clientId, int characterId)
    {
        if (ClientData.TryGetValue(clientId, out var data))
        {
            data.characterId = characterId;
        }
    }

    public void StartGame()
    {
        gameHasStarted = true;
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }
}