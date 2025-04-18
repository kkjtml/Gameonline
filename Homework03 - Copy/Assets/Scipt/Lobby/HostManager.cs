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
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;


public class HostManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxConnections = 4;
    [SerializeField] private string lobbySceneName = "LobbyRoom"; // Scene 2
    [SerializeField] private string gameplaySceneName = "SampleScene"; // Scene 3

    public static HostManager Instance { get; private set; }

    private bool gameHasStarted;
    private string lobbyId;

    public Dictionary<ulong, ClientData> ClientData { get; private set; }
    public string JoinCode { get; private set; }

    [SerializeField] private TMP_InputField userNameInputField;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            await UnityServices.InitializeAsync();

            // ⭐ สำคัญ: ลงชื่อเข้าใช้แบบ anonymous
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in anonymously!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Initialization or sign-in failed: {e.Message}");
        }
    }

    public async void StartHost()
    {
        // (1) สร้าง Allocation
        Allocation allocation;
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay create allocation request failed {e.Message}");
            return;
        }

        // (2) รับ join code
        try
        {
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch
        {
            Debug.LogError("Relay get join code request failed");
            return;
        }

        // (3) ตั้งค่า Relay ให้ Transport
        var relayServerData = new RelayServerData(allocation, "dtls");
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

        // (4) สร้าง Lobby
        try
        {
            var createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        "JoinCode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Member,
                            value: JoinCode
                        )
                    }
                }
            };

            Lobby lobby = await Lobbies.Instance.CreateLobbyAsync("My Lobby", maxConnections, createLobbyOptions);
            lobbyId = lobby.Id;

            StartCoroutine(HeartbeatLobbyCoroutine(15));
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Lobby create failed: {e.Message}");
            return;
        }

        // (5) ตั้งค่า network
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnServerStarted += OnNetworkReady;

        ClientData = new Dictionary<ulong, ClientData>();

        // (6) Start host
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
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = Vector3.zero;
        response.Rotation = Quaternion.identity;
        response.Pending = false;
    }

    private void OnNetworkReady()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        // ไป Scene 2 → LobbyRoom
        SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (ClientData.ContainsKey(clientId))
        {
            if (ClientData.Remove(clientId))
            {
                Debug.Log($"Removed client {clientId}");
            }
        }
    }

    public void StartGame()
    {
        gameHasStarted = true;
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }
}
