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
            Debug.LogError("WebSocketManager non trouvé dans la scène.");
        }

        if (panelRoomList == null)
        {
            Debug.LogError("panelRoomList non assigné dans l'Inspector !");
        }

        if (roomButtonPrefab == null)
        {
            Debug.LogError("roomButtonPrefab non assigné dans l'Inspector !");
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

        // Correction : Utiliser une classe serialisable
        CreateRoomMessage createMessage = new CreateRoomMessage { type = "createRoom", roomName = roomName };
        string jsonMessage = JsonUtility.ToJson(createMessage);

        Debug.Log("JSON envoye au serveur : " + jsonMessage);
        webSocketManager.SendMessageToServer(jsonMessage);
    }

    public void UpdateRoomList(string[] roomNames)
    {
        Debug.Log("UpdateRoomList appelee.");
        if (roomNames == null || roomNames.Length == 0)
        {
            Debug.LogWarning("Aucun salon recu, liste vide.");
            return;
        }

        Debug.Log("Salons recus pour mise a jour : " + string.Join(", ", roomNames));

        foreach (Transform child in panelRoomList)
        {
            Destroy(child.gameObject);
        }

        foreach (string roomName in roomNames)
        {
            Debug.Log("Ajout d un bouton pour le salon : " + roomName);
            CreateRoomButton(roomName);
        }
    }



    public void CreateRoomButton(string roomName)
    {
        GameObject newButton = Instantiate(roomButtonPrefab, panelRoomList);
        TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
        {
            buttonText.text = roomName;
            Debug.Log("Bouton cree pour le salon : " + roomName);
        }
        else
        {
            Debug.LogError("Erreur : TMP_Text introuvable dans le prefab du bouton.");
        }

        Button button = newButton.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => JoinRoom(roomName));
        }
        else
        {
            Debug.LogError("Erreur : Composant Button introuvable dans le prefab du bouton.");
        }
    }



    public void JoinRoom(string roomName)
    {
        JoinRoomMessage joinRoomMessage = new JoinRoomMessage { type = "joinRoom", roomName = roomName };
        string jsonMessage = JsonUtility.ToJson(joinRoomMessage);
        Debug.Log("Demande de rejoindre le salon : " + jsonMessage);
        webSocketManager.SendMessageToServer(jsonMessage);
    }

    [System.Serializable]
    public class CreateRoomMessage
    {
        public string type;
        public string roomName;
    }

    [System.Serializable]
    public class JoinRoomMessage
    {
        public string type;
        public string roomName;
    }
}
