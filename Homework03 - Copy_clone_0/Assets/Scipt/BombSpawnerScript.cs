using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class BombSpawnerScript : NetworkBehaviour
{
    public GameObject bombPrefab;
    private List<GameObject> spawnedBomb = new List<GameObject>();
    // public float spawnRadius = 5.0f;
    private OwnerNetworkAnimationScript ownerNetworkAnimationScript;

    void Start()
    {
        ownerNetworkAnimationScript = GetComponent<OwnerNetworkAnimationScript>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ownerNetworkAnimationScript.SetTrigger("PuttingDown");
            SpawnBombServerRpc(OwnerClientId);
        }
    }

    [ServerRpc]
    void SpawnBombServerRpc(ulong clientId)
    {
        // สุ่มตำแหน่งในระยะ spawnRadius
        // Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        // Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, 1.2f, randomOffset.y);
        Vector3 spawnPos = transform.position + (transform.forward * 1.8f) + (transform.up * 0.8f);
        Quaternion spawnRot = transform.rotation;
        GameObject bomb = Instantiate(bombPrefab, spawnPos, spawnRot);
        spawnedBomb.Add(bomb);
        bomb.GetComponent<BombScript>().bombSpawner = this;
        bomb.GetComponent<NetworkObject>().Spawn(true);
        //bomb.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DestroyServerRpc(ulong networkObjectId)
    {
        GameObject toDestory = findBombFromNetworkId(networkObjectId);
        if (toDestory == null) return;

        toDestory.GetComponent<NetworkObject>().Despawn();
        spawnedBomb.Remove(toDestory);
        Destroy(toDestory);
    }

    private GameObject findBombFromNetworkId(ulong networkObjectId)
    {
        foreach (GameObject bomb in spawnedBomb)
        {
            ulong bombId = bomb.GetComponent<NetworkObject>().NetworkObjectId;
            Debug.Log("bombId  " + bombId);
            if (bombId == networkObjectId)
            {
                return bomb;
            }
        }
        return null;
    }
}
