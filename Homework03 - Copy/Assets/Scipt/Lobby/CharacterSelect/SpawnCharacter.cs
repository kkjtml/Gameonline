using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnCharacter : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterDatabase characterDatabase;
    public List<GameObject> spawnPoint = new List<GameObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        foreach (var client in HostManager.Instance.ClientData)
        {
            var character = characterDatabase.GetCharacterById(client.Value.characterId);
            if (character != null && character.Id == 1)
            {
                GameObject spawnPointNow = SelectSpawn();
                var spawnPos = spawnPointNow.transform.position;
                var spawnRot = Quaternion.Euler(0f, 0f, 0f);
                StartCoroutine(WaitforMap());
                var characterInstance = Instantiate(character.GameplayPrefab, spawnPos, spawnRot);
                characterInstance.SpawnAsPlayerObject(client.Value.clientId);
            }
            if (character != null && character.Id == 2)
            {
                GameObject spawnPointNow = SelectSpawn();
                var spawnPos = spawnPointNow.transform.position;
                var spawnRot = Quaternion.Euler(0f, 0f, 0f);
                StartCoroutine(WaitforMap());
                var characterInstance = Instantiate(character.GameplayPrefab, spawnPos, spawnRot);
                characterInstance.SpawnAsPlayerObject(client.Value.clientId);
            }
        }
    }

    private GameObject SelectSpawn()
    {
        int random = Random.Range(0, spawnPoint.Count);
        return spawnPoint[random];
    }

    IEnumerator WaitforMap()
    {
        yield return new WaitForSeconds(50f);
    }
}
