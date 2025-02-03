using UnityEngine;
using NativeWebSocket;
using UnityEngine.SceneManagement;
using System.Text;

public class WebSocketManager : MonoBehaviour
{
    private WebSocket webSocket;
    private string serverURL = "ws://localhost:8080";
    private UIManager uiManager;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        uiManager = FindObjectOfType<UIManager>();

        if (uiManager == null)
        {
            Debug.LogError("UIManager non trouve dans la scene.");
        }
    }

    async void Start()
    {
        Debug.Log("Tentative de connexion a " + serverURL);

        webSocket = new WebSocket(serverURL);

        webSocket.OnOpen += () => Debug.Log("Connexion WebSocket etablie.");
        webSocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Message recu en brut du serveur : " + message); // <---- NOUVEAU LOG
            ProcessMessage(message);
        };

        webSocket.OnError += (error) => Debug.LogError("Erreur WebSocket : " + error);
        webSocket.OnClose += (closeCode) => Debug.Log("Connexion WebSocket fermee. Code : " + closeCode);

        await webSocket.Connect();
    }

    async void OnApplicationQuit()
    {
        await webSocket.Close();
    }

    public async void SendMessageToServer(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("Erreur : Tentative d envoi d un message vide !");
            return;
        }

        if (webSocket.State == WebSocketState.Open)
        {
            Debug.Log("Envoi au serveur : " + message);
            await webSocket.SendText(message);
        }
        else
        {
            Debug.LogError("Impossible d envoyer un message. WebSocket non ouverte.");
        }
    }

    private void ProcessMessage(string message)
    {
        Debug.Log("Message recu du serveur : " + message);
        var msg = JsonUtility.FromJson<Message>(message);

        if (msg == null || string.IsNullOrEmpty(msg.type))
        {
            Debug.LogError("Message JSON invalide recu !");
            return;
        }

        if (msg.type == "updateRooms")
        {
            Debug.Log("Mise a jour des salons recue du serveur : " + string.Join(", ", msg.rooms));
            if (uiManager != null)
            {
                Debug.Log("Appel de UpdateRoomList dans UIManager.");
                uiManager.UpdateRoomList(msg.rooms);
            }
            else
            {
                Debug.LogError("uiManager est null !");
            }
        }
    }



    [System.Serializable]
    public class Message
    {
        public string type;
        public bool success;
        public string roomName;
        public string[] rooms;
        public string error;
    }
}
