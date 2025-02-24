using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public GameObject playerPrefab; // Assignez votre prefab joueur via l'inspecteur

    // Dictionnaire pour suivre les joueurs par leur id
    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

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
        if (!players.ContainsKey(id))
        {
            // On instancie le prefab et on l'ajoute au dictionnaire
            GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            PlayerController pc = newPlayer.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.playerId = id;
                pc.SetPseudo(pseudo);
                // Si c'est le joueur local, on le marque
                if (id == PlayerData.id)
                {
                    pc.SetAsLocalPlayer();
                }
            }
            players.Add(id, newPlayer);
            Debug.Log($"Spawn player: {id} ({pseudo})");
        }
    }

    void HandlePlayerUpdate(string id, float x)
    {
        // On ignore la mise à jour du joueur local (qui gère lui-même son déplacement)
        if (id == PlayerData.id) return;
        if (players.ContainsKey(id))
        {
            GameObject playerObj = players[id];
            Vector3 pos = playerObj.transform.position;
            pos.x = x;
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
