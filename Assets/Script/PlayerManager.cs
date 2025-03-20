using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public GameObject playerPrefab; // Assignez votre prefab joueur via l'inspecteur

    // Dictionnaire pour suivre les joueurs par leur id
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    void Awake()
    {
        Debug.Log("[PlayerManager] Awake() called");
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
        Debug.Log($"[PlayerManager] HandlePlayerSpawn appel� pour id={id}, pseudo={pseudo}");

        if (!players.ContainsKey(id)) // V�rifier que le joueur n'existe pas encore
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
                    Debug.Log($"[PlayerManager] Joueur local d�tect� (id={id}, pseudo={pseudo}).");
                }
            }
            players.Add(id, newPlayer);
            Debug.Log($"Spawn player: {id} ({pseudo})");
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] Le joueur {id} est d�j� instanci�, annulation du spawn !");
        }
    }


    void HandlePlayerUpdate(string id, float x, float y, bool isRunning, bool isIdle)
    {
        if (id == PlayerData.id) return; // On ignore le joueur local

        if (players.ContainsKey(id))
        {
            GameObject playerObj = players[id];

            // 1) Mise � jour de la position
            Vector3 pos = playerObj.transform.position;
            pos.x = x;
            pos.y = y;
            playerObj.transform.position = pos;

            // 2) Mise � jour de l�animation
            //    Soit on r�cup�re directement le component Animator
            //    Soit on appelle une m�thode du PlayerController (plus propre).
            var playerController = playerObj.GetComponent<PlayerController>();
            if (playerController)
            {
                playerController.UpdateRemoteAnimation(isRunning, isIdle);
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
}
