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


    void HandlePlayerUpdate(string id, float x, float y)
    {
        if (id == PlayerData.id) return; // Ne pas mettre à jour le joueur local

        if (players.ContainsKey(id))
        {
            GameObject playerObj = players[id];
            Vector3 pos = playerObj.transform.position;
            pos.x = x;
            pos.y = y;
            playerObj.transform.position = pos;
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
