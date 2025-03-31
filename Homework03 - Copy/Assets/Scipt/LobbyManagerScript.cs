using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFSW.QC;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using TMPro;

public class LobbyManagerScript : MonoBehaviour
{
    Lobby hostLobby;
    private string playerName;
    public Button listLobbiesButton; // ปุ่มกดเพื่อเรียกดู Lobby
    public GameObject lobbyEntryPrefab; // Prefab ที่ใช้แสดงรายการ Lobby
    public Transform lobbiesContent; // Content ของ Scroll View
    private void Start()
    {
        playerName = "myName " + Random.Range(1, 999);
        Debug.Log("Player name : " + playerName);
        listLobbiesButton.onClick.AddListener(() => ListLobbies());
    }

    [Command]
    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "MyLobby " + Random.Range(1, 999); ;
            int maxPlayer = 5;
            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {"PlayerName",
                            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member,playerName)}
                    }
                },
                Data = new Dictionary<string, DataObject>
                {
                    {"GameMode", new DataObject(DataObject.VisibilityOptions.Public, "DeathMatch") }
                }
            };
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayer, createLobbyOptions);
            hostLobby = lobby;
            StartCoroutine(HeartbeatLobbyCoroutine(hostLobby.Id, 15));
            Debug.Log("Created Lobby : " + lobby.Name + " , " + lobby.MaxPlayers + " , " + lobby.Id + " , " + lobby.LobbyCode);
            PrintPlayers(hostLobby);
        }
        catch (LobbyServiceException e)
        { Debug.Log(e); }
    }

    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log("Players in Lobby : " + lobby.Name + " : " + lobby.Data["GameMode"].Value);
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Id + " : " + player.Data["PlayerName"].Value);
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
            Lobby joinedLobby = await Lobbies.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions);
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
}
