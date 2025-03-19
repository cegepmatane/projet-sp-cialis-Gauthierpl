using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ChatManager : MonoBehaviour
{
    [Header("Références UI")]
    public GameObject chatPanel;        // Panel (UI) à activer/désactiver
    public TMP_InputField inputField;   // Champ de saisie
    public TextMeshProUGUI chatContent; // Le texte principal qui contient l’historique
    public ScrollRect scrollRect;       // Pour scroller le contenu

    // On stocke tous les messages
    private List<string> messages = new List<string>();

    private bool isChatOpen = false; // Pour savoir si le chat est ouvert

    void OnEnable()
    {
        // On s'abonne à l'événement OnChatMessage du NetworkManager
        NetworkManager.OnChatMessage += HandleChatMessage;
    }

    void OnDisable()
    {
        NetworkManager.OnChatMessage -= HandleChatMessage;
    }

    void Start()
    {
        // On cache le chat par défaut (si tu préfères)
        if (chatPanel) chatPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            bool inputFieldFocused = (inputField != null && inputField.isFocused);

            // Cas 1 : le chat est fermé => T l’ouvre
            if (!isChatOpen)
            {
                ToggleChat();
            }
            // Cas 2 : le chat est ouvert mais le champ n'a pas le focus => T le ferme
            else if (isChatOpen && !inputFieldFocused)
            {
                ToggleChat();
            }
            // Cas 3 : le chat est ouvert ET le champ est focus => T s’écrit dans l’inputField (on ne Toggle pas)
        }

        // Si le chat est ouvert et qu’on appuie sur Entrée, on envoie le message
        if (isChatOpen && Input.GetKeyDown(KeyCode.Return))
        {
            SendMessageToServer();
        }
    }


    private void ToggleChat()
    {
        isChatOpen = !isChatOpen;
        if (chatPanel) chatPanel.SetActive(isChatOpen);

        // Donner le focus à l’input field
        if (isChatOpen && inputField)
        {
            inputField.text = string.Empty;
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    // Appelé quand un nouveau message chat arrive (depuis l'événement OnChatMessage)
    private void HandleChatMessage(string playerId, string pseudo, string message)
    {
        string newLine = $"<b>{pseudo}</b>: {message}";
        messages.Add(newLine);

        // On reconstruit tout le contenu
        chatContent.text = string.Join("\n", messages);

        // Faire défiler en bas
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;


    }

    // Appelé quand on veut envoyer un message
    public void SendMessageToServer()
    {
        if (!inputField || string.IsNullOrEmpty(inputField.text)) return;

        // Envoi du message via NetworkManager
        NetworkManager.Instance.SendChatMessage(inputField.text);

        // On vide le champ
        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }
}
