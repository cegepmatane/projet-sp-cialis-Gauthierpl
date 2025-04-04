using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro; // Si tu veux afficher un message de statut

public class MapSaver : MonoBehaviour
{
    [Tooltip("L'URL compl�te de ton script PHP d'enregistrement")]
    public string saveApiUrl = "https://gauthierpl.com/JeuMulti/save_map.php"; // <--- METS TON URL ICI

    public TextMeshProUGUI statusText; // Optionnel: Pour afficher Succ�s/Erreur

    public void SaveMapData(string jsonData)
    {
        StartCoroutine(UploadMapData(jsonData));
    }

    IEnumerator UploadMapData(string jsonData)
    {
        if (string.IsNullOrEmpty(saveApiUrl))
        {
            Debug.LogError("L'URL de l'API d'enregistrement n'est pas d�finie dans MapSaver !");
            if (statusText) statusText.text = "Erreur: URL API manquante";
            yield break;
        }

        if (statusText) statusText.text = "Enregistrement en cours...";

        // UnityWebRequest pr�f�re les bytes, on encode le JSON en UTF8
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // Cr�ation de la requ�te POST
        using (UnityWebRequest www = new UnityWebRequest(saveApiUrl, "POST"))
        {
            www.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            // Important: Sp�cifier le type de contenu
            www.SetRequestHeader("Content-Type", "application/json");

            // Envoyer la requ�te et attendre la r�ponse
            yield return www.SendWebRequest();

            // V�rifier les erreurs
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Erreur lors de l'enregistrement de la map: {www.error}");
                Debug.LogError($"R�ponse serveur (si disponible): {www.downloadHandler.text}");
                if (statusText) statusText.text = $"Erreur: {www.error}";
            }
            else
            {
                Debug.Log($"R�ponse du serveur: {www.downloadHandler.text}");
                if (statusText) statusText.text = "Carte enregistr�e avec succ�s !";
                // Optionnel: Analyser la r�ponse JSON du serveur si elle contient plus d'infos
                // var response = JsonUtility.FromJson<ServerResponse>(www.downloadHandler.text);
            }
        }
    }

    // Optionnel: Une classe pour parser la r�ponse du serveur si elle est en JSON
    // [System.Serializable]
    // public class ServerResponse {
    //     public bool success;
    //     public string message;
    // }
}