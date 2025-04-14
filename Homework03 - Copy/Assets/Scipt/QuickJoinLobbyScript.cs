using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Authentication;
using ParrelSync;
using Unity.Services.Core;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System;
using Unity.Netcode;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using TMPro;

public class QuickJoinLobbyScript : MonoBehaviour
{
    public TMP_InputField userNameInput;
    public GameObject startButton;
    public GameObject quickJoinPanel;
    private string playerName;
    string lobbyName = "MyLobby";
    private Lobby joinedLobby;

    public async void CreateOrJoinLobby()
    {
        startButton.SetActive(false);
        quickJoinPanel.SetActive(false);

        playerName = userNameInput.GetComponent<TMP_InputField>().text;
        //joinedLobby = await QuickJoinLobby() ?? await CreateLobby();
        joinedLobby = await CreateLobby();
        if (joinedLobby == null)
        {
            startButton.SetActive(true);
            quickJoinPanel.SetActive(true);
        }
    }

    private async Task<Lobby> QuickJoinLobby()
    {
        try
        {
            // Quick-join a random lobby 
            Lobby lobby = await FindRandomLobby();

            if (lobby == null) return null;
            Debug.Log(lobby.Name + " , " + lobby.AvailableSlots);

            // If we found one, grab the relay allocation details
            if (lobby.Data["JoinCodeKey"].Value != null)
            {
                string joinCode = lobby.Data["JoinCodeKey"].Value;
                Debug.Log("joinCode = " + joinCode);
                if (joinCode == null) return null;

                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

                // Join the game room as a client
                NetworkManager.Singleton.StartClient();
                return lobby;
            }

            return null;
        }
        catch (Exception e)
        {
            Debug.Log("No lobbies available via quick join");
            return null;
        }
    }

    public async Task<Lobby> FindRandomLobby()
    {
        try{
            Lobby lobby =  await Lobbies.Instance.QuickJoinLobbyAsync();
            Debug.Log(lobby.Name + "," + lobby.AvailableSlots);
            return lobby;
        }catch (LobbyServiceException e){ 
            Debug.Log(e);
            return null;
        }
    }
    
    // private async Task<Lobby> FindRandomLobby()
    // {
    //     try
    //     {
    //         QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
    //         {
    //             Filters = new List<QueryFilter>
    //             {
    //                 new QueryFilter(QueryFilter.FieldOptions.AvailableSlots,"1",QueryFilter.OpOptions.GT)
    //             }
    //         };
    //         QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);
    //         Debug.Log("Lobbies found: " + queryResponse.Results.Count);
    //         foreach (Lobby lobby in queryResponse.Results)
    //         {
    //             return lobby;
    //         }
    //         return null;
    //     }
    //     catch (LobbyServiceException e)
    //     {
    //         Debug.Log(e);
    //         return null;
    //     }
    // }

    private async Task<Lobby> CreateLobby()
    {
        try
        {
            const int maxPlayers = 5;

            // Create a relay allocation and generate a join code to share with the lobby
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Create a lobby, adding the relay join code to the lobby data
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
                    {"JoinCodeKey", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);

            // Send a heartbeat every 15 seconds to keep the room alive
            StartCoroutine(HeartBeatLobbyCoroutine(lobby.Id, 15));

            // Set the game room to use the relay allocation
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            
            // Start the room immediately (or can wait for the lobby to fill up)
            NetworkManager.Singleton.StartHost();

            Debug.Log("Join code = " + joinCode);
            LobbyManagerScript.Instance.PrintPlayers(lobby);
            return lobby;
        }
        catch (Exception e)
        {
            Debug.LogFormat("Failed creating a lobby");
            return null;
        }
    }

    private static IEnumerator HeartBeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    private void OnDestroy()
    {
        try
        {
            StopAllCoroutines();
            // todo: Add a check to see if you're host
            //if (joinedLobby != null)
            //{
            //    if (joinedLobby.HostId == _playerId) Lobbies.Instance.DeleteLobbyAsync(joinedLobby.Id);
            //    else Lobbies.Instance.RemovePlayerAsync(joinedLobby.Id, _playerId);
            //}
        }
        catch (Exception e)
        {
            Debug.Log($"Error shutting down lobby: {e}");
        }
    }

}
