using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ChatManager : MonoBehaviour
{
    [Header("R�f�rences UI")]
    public GameObject chatPanel;        // Panel (UI) � activer/d�sactiver
    public TMP_InputField inputField;   // Champ de saisie
    public TextMeshProUGUI chatContent; // Le texte principal qui contient l�historique
    public ScrollRect scrollRect;       // Pour scroller le contenu

    // On stocke tous les messages
    private List<string> messages = new List<string>();

    private bool isChatOpen = false; // Pour savoir si le chat est ouvert

    void OnEnable()
    {
        // On s'abonne � l'�v�nement OnChatMessage du NetworkManager
        NetworkManager.OnChatMessage += HandleChatMessage;
    }

    void OnDisable()
    {
        NetworkManager.OnChatMessage -= HandleChatMessage;
    }

    void Start()
    {
        // On cache le chat par d�faut (si tu pr�f�res)
        if (chatPanel) chatPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            bool inputFieldFocused = (inputField != null && inputField.isFocused);

            // Cas 1 : le chat est ferm� => T l�ouvre
            if (!isChatOpen)
            {
                ToggleChat();
            }
            // Cas 2 : le chat est ouvert mais le champ n'a pas le focus => T le ferme
            else if (isChatOpen && !inputFieldFocused)
            {
                ToggleChat();
            }
            // Cas 3 : le chat est ouvert ET le champ est focus => T s��crit dans l�inputField (on ne Toggle pas)
        }

        // Si le chat est ouvert et qu�on appuie sur Entr�e, on envoie le message
        if (isChatOpen && Input.GetKeyDown(KeyCode.Return))
        {
            SendMessageToServer();
        }
    }


    private void ToggleChat()
    {
        isChatOpen = !isChatOpen;
        if (chatPanel) chatPanel.SetActive(isChatOpen);

        // Donner le focus � l�input field
        if (isChatOpen && inputField)
        {
            inputField.text = string.Empty;
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    // Appel� quand un nouveau message chat arrive (depuis l'�v�nement OnChatMessage)
    private void HandleChatMessage(string playerId, string pseudo, string message)
    {
        string newLine = $"<b>{pseudo}</b>: {message}";
        messages.Add(newLine);

        // On reconstruit tout le contenu
        chatContent.text = string.Join("\n", messages);

        // Faire d�filer en bas
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;


    }

    // Appel� quand on veut envoyer un message
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
