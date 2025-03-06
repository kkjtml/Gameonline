using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class AutoDestoryParticleSystemScript : NetworkBehaviour
{
    public float delayBeforeDestroy = 2f;
    private ParticleSystem ps;

    public void Start()
    {
        ps = GetComponent<ParticleSystem>();
    }

    public void Update()
    {
        if (!IsOwner) return;

        if (ps && !ps.IsAlive())
        {
            DestoryObject();
        }
    }
    
    void DestoryObject()
    {
        GetComponent<NetworkObject>().Despawn();
        Destroy(gameObject, delayBeforeDestroy);
    }
}
