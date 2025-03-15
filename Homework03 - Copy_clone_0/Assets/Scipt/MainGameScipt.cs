using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MainGameScipt : MonoBehaviour
{
    public void OnSeverButtonClick()
    {
        NetworkManager.Singleton.StartServer();
    }

    public void OnHostButtonClick()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void OnClientButtonClic()
    {
        NetworkManager.Singleton.StartClient();
    }
    
}
