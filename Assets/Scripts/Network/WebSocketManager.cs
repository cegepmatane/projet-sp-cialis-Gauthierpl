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
            Debug.LogError("UIManager n'a pas �t� trouv� dans la sc�ne.");
        }
    }

    async void Start()
    {
        webSocket = new WebSocket(serverURL);

        webSocket.OnOpen += () => Debug.Log("Connexion WebSocket �tablie.");
        webSocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Message re�u du serveur : " + message);
            ProcessMessage(message);
        };
        webSocket.OnError += (error) => Debug.LogError("Erreur WebSocket : " + error);
        webSocket.OnClose += (closeCode) => Debug.Log($"Connexion WebSocket ferm�e. Code : {closeCode}");

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
            Debug.LogError("Erreur : Tentative d'envoi d'un message vide !");
            return;
        }

        if (webSocket.State == WebSocketState.Open)
        {
            Debug.Log("Envoi au serveur : " + message);
            await webSocket.SendText(message);
        }
        else
        {
            Debug.LogError("Impossible d'envoyer un message. WebSocket non ouverte.");
        }
    }

    private void ProcessMessage(string message)
    {
        Debug.Log("Traitement du message re�u : " + message);
        var msg = JsonUtility.FromJson<Message>(message);

        if (msg == null || string.IsNullOrEmpty(msg.type))
        {
            Debug.LogError("Message JSON invalide re�u !");
            return;
        }

        if (msg.type == "createRoom" && msg.success)
        {
            Debug.Log("Salon cr�� avec succ�s : " + msg.roomName);
            SceneManager.LoadScene("Jeu");
        }
        else if (msg.type == "joinRoom" && msg.success)
        {
            Debug.Log("Vous avez rejoint le salon : " + msg.roomName);
            SceneManager.LoadScene("Jeu");
        }
        else if (msg.type == "updateRooms")
        {
            Debug.Log("Mise � jour de la liste des salons re�ue : " + string.Join(", ", msg.rooms));
            uiManager.UpdateRoomList(msg.rooms);
        }
        else
        {
            Debug.LogError("Erreur lors de la cr�ation ou du rejoint du salon : " + msg.error);
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
