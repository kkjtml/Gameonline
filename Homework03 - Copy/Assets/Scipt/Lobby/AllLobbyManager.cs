using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using Unity.Collections;
using UnityEngine.UI;

public class AllLobbyManager : NetworkBehaviour
{
    [Header("Lobby UI")]
    public Transform contentParent;
    public GameObject playerEntryPrefab;
    public TMP_Text joinCodeText;
    public TMP_Dropdown characterSelect;
    public CharacterDatabase characterDatabase;

    private Dictionary<ulong, GameObject> playerEntries = new();


    public GameObject StartButton;
    public GameObject ReadyButton;

    private NetworkList<CharacterSelectState> players = new NetworkList<CharacterSelectState>();
    private NetworkList<FixedString64Bytes> playerNames = new NetworkList<FixedString64Bytes>();

    private void Start()
    {
        if (contentParent == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Content");
            if (found != null)
                contentParent = found.transform;
        }

        if (joinCodeText != null && HostManager.Instance != null)
        {
            joinCodeText.text = HostManager.Instance.JoinCode;
        }
    }

    public override void OnNetworkSpawn()
    {
        playerNames.OnListChanged += OnPlayerListChanged;
        players.OnListChanged += OnPlayersChanged;

        string myName = PlayerPrefs.GetString("PlayerName", $"Player {OwnerClientId}");

        if (IsServer)
        {
            playerNames.Add(myName);

            // ✅ ถ้าเป็น Host → เปิดปุ่ม Start, ปิดปุ่ม Ready
            if (StartButton != null) StartButton.SetActive(true);
            if (ReadyButton != null) ReadyButton.SetActive(false);
        }
        else
        {
            SubmitMyNameServerRpc(myName);

            // ✅ ถ้าเป็น Client → เปิดปุ่ม Ready, ปิดปุ่ม Start
            if (StartButton != null) StartButton.SetActive(false);
            if (ReadyButton != null) ReadyButton.SetActive(true);
        }

        RefreshUI();
    }
    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
    {
        RefreshUI();
    }

    private void OnPlayersChanged(NetworkListEvent<CharacterSelectState> changeEvent)
    {
        RefreshUI();    
    }

    private void RefreshUI()
    {
        if (contentParent == null) return;

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        for (int i = 0; i < playerNames.Count; i++)
        {
            GameObject entry = Instantiate(playerEntryPrefab, contentParent);
            string label = i == 0 ? "Player 1 (Host)" : $"Player {i + 1}";

            bool isReady = false;
            for (int j = 0; j < players.Count; j++)
            {
                if (players[j].ClientId == (ulong)i && players[j].IsLockedIn)
                {
                    isReady = true;
                    break;
                }
            }

            string readyText = isReady ? "(Ready)" : "(Not Ready)";
            entry.GetComponentInChildren<TMP_Text>().text = label + " " + readyText + ": " + playerNames[i].ToString();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitMyNameServerRpc(string name)
    {
        playerNames.Add(name);
    }

    private void OnDestroy()
    {
        if (IsServer && playerNames != null)
        {
            playerNames.Dispose();
        }
    }

    public void Ready()
    {
        int selectedIndex = characterSelect.value;
        int characterId = characterDatabase.GetAllCharacters()[selectedIndex].Id;
        SendReadyServerRpc(characterId);
    }

    public void LockIn()
    {
        LockInServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void LockInServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        int selectedIndex = characterSelect.value;
        int characterId = characterDatabase.GetAllCharacters()[selectedIndex].Id;

        // เช็คก่อนว่ามีอยู่ใน list ไหม
        bool found = false;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
            {
                // Update state
                players[i] = new CharacterSelectState(clientId, characterId, true);
                found = true;
                break;
            }
        }

        // ถ้ายังไม่เจอ client นี้ → เพิ่มใหม่
        if (!found)
        {
            players.Add(new CharacterSelectState(clientId, characterId, true));
        }

        Debug.Log($"Client {clientId} Locked in with CharacterId {characterId}");

        foreach (var player in players)
        {
            HostManager.Instance.SetCharacter(player.ClientId, player.CharacterId);
        }

        HostManager.Instance.StartGame();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendReadyServerRpc(int characterId, ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;

        // หา player ใน list
        bool found = false;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ClientId == clientId)
            {
                // Update state: เปลี่ยนเป็น Ready
                players[i] = new CharacterSelectState(clientId, characterId, true);
                found = true;
                break;
            }
        }

        // ถ้าไม่มี player นี้ใน list → เพิ่มใหม่
        if (!found)
        {
            players.Add(new CharacterSelectState(clientId, characterId, true));
        }

        Debug.Log($"Client {clientId} Ready with CharacterId {characterId}");

        // อัพเดตให้ HostManager จำค่าไว้ด้วย
        HostManager.Instance.SetCharacter(clientId, characterId);
        RefreshUI();
    }


}
