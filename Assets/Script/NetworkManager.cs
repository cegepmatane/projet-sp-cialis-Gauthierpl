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
            Debug.Log(" Connecté au serveur Socket.IO !");
        };

        // Vérifie bien que ces événements sont bien enregistrés lors de la connexion
        client.On("roomCreated", response =>
        {
            Debug.Log(" [Unity] Réception de l'événement 'roomCreated'");

            string roomName = response.GetValue<string>();
            Debug.Log($" Salon créé avec succès: {roomName}");
        });

        client.On("roomJoined", response =>
        {
            Debug.Log(" [Unity] Réception de l'événement 'roomJoined'");

            string roomName = response.GetValue<string>();
            Debug.Log($" Salon rejoint avec succès: {roomName}");
        });

        client.On("roomsList", response =>
        {
            Debug.Log("[Unity] Réception de l'événement 'roomsList'");

            // Vérifier la structure exacte de la réponse
            Debug.Log($"[Unity] Contenu brut de 'roomsList' : {response}");

            // Extraire correctement la liste depuis la structure JSON reçue
            var jsonObject = response.GetValue<Dictionary<string, List<string>>>();
            if (jsonObject.ContainsKey("rooms"))
            {
                List<string> rooms = jsonObject["rooms"];
                Debug.Log($"[Unity] Liste des salons reçue (corrigée) : {string.Join(", ", rooms)}");

                // Vérifier la présence du UIManager
                UIManager uiManager = FindObjectOfType<UIManager>();
                if (uiManager == null)
                {
                    Debug.LogError("[Unity] UIManager non trouvé ! Impossible de mettre à jour la liste.");
                    return;
                }

                Debug.Log("[Unity] Mise à jour de l'interface avec la liste des salons...");
                uiManager.UpdateRoomsList(rooms);
            }
            else
            {
                Debug.LogError("[Unity] Erreur : la réponse ne contient pas de clé 'rooms' !");
            }
        });




        client.On("roomError", response =>
        {
            Debug.Log("[Unity] Réception de l'événement 'roomError'");

            string errorMsg = response.GetValue<string>();
            Debug.LogWarning($"Erreur lors de la gestion des salons: {errorMsg}");
        });

        await client.ConnectAsync();
    }


    public async void CreateRoom(string roomName)
    {
        Debug.Log("Demande de création du salon: " + roomName);

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
