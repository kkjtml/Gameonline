using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEnterUI : MonoBehaviour
{
    public TMPro.TextMeshProUGUI nameText;
    public void SetName(string playerName)
    {
        nameText.text = playerName;
    }   
}
