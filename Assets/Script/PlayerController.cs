// Fichier: Script unity/Script/PlayerController.cs
using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Player Info")]
    [ReadOnly] public string playerId;
    [ReadOnly] public string pseudo;
    public bool IsLocalPlayer => isLocalPlayer;

    [Header("Movement Settings")]
    [Tooltip("Force appliquée pour le mouvement horizontal.")]
    public float moveForce = 35f;
    [Tooltip("Vitesse horizontale maximale que le joueur peut atteindre.")]
    public float maxSpeed = 8f;
    [Tooltip("Force appliquée vers le haut pour le saut.")]
    public float jumpForce = 20f;
    // Suppression des variables liées au Ground Check :
    // public float groundCheckRadius = 0.2f;
    // public Transform groundCheckPoint;
    // public LayerMask groundLayer;

    [Header("Gameplay State")]
    [SerializeField][ReadOnly] private bool hasMouseGoal = false;
    [SerializeField][ReadOnly] private bool isDead = false;

    [Header("Death Condition")]
    public float deathYThreshold = -10f;

    [Header("Component References")]
    public TextMeshProUGUI pseudoLabel;
    public SpriteRenderer spriteRenderer;
    public Animator animator; // Gardé pour Run/Idle/Jump si tu les as
    public GameObject chatBubble;
    public TextMeshProUGUI chatBubbleText;

    // --- Internal State ---
    private bool isLocalPlayer = false;
    private Rigidbody2D rb;
    private Collider2D playerCollider;

    // Variables pour les inputs
    private bool jumpRequest = false;
    private float moveInput = 0f;
    // Suppression de isGrounded
    private Coroutine hideBubbleCoroutine = null;

    // --- Initialization ---
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>(); // Récupère l'Animator

        // Vérifications initiales
        if (rb == null) Debug.LogError($"[{gameObject.name}] Rigidbody2D manquant !", this);
        if (playerCollider == null) Debug.LogError($"[{gameObject.name}] Collider2D manquant !", this);
        if (spriteRenderer == null) Debug.LogWarning($"[{gameObject.name}] SpriteRenderer non trouvé.", this);
        // Garder le warning pour l'animator si tu l'utilises
        if (animator == null) Debug.LogWarning($"[{gameObject.name}] Animator non trouvé.", this);
        // Suppression des warnings liés au GroundCheck

        if (pseudoLabel) pseudoLabel.text = "";
        if (chatBubble) chatBubble.SetActive(false);
    }

    // --- Input Handling (Local Player Only) ---
    void Update()
    {
        if (!isLocalPlayer || isDead)
        {
            // Arrêter le mouvement si mort
            if (isDead && rb != null && rb.simulated)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            return;
        }

        // Lire les inputs horizontaux
        moveInput = Input.GetAxis("Horizontal"); // Donne une valeur entre -1 et 1

        // Demande de saut SANS vérifier si on est au sol
        if (Input.GetButtonDown("Jump")) // Utilise le bouton "Jump" défini dans Input Manager
        {
            jumpRequest = true;
        }

        // Vérification de la mort par chute (inchangé)
        if (transform.position.y < deathYThreshold)
        {
            Die();
        }
    }

    // --- Physics Update ---
    void FixedUpdate()
    {
        // Suppression de l'appel à CheckGrounded()

        if (!isLocalPlayer || isDead) return; // Ne rien faire si non local ou mort

        // --- Mouvement Horizontal avec AddForce et Limite de Vitesse ---
        // Appliquer une force seulement si la vitesse actuelle est inférieure à la vitesse max
        // ou si on essaie de changer de direction.
        if (Mathf.Abs(rb.linearVelocity.x) < maxSpeed || Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(moveInput))
        {
            // AddForce est affecté par la masse, ajuste moveForce si nécessaire
            rb.AddForce(Vector2.right * moveInput * moveForce);
        }

        // Optionnel : Freinage si aucune touche n'est appuyée (pour éviter de glisser indéfiniment)
        if (Mathf.Abs(moveInput) < 0.1f && rb.linearVelocity.x != 0)
        {
            // Applique une force opposée pour freiner, ou réduit la vélocité
            // rb.AddForce(Vector2.left * Mathf.Sign(rb.velocity.x) * moveForce * 0.5f); // Exemple avec force
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.90f, rb.linearVelocity.y); // Exemple avec réduction de vélocité (plus simple)
        }


        // --- Saut avec AddForce ---
        if (jumpRequest)
        {
            // Appliquer une force verticale instantanée (ForceMode2D.Impulse ignore la masse)
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpRequest = false; // Réinitialiser la demande

            // Déclencher l'animation de saut si l'animator est présent
            if (animator) animator.SetTrigger("Jump"); // Assure-toi d'avoir un Trigger "Jump" dans ton Animator
        }
        // --- Fin Saut ---

        // Mettre à jour les animations et l'orientation du sprite
        UpdateAnimationsAndFlip();

        // Envoyer l'état au serveur via NetworkManager
        // Adapte isRunning et isIdle comme tu le souhaites
        bool isRunning = Mathf.Abs(rb.linearVelocity.x) > 0.1f; // Considéré comme courant si vitesse H non nulle
        bool isIdle = !isRunning && Mathf.Abs(rb.linearVelocity.y) < 0.1f; // Considéré idle si quasi immobile
        NetworkManager.Instance?.SendPlayerMove(
            transform.position.x,
            transform.position.y,
            isRunning,
            isIdle,
            spriteRenderer != null ? spriteRenderer.flipX : false // état du flip
        );
    }

    // Met à jour l'animator et l'orientation du sprite localement
    void UpdateAnimationsAndFlip()
    {
        // Détermine si le personnage court (basé sur la vitesse horizontale)
        bool isRunning = Mathf.Abs(rb.linearVelocity.x) > 0.1f;

        // Met à jour l'Animator local (si tu en as un)
        if (animator != null)
        {
            animator.SetBool("IsRunning", isRunning);
            // Suppression de IsGrounded
            // animator.SetBool("IsGrounded", false); // Ou le supprimer complètement de l'animator
            // Garder VerticalVelocity peut être utile pour l'animation de saut/chute
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        }

        // Gère l'orientation du sprite (flip) basé sur l'INPUT pour la réactivité
        if (spriteRenderer != null)
        {
            if (moveInput < -0.01f) spriteRenderer.flipX = true;  // Regarde à gauche
            else if (moveInput > 0.01f) spriteRenderer.flipX = false; // Regarde à droite
            // Ne change pas d'orientation si l'input est neutre
        }
    }

    // --- Méthodes pour Joueurs Distants (Mise à jour simplifiée) ---
    public void UpdateRemoteState(bool remoteIsRunning, bool remoteIsIdle, bool remoteFlip)
    {
        if (isLocalPlayer || isDead) return; // Ne pas appliquer si local ou mort

        // Met à jour l'Animator distant (IsRunning)
        if (animator != null)
        {
            animator.SetBool("IsRunning", remoteIsRunning);
            // On ne met PAS à jour VerticalVelocity ici car on ne la connaît pas pour les joueurs distants
        }

        // Applique le flip reçu
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = remoteFlip;
        }
    }

    // --- Logique de Mort (inchangée, sauf peut-être reset animator) ---
    private void Die()
    {
        if (!isLocalPlayer || isDead) return;
        // ... (code existant pour désactiver composants, etc.) ...
        isDead = true;
        hasMouseGoal = false; // Perd le butin
        if (spriteRenderer) spriteRenderer.enabled = false;
        if (playerCollider) playerCollider.enabled = false;
        if (rb) { rb.simulated = false; rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
        if (pseudoLabel) pseudoLabel.enabled = false;
        if (chatBubble) chatBubble.SetActive(false);
        NetworkManager.Instance?.SendPlayerDied();
    }

    // --- Logique de Respawn (inchangée, sauf peut-être reset animator) ---
    public void ReviveAndMoveToSpawn(Vector3 spawnPosition)
    {
        if (!IsLocalPlayer) return;
        // ... (code existant pour réactiver composants, position, etc.) ...
        isDead = false;
        hasMouseGoal = false; // Réinitialise l'état du butin
        transform.position = spawnPosition;
        if (spriteRenderer) spriteRenderer.enabled = true;
        if (playerCollider) playerCollider.enabled = true;
        if (rb) { rb.simulated = true; rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
        if (pseudoLabel) pseudoLabel.enabled = true;

        // Réinitialiser les états d'animation
        if (animator)
        {
            animator.SetBool("IsRunning", false);
            // Suppression de IsGrounded
            animator.SetFloat("VerticalVelocity", 0f);
            // Peut-être forcer un retour à l'état Idle si tu as un trigger pour ça
            // animator.ResetTrigger("Jump"); // Si tu utilises un trigger de saut
        }
        moveInput = 0f;
        jumpRequest = false;
    }

    // --- Détection des Triggers (inchangée) ---
    void OnTriggerEnter2D(Collider2D other)
    {
        // ... (code de collision inchangé) ...
        if (!isLocalPlayer || isDead) return;
        if (other.gameObject.CompareTag("MouseGoal")) { /* ... */ }
        else if (other.gameObject.CompareTag("CatTree")) { /* ... */ }
    }

    // --- Méthodes appelées par PlayerManager (inchangées) ---
    public void SetNetworkId(string networkId) { this.playerId = networkId; }
    public void SetPseudo(string pseudo) { this.pseudo = pseudo; if (pseudoLabel != null) pseudoLabel.text = pseudo; }
    public void SetColor(Color color) { if (spriteRenderer != null) spriteRenderer.color = color; }
    public void SetAsLocalPlayer() { isLocalPlayer = true; }

    // --- Gestion Chat Bubble ---
    public void DisplayChatMessage(string message, float duration = 5f)
    {
        // Vérifier si les références nécessaires existent
        if (chatBubble && chatBubbleText)
        {
            // Afficher le message et activer la bulle
            chatBubbleText.text = message;
            chatBubble.SetActive(true);

            // Si une coroutine précédente tournait pour cacher la bulle, l'arrêter
            if (hideBubbleCoroutine != null)
            {
                StopCoroutine(hideBubbleCoroutine);
            }
            // Démarrer une nouvelle coroutine pour cacher la bulle après 'duration' secondes
            hideBubbleCoroutine = StartCoroutine(HideBubbleAfterSeconds(duration));
        }
        // Optionnel : Log d'avertissement si les composants UI manquent
        else { Debug.LogWarning($"[{gameObject.name}] Références ChatBubble ou ChatBubbleText manquantes pour afficher le message."); }
    }

    // Méthode Coroutine pour cacher la bulle après un délai
    // Assure-toi que la signature est bien "IEnumerator"
    private IEnumerator HideBubbleAfterSeconds(float delay)
    {
        // Pause l'exécution de cette coroutine pour la durée 'delay'
        yield return new WaitForSeconds(delay);

        // Après l'attente, cacher la bulle si elle existe encore
        if (chatBubble)
        {
            chatBubble.SetActive(false);
        }
        // Réinitialiser la référence à la coroutine pour indiquer qu'elle est terminée
        hideBubbleCoroutine = null;
    }
    // --- Fin Gestion Chat Bubble ---

    // --- Gizmos (supprimés ou à adapter si besoin) ---
    // void OnDrawGizmosSelected() { /* ... */ }

    // --- Attribut ReadOnly (inchangé) ---
    public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        // Correction : Ajout de la méthode OnGUI qui manquait dans le placeholder
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false; // Désactive l'édition
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true; // Réactive l'édition pour les autres champs
        }
    }
#endif
} // Fin de la classe PlayerController