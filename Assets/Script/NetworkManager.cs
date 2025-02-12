using UnityEngine;
using SocketIOClient;
using System;
using System.Collections.Generic;
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
            Debug.Log(" Connect� au serveur Socket.IO !");
        };

        // V�rifie bien que ces �v�nements sont bien enregistr�s lors de la connexion
        client.On("roomCreated", response =>
        {
            Debug.Log(" [Unity] R�ception de l'�v�nement 'roomCreated'");

            string roomName = response.GetValue<string>();
            Debug.Log($" Salon cr�� avec succ�s: {roomName}");
        });

        client.On("roomJoined", response =>
        {
            Debug.Log(" [Unity] R�ception de l'�v�nement 'roomJoined'");

            string roomName = response.GetValue<string>();
            Debug.Log($" Salon rejoint avec succ�s: {roomName}");
        });

        client.On("roomsList", response =>
        {
            Debug.Log("[Unity] R�ception de l'�v�nement 'roomsList'");

            // V�rifier la structure exacte de la r�ponse
            Debug.Log($"[Unity] Contenu brut de 'roomsList' : {response}");

            // Extraire correctement la liste depuis la structure JSON re�ue
            var jsonObject = response.GetValue<Dictionary<string, List<string>>>();
            if (jsonObject.ContainsKey("rooms"))
            {
                List<string> rooms = jsonObject["rooms"];
                Debug.Log($"[Unity] Liste des salons re�ue (corrig�e) : {string.Join(", ", rooms)}");

                // V�rifier la pr�sence du UIManager
                UIManager uiManager = FindObjectOfType<UIManager>();
                if (uiManager == null)
                {
                    Debug.LogError("[Unity] UIManager non trouv� ! Impossible de mettre � jour la liste.");
                    return;
                }

                Debug.Log("[Unity] Mise � jour de l'interface avec la liste des salons...");
                uiManager.UpdateRoomsList(rooms);
            }
            else
            {
                Debug.LogError("[Unity] Erreur : la r�ponse ne contient pas de cl� 'rooms' !");
            }
        });




        client.On("roomError", response =>
        {
            Debug.Log("[Unity] R�ception de l'�v�nement 'roomError'");

            string errorMsg = response.GetValue<string>();
            Debug.LogWarning($"Erreur lors de la gestion des salons: {errorMsg}");
        });

        await client.ConnectAsync();
    }


    public async void CreateRoom(string roomName)
    {
        Debug.Log("Demande de cr�ation du salon: " + roomName);

        var data = new Dictionary<string, string>
    {
        { "roomName", roomName }
    };

        await client.EmitAsync("createRoom", data);
    }

    public async void JoinRoom(string roomName)
    {
        Debug.Log("Demande de rejoindre le salon: " + roomName);

        var data = new Dictionary<string, string>
    {
        { "roomName", roomName }
    };

        await client.EmitAsync("joinRoom", data);
    }

    public async void GetRooms()
    {
        Debug.Log("Demande de la liste des salons");

        await client.EmitAsync("getRooms");
    }

    private async void OnDestroy()
    {
        if (client != null)
        {
            await client.DisconnectAsync();
        }
    }
}
