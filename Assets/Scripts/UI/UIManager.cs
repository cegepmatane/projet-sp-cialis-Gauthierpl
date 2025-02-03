using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public TMP_InputField inputRoomName;
    public Button btnCreateRoom;
    public Transform panelRoomList;
    public GameObject roomButtonPrefab;
    private WebSocketManager webSocketManager;

    private void Awake()
    {
        webSocketManager = FindObjectOfType<WebSocketManager>();
        if (webSocketManager == null)
        {
            Debug.LogError("WebSocketManager non trouvé !");
        }
    }

    void Start()
    {
        btnCreateRoom.onClick.AddListener(CreateRoom);
    }

    public void CreateRoom()
    {
        string roomName = inputRoomName.text;
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Erreur : Le nom du salon est vide.");
            return;
        }

        var createMessage = new CreateRoomMessage { type = "createRoom", roomName = roomName };
        string jsonMessage = JsonUtility.ToJson(createMessage);

        Debug.Log("JSON envoyé au serveur : " + jsonMessage);
        webSocketManager.SendMessageToServer(jsonMessage);
    }

    public void UpdateRoomList(string[] roomNames)
    {
        foreach (Transform child in panelRoomList)
        {
            Destroy(child.gameObject);
        }

        foreach (string roomName in roomNames)
        {
            GameObject newButton = Instantiate(roomButtonPrefab, panelRoomList);
            newButton.GetComponentInChildren<TMP_Text>().text = roomName;
            newButton.GetComponent<Button>().onClick.AddListener(() => JoinRoom(roomName));
        }
    }

    public void JoinRoom(string roomName)
    {
        string joinRoomMessage = JsonUtility.ToJson(new { type = "joinRoom", roomName = roomName });
        Debug.Log("Demande de rejoindre le salon : " + joinRoomMessage);
        webSocketManager.SendMessageToServer(joinRoomMessage);
    }

    [System.Serializable]
    public class CreateRoomMessage
    {
        public string type;
        public string roomName;
    }
}
