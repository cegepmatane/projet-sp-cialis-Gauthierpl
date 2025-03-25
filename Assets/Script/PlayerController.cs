using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    public string playerId;
    public string pseudo;
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    private bool isLocalPlayer = false;
    private Rigidbody2D rb;
    private Animator animator;

    private bool jumpRequest = false;
    private float moveInput = 0f;

    public TextMeshProUGUI pseudoLabel;
    public SpriteRenderer spriteRenderer;

    //Références vers la bulle
    public GameObject chatBubble;          // On assigne l'objet "ChatBubble" depuis l'Inspecteur
    public TextMeshProUGUI chatBubbleText; // On assigne le TMP (le texte dans la bulle)
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        ApplyRandomColorForPlayerPrefab();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // Récupération des inputs (horizontal + bouton de saut)
        moveInput = Input.GetAxis("Horizontal");

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpRequest = true;
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        // 1) Appliquer le mouvement
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        // 2) Gérer le saut
        if (jumpRequest)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpRequest = false;
        }

        // 3) Calculer l'état d'animation
        bool isRunning = Mathf.Abs(moveInput) > 0.01f;
        bool isIdle = !isRunning;

        // 4) Mettre à jour l'anim côté local
        animator.SetBool("IsRunning", isRunning);
        animator.SetBool("IsIdle", isIdle);

        // 5) Envoyer la position & l'état d'anim au serveur
        NetworkManager.Instance.SendPlayerMove(
            transform.position.x,
            transform.position.y,
            isRunning,
            isIdle
        );
    }

    // Pour mettre à jour la couleur du joueur (exemple)
    void ApplyRandomColorForPlayerPrefab()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(
                Random.Range(0f, 1f),
                Random.Range(0f, 1f),
                Random.Range(0f, 1f)
            );
        }
    }

    // Appelé seulement sur les autres joueurs quand on reçoit l'événement réseau 'updatePlayer'
    public void UpdateRemoteAnimation(bool isRunning, bool isIdle)
    {
        animator.SetBool("IsRunning", isRunning);
        animator.SetBool("IsIdle", isIdle);
    }

    // Pour attribuer le pseudo
    public void SetPseudo(string pseudo)
    {
        this.pseudo = pseudo;
        if (pseudoLabel != null)
        {
            pseudoLabel.text = pseudo;
        }
    }

    // Détermine si c'est le joueur local
    public void SetAsLocalPlayer()
    {
        isLocalPlayer = true;
    }


    // 2) Méthode pour afficher le message pendant un certain temps
    public void DisplayChatMessage(string message, float duration = 10f)
    {
        if (chatBubble == null || chatBubbleText == null) return;

        // Affiche la bulle
        chatBubble.SetActive(true);
        chatBubbleText.text = message;

        // Au cas où plusieurs messages rapides arrivent, on arrête les coroutines précédentes
        StopAllCoroutines();
        StartCoroutine(HideBubbleAfterSeconds(duration));
    }

    private IEnumerator HideBubbleAfterSeconds(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Cache la bulle
        chatBubble.SetActive(false);
    }
}
