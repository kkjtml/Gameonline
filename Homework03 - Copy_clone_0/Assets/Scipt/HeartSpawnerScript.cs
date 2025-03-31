using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class HeartSpawnerScript : NetworkBehaviour
{
    public GameObject heartPrefab;
    private List<GameObject> spawnedHeart = new List<GameObject>();
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

        if (Input.GetKeyDown(KeyCode.E))
        {
            ownerNetworkAnimationScript.SetTrigger("PuttingDown");
            SpawnHeartServerRpc(OwnerClientId);
        }
    }

    [ServerRpc]
    void SpawnHeartServerRpc(ulong clientId)
    {
        // สุ่มตำแหน่งในระยะ spawnRadius
        // Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        // Vector3 spawnPos = transform.position + new Vector3(randomOffset.x, 1.2f, randomOffset.y);
        Vector3 spawnPos = transform.position + (transform.forward * 1.8f) + (transform.up * 0.8f);
        Quaternion spawnRot = transform.rotation;
        GameObject heart = Instantiate(heartPrefab, spawnPos, spawnRot);
        spawnedHeart.Add(heart);
        heart.GetComponent<HeartScript>().heartSpawner = this;
        heart.GetComponent<NetworkObject>().Spawn(true); //กระจายอ็อบเจ็กต์ไปยังผู้เล่นทุกคน
        //bomb.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DestroyServerRpc(ulong networkObjectId)
    {
        GameObject toDestory = findHeartFromNetworkId(networkObjectId);
        if (toDestory == null) return;

        toDestory.GetComponent<NetworkObject>().Despawn();
        spawnedHeart.Remove(toDestory);
        Destroy(toDestory);
    }

    private GameObject findHeartFromNetworkId(ulong networkObjectId)
    {
        foreach (GameObject heart in spawnedHeart)
        {
            ulong heartId = heart.GetComponent<NetworkObject>().NetworkObjectId;
            Debug.Log("heartId  " + heartId);
            if (heartId == networkObjectId)
            {
                return heart;
            }
        }
        return null;
    }
}

