using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class LobbyItem : MonoBehaviour
{
    private LobbyManagerScript lobbyManagerScript;
    private Lobby lobby;

    public Button joinButton; // 🎯 set ผ่าน Inspector

    public void Initialise(LobbyManagerScript manager, Lobby lobby)
    {
        this.lobbyManagerScript = manager;
        this.lobby = lobby;

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(Join); // ✅ ผูกปุ่ม Join
        }
        else
        {
            Debug.LogWarning("❗ joinButton not assigned in Inspector");
        }
    }

    public void Join()
    {
        if (lobbyManagerScript == null || lobby == null)
        {
            Debug.LogError("❌ LobbyItem not initialized!");
            return;
        }

        lobbyManagerScript.JoinAsync(lobby); // ✅ ส่งข้อมูลไป join
    }
}
