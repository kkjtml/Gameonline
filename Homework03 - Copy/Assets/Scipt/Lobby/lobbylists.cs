using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using TMPro;
using QFSW.QC;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Netcode;

public class lobbylists : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform lobbyItemParent;           // ✅ ใช้อันนี้แทน lobbiesContent
    [SerializeField] private GameObject lobbyItemPrefab;

    private bool isRefreshing;
    private bool isJoining;

    [Command]
    public async void ListLobbies()
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
            foreach (Transform child in lobbyItemParent)
            {
                Destroy(child.gameObject);
            }

            Debug.Log("Lobbies found: " + queryResponse.Results.Count);

            foreach (Lobby lobby in queryResponse.Results)
            {
                GameObject newEntry = Instantiate(lobbyItemPrefab, lobbyItemParent);

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
                    Debug.LogError("❌ LobbyItem missing on lobbyItemPrefab!");
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    public async void JoinAsync(Lobby lobby)
    {
        if (isJoining) { return; }

        isJoining = true;

        try
        {
            Lobby joiningLobby = await Lobbies.Instance.JoinLobbyByIdAsync(lobby.Id);
            string joinCode = joiningLobby.Data["JoinCode"].Value;

            await ClientManager.Instance.StartClient(joinCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }

        isJoining = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"✅ Client connected! ID: {clientId}");

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // ⭐ เมื่อเราตัวเอง connect สำเร็จ → เปลี่ยนซีน
            UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyRoom");
        }
    }

}
