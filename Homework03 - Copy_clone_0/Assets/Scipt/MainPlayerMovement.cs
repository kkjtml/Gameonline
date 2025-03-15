using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Threading;
using Unity.Collections;
// using UnityEditor.PackageManager;

public class MainPlayerMovement : NetworkBehaviour
{
    public float speed = 5.0f;
    public float rotationSpeed = 10.0f;
    Rigidbody rb;

    public TMP_Text namePrefab;
    private TMP_Text nameLabel;
    private NetworkVariable<int> postX = new NetworkVariable<int>(0,
    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<NetworkString> playerNameA = new NetworkVariable<NetworkString>(
    new NetworkString { info = "Player" }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); //ดึงข้อมูลจาก loginmanager มากำหนดค่าให้ playerA 
    public NetworkVariable<NetworkString> playerNameB = new NetworkVariable<NetworkString>(
        new NetworkString { info = "Player" }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); //ดึงข้อมูลจาก loginmanager มากำหนดค่าให้ playerB

    public Renderer eyeRenderer; // Renderer ของดวงตา
    private MaterialPropertyBlock mpb;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    public Texture defaultEyeTexture; // ตาปกติ
    public Texture specialEyeTexture; // ตาพิเศษ

    // NetworkVariable with updated write permission (Server and Owner can write)
    public NetworkVariable<int> eyeTextureStatus = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server); // Allow both Server and Owner to write


    private LoginManagerScipt loginManager;

    public struct NetworkString : INetworkSerializable
    {
        public FixedString32Bytes info;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref info);
        }
        public override string ToString()
        {
            return info.ToString();
        }

        public static implicit operator NetworkString(string v) =>
            new NetworkString() { info = new FixedString32Bytes(v) };
    }

    private void OnEnable()
    {
        if (nameLabel != null)
            nameLabel.enabled = true;
        // อัปเดต Texture ถ้ามีการเปลี่ยนค่า
        eyeTextureStatus.OnValueChanged += (previous, current) => UpdateEyeTexture(current);
    }

    private void OnDisable()
    {
        if (nameLabel != null)
            nameLabel.enabled = false;
        // อัปเดต Texture ถ้ามีการเปลี่ยนค่า
        eyeTextureStatus.OnValueChanged += (previous, current) => UpdateEyeTexture(current);
    }

    public override void OnNetworkSpawn()//เรียกเมื่อตัวละคร spawn ขึ้น
    {
        GameObject canvas = GameObject.FindWithTag("MainCanvas");
        nameLabel = Instantiate(namePrefab, Vector3.zero, Quaternion.identity);
        nameLabel.transform.SetParent(canvas.transform);

        postX.OnValueChanged += (int previousValue, int newValue) =>
        {
            Debug.Log("OwnerID = " + OwnerClientId + " : post x = " + postX.Value);
        };

        playerNameA.OnValueChanged += (NetworkString previousValue, NetworkString newValue) =>
        {
            Debug.Log("OwerId = " + OwnerClientId + " : Old name = " + previousValue.info + " : New name = " + newValue.info);
        };
        playerNameB.OnValueChanged += (NetworkString previousValue, NetworkString newValue) =>
        {
            Debug.Log("OwerId = " + OwnerClientId + " : Old name = " + previousValue.info + " : New name = " + newValue.info);
        };

        // if (IsServer)
        // {
        //     playerNameA.Value = new NetworkString() { info = new FixedString32Bytes("Player1") };
        //     playerNameB.Value = new NetworkString() { info = new FixedString32Bytes("Player2") };
        // }

        base.OnNetworkSpawn();

        // เริ่มต้นการแสดงผล Texture
        UpdateEyeTexture(eyeTextureStatus.Value);

        if (IsOwner)
        {
            loginManager = GameObject.FindObjectOfType<LoginManagerScipt>();
            if (loginManager != null)
            {
                string name = loginManager.userNameInputField.text;
                // ส่งคำขอไปที่ Server เพื่อเปลี่ยน playerNameA หรือ playerNameB
                if (IsOwnedByServer)
                    SetPlayerNameServerRpc(name, true); // ให้ Server อัปเดต playerNameA
                else
                    SetPlayerNameServerRpc(name, false); // ให้ Server อัปเดต playerNameB

                UpdateEyeTexture(eyeTextureStatus.Value);
            }
        }
    }

    private void Update()
    {
        Vector3 nameLabelPos = Camera.main.WorldToScreenPoint(transform.position + new Vector3(0, 4f, 0)); //ชื่ออยู่บนตัวละคร 2.7
        nameLabel.text = gameObject.name;
        nameLabel.transform.position = nameLabelPos;

        if (IsOwner)
        {
            postX.Value = (int)System.Math.Ceiling(transform.position.x);

            // ตรวจสอบการกด F เพื่อเปลี่ยน Texture
            if (Input.GetKeyDown(KeyCode.F))
            {
                RequestToggleEyeTextureServerRpc();
            }

            // if (Input.GetKeyDown(KeyCode.L))
            // {
            //     ClientRpcSendParams clientRpcSendParams = new ClientRpcSendParams { TargetClientIds = new List<ulong> { 1 } };
            //     ClientRpcParams clientRpcParams = new ClientRpcParams { Send = clientRpcSendParams };
            //     TestClientRpc("Hello, this is server", clientRpcParams);
            // }
        }

        UpdatePlayerInfo();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerNameServerRpc(string newName, bool isPlayerA)
    {
        if (isPlayerA)
            playerNameA.Value = new NetworkString { info = new FixedString32Bytes(newName) };
        else
            playerNameB.Value = new NetworkString { info = new FixedString32Bytes(newName) };

        // เรียก ClientRpc เพื่ออัปเดตข้อมูล
        UpdatePlayerNameClientRpc();
    }

    // ClientRpc สำหรับอัปเดตชื่อให้ client อื่น ๆ
    [ClientRpc]
    private void UpdatePlayerNameClientRpc()
    {
        nameLabel.text = (IsOwnedByServer) ? playerNameA.Value.ToString() : playerNameB.Value.ToString();
    }

    // private void TestClientRpc(string msg, ClientRpcParams clientRpcParams)
    // {
    //     Debug.Log("Message : " + msg);
    // }

    [ServerRpc(RequireOwnership = false)]
    private void RequestToggleEyeTextureServerRpc(ServerRpcParams rpcParams = default)
    {
        eyeTextureStatus.Value = (eyeTextureStatus.Value == 0) ? 1 : 0;
    }

    private void UpdateEyeTexture(int status)
    {
        if (eyeRenderer != null)
        {
            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            Texture newTexture = (status == 1) ? specialEyeTexture : defaultEyeTexture;
            mpb.SetTexture(MainTex, newTexture);
            eyeRenderer.SetPropertyBlock(mpb);
        }
    }
    private void UpdatePlayerInfo()
    {
        if (IsOwnedByServer)
        {
            nameLabel.text = playerNameA.Value.ToString();
        }
        else
        {
            nameLabel.text = playerNameB.Value.ToString();
        }
    }

    public override void OnDestroy()
    {
        if (nameLabel != null) { Destroy(nameLabel.gameObject); }
        base.OnDestroy();
    }

    public void Start()
    {
        rb = this.GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // if (IsOwner)
        // {
        //     float translation = Input.GetAxis("Vertical") * speed;
        //     translation *= Time.deltaTime;
        //     rb.MovePosition(rb.position + this.transform.forward * translation);

        //     float rotation = Input.GetAxis("Horizontal");
        //     if (rotation != 0)
        //     {
        //         rotation *= rotationSpeed;
        //         Quaternion turn = Quaternion.Euler(0f, rotation, 0f);
        //         rb.MoveRotation(rb.rotation * turn);
        //     }
        //     else
        //     {
        //         rb.angularVelocity = Vector3.zero;
        //     }
        // }
    }
}
