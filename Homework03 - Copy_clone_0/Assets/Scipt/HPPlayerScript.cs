using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.SocialPlatforms.Impl;

public class HPPlayerScript : NetworkBehaviour
{
    TMP_Text p1Text;
    TMP_Text p2Text;
    MainPlayerMovement mainPlayer;
    public NetworkVariable<int> hpP1 = new NetworkVariable<int>(5,
    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<int> hpP2 = new NetworkVariable<int>(5,
    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private OwnerNetworkAnimationScript ownerNetworkAnimationScript;

    // Start is called before the first frame update
    void Start()
    {
        p1Text = GameObject.Find("P1HPText (TMP)").GetComponent<TMP_Text>();
        p2Text = GameObject.Find("P2HPText (TMP)").GetComponent<TMP_Text>();
        mainPlayer = GetComponent<MainPlayerMovement>();
        ownerNetworkAnimationScript = GetComponent<OwnerNetworkAnimationScript>();
    }

    private void UpdatePlayerNameAndScore()
    {
        if (IsOwnedByServer)
        {
            p1Text.text = $"{mainPlayer.playerNameA.Value} : {hpP1.Value}";
        }
        else
        {
            p2Text.text = $"{mainPlayer.playerNameB.Value} : {hpP2.Value}";
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdatePlayerNameAndScore();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsLocalPlayer) return;

        if (collision.gameObject.tag == "DeathZone")
        {
            if (IsOwnedByServer)
            {
                hpP1.Value--;
            }
            else
            {
                hpP2.Value--;
            }
            gameObject.GetComponent<PlayerSpawnerScript>().Respawn();
        }
        else if (collision.gameObject.tag == "Bomb")
        {
            if (IsOwnedByServer)
            {
                hpP1.Value--;
                if (hpP1.Value <= 0)
                {
                    ownerNetworkAnimationScript.SetTrigger("Dead");
                }
                ownerNetworkAnimationScript.SetTrigger("Hurt");
            }
            else
            {
                hpP2.Value--;
                if (hpP2.Value <= 0)
                {
                    ownerNetworkAnimationScript.SetTrigger("Dead");
                }
                ownerNetworkAnimationScript.SetTrigger("Hurt");
            }
        }
        else if (collision.gameObject.tag == "Heart")
        {
            if (IsOwnedByServer)
            {
                hpP1.Value++;
                ownerNetworkAnimationScript.SetTrigger("Glad");
            }
            else
            {
                hpP2.Value++;
                ownerNetworkAnimationScript.SetTrigger("Glad");
            }
        }
    }
}
