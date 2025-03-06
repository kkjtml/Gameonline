using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Multiplayer.Samples.Utilities.ClientAuthority; // ต้องใช้เพื่อเข้าถึง ClientNetworkTransform

public class PlayerSpawnerScript : NetworkBehaviour
{
    //MainPlayerScript mainPlayer;
    public Behaviour[] scripts;
    private Renderer[] renderers;
    private ClientNetworkTransform clientNetworkTransform;

    void Start()
    {
        //mainPlayer = gameObject.GetComponent<MainPlayerScript>();
        renderers = GetComponentsInChildren<Renderer>();
        clientNetworkTransform = GetComponent<ClientNetworkTransform>(); // ดึง Component มาใช้
    }
    void SetPlayerState(bool state)
    {
        foreach (var script in scripts) { script.enabled = state; }
        foreach (var renderer in renderers) { renderer.enabled = state; }
    }

    private Vector3 GetRandomPos()
    {
        Vector3 randPos = new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        return randPos;
    }
    public void Respawn()
    {
        RespawnServerRpc();
    }
    [ServerRpc]
    void RespawnServerRpc()
    {
        Vector3 pos = GetRandomPos();
        RespawnClientRpc(pos);
    }
    [ClientRpc]
    void RespawnClientRpc(Vector3 spawnPos)
    {
        StartCoroutine(RespawnCoroutine(spawnPos));
    }
    IEnumerator RespawnCoroutine(Vector3 spawnPos)
    {
        SetPlayerState(false);

         // ปิด Interpolation ก่อนเปลี่ยนตำแหน่ง
        if (clientNetworkTransform != null)
        {
            clientNetworkTransform.Interpolate = false;
        }

        transform.position = spawnPos;  // เปลี่ยนตำแหน่งทันที
        yield return new WaitForSeconds(3f);

        // เปิด Interpolation กลับมาให้เคลื่อนไหวลื่นไหล
        if (clientNetworkTransform != null)
        {
            clientNetworkTransform.Interpolate = true;
        }

        SetPlayerState(true);
    }
}
