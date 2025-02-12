using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;


public class UIManager : MonoBehaviour
{
    public NetworkManager networkManager;
    public TMP_InputField createRoomInput;
    public TMP_InputField joinRoomInput;
    public GameObject roomTemplateText; // R�f�rence au mod�le de texte
    public Transform roomsListContent; // Conteneur des salons
    private void Start()
    {
        List<string> testRooms = new List<string> { "SalonA", "SalonB", "SalonC" };
        UpdateRoomsList(testRooms);
    }

    public void OnCreateRoomClick()
    {
        string roomName = createRoomInput.text;
        if (!string.IsNullOrEmpty(roomName))
        {
            networkManager.CreateRoom(roomName);
        }
        else
        {
            Debug.LogWarning("Veuillez entrer un nom de salon.");
        }
    }

    public void OnJoinRoomClick()
    {
        string roomName = joinRoomInput.text;
        if (!string.IsNullOrEmpty(roomName))
        {
            networkManager.JoinRoom(roomName);
        }
        else
        {
            Debug.LogWarning("Veuillez entrer un nom de salon.");
        }
    }

    public void OnGetRoomsClick()
    {
        Debug.Log("Demande de la liste des salons...");
        networkManager.GetRooms();
    }

    public void UpdateRoomsList(List<string> rooms)
    {
        Debug.Log($"[UI] Mise � jour de la liste des salons avec {rooms.Count} �l�ments...");

        if (roomsListContent == null)
        {
            Debug.LogError("[UI] Erreur : `RoomsListContent` est NULL !");
            return;
        }

        // Supprimer les anciens salons affich�s
        foreach (Transform child in roomsListContent)
        {
            Destroy(child.gameObject);
        }

        if (roomTemplateText == null)
        {
            Debug.LogError("[UI] Erreur : `RoomTemplateText` est NULL !");
            return;
        }

        foreach (string room in rooms)
        {
            Debug.Log($"[UI] Cr�ation d'un �l�ment UI pour le salon : {room}");

            GameObject newText = Instantiate(roomTemplateText, roomsListContent);
            newText.GetComponent<TextMeshProUGUI>().text = room; // Assigne le nom du salon

            Debug.Log($"[UI] Salon ajout� � la liste : {room}");
        }
    }




}
