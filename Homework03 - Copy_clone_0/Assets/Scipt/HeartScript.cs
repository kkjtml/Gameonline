using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class HeartScript : NetworkBehaviour
{
    public HeartSpawnerScript heartSpawner;
    public GameObject effectPrefab;
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsOwner) return;

        if (collision.gameObject.tag == "Playerr")
        {
            ulong networkObjectId = GetComponent<NetworkObject>().NetworkObjectId;
            SpawnEffect();
            heartSpawner.DestroyServerRpc(networkObjectId);
        }
    }
    
    void SpawnEffect()
    {
        GameObject effect = Instantiate(effectPrefab, transform.position, Quaternion.identity);
        effect.GetComponent<NetworkObject>().Spawn();
    }
}

