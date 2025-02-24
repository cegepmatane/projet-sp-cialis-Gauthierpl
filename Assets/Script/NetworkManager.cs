using UnityEngine;
using UnityEngine.SceneManagement; // N'oubliez pas cet import pour SceneManager
using SocketIOClient;
using System;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour
{
    private SocketIO client;

    async void Start()
    {
        await ConnectToServer();
    }

    async Task ConnectToServer()
    {
        client = new SocketIO("http://localhost:3000");

        client.OnConnected += (sender, e) =>
        {
            Debug.Log("Connect� au serveur Socket.IO !");
        };

        client.On("gameJoined", response =>
        {
            Debug.Log("R�ception de l'�v�nement 'gameJoined'");
            string roomName = response.GetValue<string>();
            Debug.Log($"Salon global rejoint: {roomName}");

            // Dispatch sur le thread principal
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Debug.Log("Chargement de la sc�ne GameScene...");
                SceneManager.LoadScene("GameScene");
            });
        });



        await client.ConnectAsync();
    }

    public async void JoinGame()
    {
        Debug.Log("Demande de rejoindre le jeu.");
        await client.EmitAsync("joinGame");
    }

    private async void OnDestroy()
    {
        if (client != null)
        {
            await client.DisconnectAsync();
        }
    }
}
