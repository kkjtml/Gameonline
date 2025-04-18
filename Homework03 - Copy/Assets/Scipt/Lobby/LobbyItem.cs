using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class LobbyItem : MonoBehaviour
{
    private lobbylists manager;  // ✅ ตัวแปรถูกต้อง
    private Lobby lobby;

    public Button joinButton;

    public void Initialise(lobbylists manager, Lobby lobby)
    {
        this.manager = manager;  // ✅ ใช้ชื่อให้ตรงกับตัวแปรจริง
        this.lobby = lobby;

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(Join);
        }
        else
        {
            Debug.LogWarning("❗ joinButton not assigned in Inspector");
        }
    }

    public void Join()
    {
        if (manager == null || lobby == null)
        {
            Debug.LogError("❌ LobbyItem not initialized!");
            return;
        }

        manager.JoinAsync(lobby); // ✅ เรียกผ่านตัวแปรที่ถูกต้อง
    }
}
