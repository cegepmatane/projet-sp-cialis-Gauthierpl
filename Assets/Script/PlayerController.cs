using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public string playerId;
    public string pseudo;
    public float speed = 5f;
    private bool isLocalPlayer = false;

    public float jumpForce = 5f; // Force du saut
    private Rigidbody2D rb;
    private bool jumpRequest = false;


    // Optionnel : r�f�rence � un TextMeshPro pour afficher le pseudo au-dessus du joueur
    public TextMeshProUGUI pseudoLabel;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    void Update()
    {
        if (isLocalPlayer && Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Z))
        {
            jumpRequest = true;
        }
    }


    // Retire le mouvement depuis Update() et place-le dans FixedUpdate()
    void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            float moveX = Input.GetAxis("Horizontal") * speed * Time.fixedDeltaTime;
            transform.Translate(moveX, 0, 0);

            // Appliquer le saut si une demande est en attente
            if (jumpRequest)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpRequest = false; // Réinitialisation de la demande de saut
            }

            // Envoi de la position x et y au serveur
            NetworkManager.Instance.SendPlayerMove(transform.position.x, transform.position.y);
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
