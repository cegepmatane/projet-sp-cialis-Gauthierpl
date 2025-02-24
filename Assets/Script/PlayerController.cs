using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public string playerId;
    public string pseudo;
    public float speed = 5f;
    private bool isLocalPlayer = false;

    // Optionnel : référence à un TextMeshPro pour afficher le pseudo au-dessus du joueur
    public TextMeshProUGUI pseudoLabel;

    void Update()
    {
        if (isLocalPlayer)
        {
            float move = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
            if (move != 0)
            {
                transform.Translate(move, 0, 0);
                // Envoyer la nouvelle position au serveur
                NetworkManager.Instance.SendPlayerMove(transform.position.x);
            }
        }
    }

    public void SetPseudo(string pseudo)
    {
        this.pseudo = pseudo;
        if (pseudoLabel != null)
        {
            pseudoLabel.text = pseudo;
        }
    }

    public void SetAsLocalPlayer()
    {
        isLocalPlayer = true;
    }
}
