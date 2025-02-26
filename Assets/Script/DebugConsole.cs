using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }

    public TextMeshProUGUI logText;  // Assigné depuis l'inspecteur
    public ScrollRect scrollRect;
    public GameObject consolePanel;  // Le panel à activer/désactiver
    public Button copyButton;

    private static Queue<string> logQueue = new Queue<string>();
    private const int maxLogCount = 50; // Nombre max de logs affichés

    private void Awake()
    {
        consolePanel.SetActive(false); // La console est fermée par défaut

        // Ajouter un listener pour capturer les logs
        Application.logMessageReceived += HandleLog;

        if (copyButton != null)
            copyButton.onClick.AddListener(CopyLogsToClipboard);

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Ouvrir/Fermer la console avec F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            consolePanel.SetActive(!consolePanel.activeSelf);
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (logQueue.Count >= maxLogCount)
        {
            logQueue.Dequeue(); // Supprime l'ancien log s'il y en a trop
        }

        // Ajoute la couleur selon le type de log
        string color = (type == LogType.Error || type == LogType.Exception) ? "red" :
                       (type == LogType.Warning) ? "yellow" : "white";

        string formattedLog = $"<color={color}>{logString}</color>";
        logQueue.Enqueue(formattedLog);

        // Met à jour le texte de la console
        logText.text = string.Join("\n", logQueue.ToArray());

        // Auto-scroll en bas
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void CopyLogsToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string log in logQueue)
        {
            sb.AppendLine(log);
        }
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Logs copiés dans le presse-papiers !");
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }
}
