using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using QFSW.QC;
using TMPro;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class LoginManagerScipt : MonoBehaviour
{
    public static LoginManagerScipt Instance { get; private set; }  // Singleton instance
    public TMP_InputField userNameInputField;
    // public TMP_InputField CoderoomInputField;
    public TMP_Dropdown characterSelect;

    public GameObject loginPanel;
    public GameObject leaveButton;
    public List<GameObject> spawnPoint = new List<GameObject>();
    public List<uint> AlternatePlayerPrefebs;

    public GameObject scorePanel;

    public string ipAddress = "127.0.0.1";
    public TMP_InputField ipInputField;
    UnityTransport transport;

    public TMP_InputField joinCodeInputField;
    public string joinCode;

    public TMP_Text joinCodeDisplayText;

    private void Awake()
    {
        // Ensure that there is only one instance of LoginManagerScipt
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);  // Destroy this instance if another one already exists
        }
    }
    public void Start()
    {
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HanddleClientDisconnect;

        SetUIVisable(false);
        joinCodeDisplayText.gameObject.SetActive(true);
        joinCodeInputField.gameObject.SetActive(false);
    }

    private void SetUIVisable(bool isUserLogin)
    {
        if (isUserLogin)
        {
            loginPanel.SetActive(false);
            leaveButton.SetActive(true);
            scorePanel.SetActive(true);
        }
        else
        {
            loginPanel.SetActive(true);
            leaveButton.SetActive(true);
            scorePanel.SetActive(false);

            joinCodeDisplayText.gameObject.SetActive(true); 
            joinCodeInputField.gameObject.SetActive(false);
        }
    }

    public void OnDestroy()
    {
        if (NetworkManager.Singleton == null) { return; }
        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HanddleClientDisconnect;

    }

    public void HandleServerStarted()
    {
        Debug.Log("HandleServerStarted");
    }

    public void HandleClientConnected(ulong clientId)
    {
        Debug.Log("ClientID = " + clientId);
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // loginPanel.SetActive(false);
            // leaveButton.SetActive(true);
            SetUIVisable(true);
        }

    }

    public void HanddleClientDisconnect(ulong clientID)
    {
        Debug.Log("HandleClientDisconnect clientID = " + clientID);
        if (NetworkManager.Singleton.IsHost) { }
        else if (NetworkManager.Singleton.IsClient) { leave(); }
    }

    public void leave()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // loginPanel.SetActive(true);
        // leaveButton.SetActive(false);
        SetUIVisable(false);
    }

    // private void setIpAddress() {
    //     transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    //     ipAddress = ipInputField.GetComponent<TMP_InputField>().text;
    //     transport.ConnectionData.Address = ipAddress;
    // }

    public async void Host()
    {
        // setIpAddress();
        if (RelayManagerScript.Instance.IsRelayEnabled)
        {
            await RelayManagerScript.Instance.CreateRelay();
        }

        joinCodeDisplayText.gameObject.SetActive(true);
        joinCodeInputField.gameObject.SetActive(false);

        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        NetworkManager.Singleton.StartHost();
        Debug.Log("Start Host");
    }

    public async void Client()
    {
        joinCodeDisplayText.gameObject.SetActive(false);
        joinCodeInputField.gameObject.SetActive(true);

        // setIpAddress();
        joinCode = joinCodeInputField.GetComponent<TMP_InputField>().text;
        if (RelayManagerScript.Instance.IsRelayEnabled && !string.IsNullOrEmpty(joinCode))
        {
            await RelayManagerScript.Instance.JoinRelay(joinCode);
        }
        string userName = userNameInputField.GetComponent<TMP_InputField>().text;
        // string Coderoom = CoderoomInputField.GetComponent<TMP_InputField>().text;
        int Character = SelectColor();

        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(userName + "/" + Character);
        NetworkManager.Singleton.StartClient();
        Debug.Log("Start Client");
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // The client identifier to be authenticated
        var clientId = request.ClientNetworkId;

        // Additional connection data defined by user code
        var connectionData = request.Payload;

        int byteLength = connectionData.Length;
        Debug.Log("byte length = " + byteLength);
        bool isApproved = false;
        if (byteLength > 0)
        {
            string clientData = System.Text.Encoding.ASCII.GetString(connectionData, 0, byteLength);
            string[] ClientDataAndCode = clientData.Split("/");
            // int ColorSelect = int.Parse(ClientDataAndCode[2]);
            int ColorSelect = int.Parse(ClientDataAndCode[1]);
            string hostData = userNameInputField.GetComponent<TMP_InputField>().text;
            // string CoderoomHost = CoderoomInputField.GetComponent<TMP_InputField>().text;
            // isApproved = approveConnection(ClientDataAndCode, hostData, CoderoomHost);
            isApproved = approveConnection(ClientDataAndCode, hostData);
            response.PlayerPrefabHash = AlternatePlayerPrefebs[ColorSelect];
        }
        else
        {
            if (NetworkManager.Singleton.IsHost)
            {
                response.PlayerPrefabHash = AlternatePlayerPrefebs[SelectColor()];
            }
        }

        response.Approved = isApproved;
        response.CreatePlayerObject = true;


        response.Position = Vector3.zero;

        response.Rotation = Quaternion.identity;
        setSpawnLocation(clientId, response);
        //NetworkLog.InfoServer("spawnPos of " + clientId + " is " + response.Position.ToString());

        response.Reason = "Some reason for not approving the client";


        response.Pending = false;
    }

    public bool approveConnection(string[] ClientDataAndCode, string hostData)
    {
        bool isApproved = false;

        string clientData = ClientDataAndCode[0];
        // string CoderoomClient = ClientDataAndCode[1];
        // string Color = ClientDataAndCode[2];
        string Color = ClientDataAndCode[1];


        Debug.Log("HostName = " + hostData);
        Debug.Log("ClientName = " + clientData);
        // Debug.Log("Host Coderoom " + CoderoomHost);
        // Debug.Log("Client Coderoom " + CoderoomClient);

        // bool approveName = System.String.Equals(clientData.Trim(), hostData.Trim()) ? false : true;
        // // bool approveCode = System.String.Equals(CoderoomClient.Trim(), CoderoomHost.Trim()) ? true : false;

        // Debug.Log(approveName);
        // Debug.Log(approveCode);


        // if (approveCode == true && approveName == true)
        // {
        //     isApproved = true;
        // }
        // else if (approveCode == true && approveName == false)
        // {
        //     isApproved = false;
        // }
        // else
        // {
        //     isApproved = false;
        // }

        // return isApproved;

        // ถ้าชื่อไม่ตรงกัน ให้อนุมัติการเชื่อมต่อ
        if (!System.String.Equals(clientData.Trim(), hostData.Trim()))
        {
            isApproved = true;
        }

        return isApproved;
    }

    private void setSpawnLocation(ulong clientID, NetworkManager.ConnectionApprovalResponse response)
    {
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (clientID == NetworkManager.Singleton.LocalClientId)
        {
            GameObject spawnPointNow = SelectSpawn();
            spawnPos = spawnPointNow.transform.position;
            spawnRot = Quaternion.Euler(0f, 225f, 0f);
        }
        else
        {
            GameObject spawnPointNow = SelectSpawn();
            spawnPos = spawnPointNow.transform.position;
            spawnRot = Quaternion.Euler(0f, 225f, 0f);
        }
        response.Position = spawnPos;
        response.Rotation = spawnRot;
    }

    private GameObject SelectSpawn()
    {
        int random = Random.Range(0, spawnPoint.Count);
        return spawnPoint[random];
    }

    public int SelectColor()
    {
        if (characterSelect.GetComponent<TMP_Dropdown>().value == 0)
        {
            return 0;
        }
        else if (characterSelect.GetComponent<TMP_Dropdown>().value == 1)
        {
            return 1;
        }
        else if (characterSelect.GetComponent<TMP_Dropdown>().value == 2)
        {
            return 2;
        }
        else if (characterSelect.GetComponent<TMP_Dropdown>().value == 3)
        {
            return 3;
        }
        else if (characterSelect.GetComponent<TMP_Dropdown>().value == 4)
        {
            return 4;
        }
        return 0;
    }

    public void UpdateJoinCodeDisplay(string joinCode)
    {
        if (joinCodeDisplayText != null)
        {
            joinCodeDisplayText.text = joinCode;
        }
    }
}
