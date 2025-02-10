using System.Collections;
using UnityEngine;
using NativeWebSocket;
using System.Text;

public class WebSocketManager : MonoBehaviour
{
    private WebSocket webSocket;
    private string serverURL = "ws://localhost:8080";
    private UIManager uiManager;

    private void Awake()
    {
        Debug.Log("[WebSocketManager] Awake() ex�cut�.");

        DontDestroyOnLoad(gameObject);
        uiManager = FindObjectOfType<UIManager>();

        if (uiManager == null)
        {
            Debug.LogError("[WebSocketManager] UIManager non trouv� !");
        }
    }


    async void Start()
    {
        Debug.Log("Tentative de connexion � " + serverURL);
        webSocket = new WebSocket(serverURL);

        webSocket.OnOpen += () => Debug.Log("Connexion WebSocket �tablie.");
        webSocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Message re�u en brut du serveur : " + message);
            ProcessMessage(message);
        };

        webSocket.OnError += (error) => Debug.LogError("Erreur WebSocket : " + error);
        webSocket.OnClose += (closeCode) => Debug.Log("Connexion WebSocket ferm�e. Code : " + closeCode);

        await webSocket.Connect();

        Debug.Log("WebSocket connect�e, d�marrage des coroutines de v�rification.");

        StartCoroutine(CheckWebSocketConnection());
        StartCoroutine(RequestRoomListPeriodically());
    }


    IEnumerator CheckWebSocketConnection()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);

            if (webSocket == null)
            {
                Debug.LogError("[WebSocketManager] WebSocket est null !");
            }
            else
            {
                Debug.Log("[WebSocketManager] V�rification �tat WebSocket : " + webSocket.State);
            }
        }
    }


    IEnumerator RequestRoomListPeriodically()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);
            SendMessageToServer("{\"type\":\"getRooms\"}");
        }
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
            Debug.Log("Message envoy� avec succ�s.");
        }
        else
        {
            Debug.LogError("Impossible d'envoyer un message. WebSocket non ouverte. �tat actuel : " + webSocket.State);
        }
    }

    private void ProcessMessage(string message)
    {
        Debug.Log("Message re�u du serveur : " + message);
        var msg = JsonUtility.FromJson<Message>(message);

        if (msg == null || string.IsNullOrEmpty(msg.type))
        {
            Debug.LogError("Message JSON invalide re�u !");
            return;
        }

        if (msg.type == "updateRooms")
        {
            Debug.Log("Mise � jour des salons re�ue du serveur : " + string.Join(", ", msg.rooms));

            if (uiManager != null)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    uiManager.UpdateRoomList(msg.rooms);
                });
            }
            else
            {
                Debug.LogError("uiManager est null !");
            }
        }
    }

    async void OnApplicationQuit()
    {
        await webSocket.Close();
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
