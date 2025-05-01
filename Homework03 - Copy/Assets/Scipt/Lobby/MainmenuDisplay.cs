using System;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;            // ✅ สำหรับ QueryLobbiesOptions, QueryFilter
using Unity.Services.Lobbies.Models;     // ✅ สำหรับ Lobby
using UnityEngine;
using System.Collections.Generic;

public class MainmenuDisplay : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeInputField;
    public TMP_InputField joinNameInputField;

    public lobbylists manager;

    private async void Start()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log($"Player Id: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }
    }

    // Update is called once per frame
    public void StartClient()
    {
        string myname = joinNameInputField.text;
        ClientManager.Instance.StartClient(joinCodeInputField.text, myname);
    }

}
