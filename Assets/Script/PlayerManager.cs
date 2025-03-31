using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public GameObject playerPrefab; // Assignez votre prefab joueur via l'inspecteur

    // Dictionnaire pour suivre les joueurs par leur id
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

    public static PlayerManager Instance; // (optionnel) si tu veux y accéder statiquement Assure-toi que PlayerManager.Instance = this; est bien mis dans Awake() ou Start(), pour qu’on puisse l’appeler depuis un autre script.


    void Awake()
    {
        Debug.Log("[PlayerManager] Awake() called");
        Instance = this; // Pour qu'on puisse faire PlayerManager.Instance.DisplayChatBubble(...)

    }

    private void OnEnable()
    {
        NetworkManager.OnPlayerSpawn += HandlePlayerSpawn;
        NetworkManager.OnPlayerUpdate += HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove += HandlePlayerRemove;
    }

    private void OnDisable()
    {
        NetworkManager.OnPlayerSpawn -= HandlePlayerSpawn;
        NetworkManager.OnPlayerUpdate -= HandlePlayerUpdate;
        NetworkManager.OnPlayerRemove -= HandlePlayerRemove;
    }


    void HandlePlayerSpawn(string id, string pseudo)
    {
        Debug.Log($"[PlayerManager] HandlePlayerSpawn appelé pour id={id}, pseudo={pseudo}");

        if (!players.ContainsKey(id)) // Vérifier que le joueur n'existe pas encore
        {
            Debug.Log("[PlayerManager] Instantiation du prefab...");
            GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

            PlayerController pc = newPlayer.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.playerId = id;
                pc.SetPseudo(pseudo);

                if (id == PlayerData.id) // Si c'est le joueur local, on le marque
                {
                    pc.SetAsLocalPlayer();
                    Debug.Log($"[PlayerManager] Joueur local détecté (id={id}, pseudo={pseudo}).");
                }
            }
            players.Add(id, newPlayer);
            Debug.Log($"Spawn player: {id} ({pseudo})");
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] Le joueur {id} est déjà instancié, annulation du spawn !");
        }
    }


    void HandlePlayerUpdate(string id, float x, float y, bool isRunning, bool isIdle, bool flip)
    {
        if (id == PlayerData.id) return; // on ignore le joueur local

        if (players.ContainsKey(id))
        {
            GameObject playerObj = players[id];

            // Mise à jour de la position
            Vector3 pos = playerObj.transform.position;
            pos.x = x;
            pos.y = y;
            playerObj.transform.position = pos;

            // Mise à jour de l'animation et de l'orientation
            var playerController = playerObj.GetComponent<PlayerController>();
            if (playerController)
            {
                playerController.UpdateRemoteAnimation(isRunning, isIdle);
                playerController.spriteRenderer.flipX = flip;
            }
        }
    }




    void HandlePlayerRemove(string id)
    {
        if (players.ContainsKey(id))
        {
            Destroy(players[id]);
            players.Remove(id);
        }
    }


    public void DisplayChatBubble(string playerId, string msg)
    {
        if (!players.ContainsKey(playerId)) return;
        GameObject playerObj = players[playerId];

        PlayerController pc = playerObj.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.DisplayChatMessage(msg, 10f); // on choisit 10 secondes
        }
    }
}
