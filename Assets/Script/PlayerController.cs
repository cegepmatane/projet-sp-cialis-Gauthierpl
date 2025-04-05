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
    public float moveForce = 35f;
    public float maxSpeed = 8f;
    public float jumpForce = 20f;

    [Header("Gameplay State")]
    [SerializeField][ReadOnly] private bool hasMouseGoal = false;
    // <<< MODIFICATION: isDead est maintenant surtout pour la logique locale avant respawn >>>
    [SerializeField][ReadOnly] private bool isLocallyDead = false;

    [Header("Death Condition")]
    public float deathYThreshold = -10f;

    [Header("Component References")]
    public TextMeshProUGUI pseudoLabel;
    public SpriteRenderer spriteRenderer; // Gardé public pour accès par PlayerManager si besoin
    public Animator animator;
    public GameObject chatBubble;
    public TextMeshProUGUI chatBubbleText;

    // --- Internal State ---
    private bool isLocalPlayer = false;
    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private bool jumpRequest = false;
    private float moveInput = 0f;
    private Coroutine hideBubbleCoroutine = null;

    // --- Initialization ---
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        // Récupérer SpriteRenderer même s'il est dans un enfant (plus robuste)
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true); // true pour inclure inactifs
        if (animator == null) animator = GetComponent<Animator>();

        if (rb == null) Debug.LogError($"[{gameObject.name}] Rigidbody2D manquant !", this);
        if (playerCollider == null) Debug.LogError($"[{gameObject.name}] Collider2D manquant !", this);
        if (spriteRenderer == null) Debug.LogWarning($"[{gameObject.name}] SpriteRenderer non trouvé.", this);
        if (animator == null) Debug.LogWarning($"[{gameObject.name}] Animator non trouvé.", this);

        if (pseudoLabel) pseudoLabel.text = "";
        if (chatBubble) chatBubble.SetActive(false);
    }

    // --- Input Handling (Local Player Only) ---
    void Update()
    {
        // <<< MODIFICATION: Vérifier isLocallyDead >>>
        if (!isLocalPlayer || isLocallyDead)
        {
            // Arrêter le mouvement si mort localement (sécurité)
            if (isLocallyDead && rb != null && rb.simulated)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            return;
        }

        moveInput = Input.GetAxis("Horizontal");
        if (Input.GetButtonDown("Jump")) { jumpRequest = true; }

        if (transform.position.y < deathYThreshold)
        {
            Die();
        }
    }

    // --- Physics Update ---
    void FixedUpdate()
    {
        // <<< MODIFICATION: Vérifier isLocallyDead >>>
        if (!isLocalPlayer || isLocallyDead) return;

        // Mouvement Horizontal
        if (Mathf.Abs(rb.linearVelocity.x) < maxSpeed || Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(moveInput))
        {
            rb.AddForce(Vector2.right * moveInput * moveForce);
        }
        if (Mathf.Abs(moveInput) < 0.1f && rb.linearVelocity.x != 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.90f, rb.linearVelocity.y);
        }

        // Saut
        if (jumpRequest)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpRequest = false;
            if (animator) animator.SetTrigger("Jump");
        }

        UpdateAnimationsAndFlip();

        // Envoyer l'état au serveur
        bool isRunning = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        bool isIdle = !isRunning && Mathf.Abs(rb.linearVelocity.y) < 0.1f;
        NetworkManager.Instance?.SendPlayerMove(
            transform.position.x, transform.position.y, isRunning, isIdle,
            spriteRenderer != null ? spriteRenderer.flipX : false,
            rb.linearVelocity.x, rb.linearVelocity.y
        );
    }

    // UpdateAnimationsAndFlip (Inchangé)
    void UpdateAnimationsAndFlip()
    {
        bool isRunning = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        if (animator != null)
        {
            animator.SetBool("IsRunning", isRunning);
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        }
        if (spriteRenderer != null)
        {
            if (moveInput < -0.01f) spriteRenderer.flipX = true;
            else if (moveInput > 0.01f) spriteRenderer.flipX = false;
        }
    }

    // UpdateRemoteState (Inchangé - met à jour anim/flip pour distants)
    public void UpdateRemoteState(bool remoteIsRunning, bool remoteIsIdle, bool remoteFlip)
    {
        if (isLocalPlayer || isLocallyDead) return; // Ne pas appliquer si local ou marqué comme mort localement

        if (animator != null)
        {
            animator.SetBool("IsRunning", remoteIsRunning);
            // Mettre à jour la vélocité verticale basée sur le RB distant (géré par PlayerManager)
            // pour que l'animation de saut/chute fonctionne visuellement
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = remoteFlip;
        }
    }

    // --- Logique de Mort (MODIFIÉE) ---
    private void Die()
    {
        // <<< MODIFICATION: Vérifier isLocallyDead >>>
        if (!isLocalPlayer || isLocallyDead) return;

        Debug.Log($"[PlayerController {playerId}] Fonction Die() appelée.");
        isLocallyDead = true; // Marque comme mort localement
        hasMouseGoal = false; // Perd le butin

        // Désactiver les composants pour l'effet visuel immédiat localement
        if (spriteRenderer) spriteRenderer.enabled = false;
        if (playerCollider) playerCollider.enabled = false;
        if (rb) { rb.simulated = false; rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
        if (pseudoLabel) pseudoLabel.enabled = false;
        if (chatBubble) chatBubble.SetActive(false);

        // Informer le serveur que ce joueur est mort (le serveur déclenchera removePlayer)
        NetworkManager.Instance?.SendPlayerDied();
        Debug.Log($"[PlayerController {playerId}] SendPlayerDied() envoyé au serveur.");
    }

    // --- Logique de Respawn (MODIFIÉE - Utilisée pour le spawn initial et respawn LOCAL) ---
    public void ReviveAndMoveToSpawn(Vector3 spawnPosition)
    {
        // Cette méthode est maintenant appelée par NetworkManager UNIQUEMENT pour le joueur local
        // quand l'événement 'spawnPlayer' correspondant à son ID est reçu.
        if (!IsLocalPlayer)
        {
            Debug.LogWarning($"[PlayerController {playerId}] ReviveAndMoveToSpawn appelé sur un joueur non local ! Ignoré.");
            return;
        }

        Debug.Log($"[PlayerController {playerId}] ReviveAndMoveToSpawn appelé à la position {spawnPosition}.");
        isLocallyDead = false; // N'est plus mort localement
        hasMouseGoal = false; // Réinitialise l'état du butin

        // Placer à la position de spawn
        transform.position = spawnPosition;

        // Réactiver les composants
        if (spriteRenderer) spriteRenderer.enabled = true;
        if (playerCollider) playerCollider.enabled = true;
        if (rb)
        {
            rb.simulated = true; // Réactive la physique
            rb.linearVelocity = Vector2.zero; // Reset la vélocité
            rb.angularVelocity = 0f;
        }
        if (pseudoLabel) pseudoLabel.enabled = true;

        // Réinitialiser les états d'animation
        if (animator)
        {
            animator.SetBool("IsRunning", false);
            animator.SetFloat("VerticalVelocity", 0f);
            animator.Play("Idle", -1, 0f); // Force l'état Idle
            animator.ResetTrigger("Jump");
        }
        moveInput = 0f;
        jumpRequest = false;
    }

    // <<< AJOUT: Méthode pour rendre inactif au démarrage local >>>
    public void SetInitialInactiveState()
    {
        if (!isLocalPlayer) return; // Sécurité
        Debug.Log($"[PlayerController {playerId}] SetInitialInactiveState appelé.");
        isLocallyDead = true; // Considéré comme "mort" initialement pour bloquer input/updates
        if (spriteRenderer) spriteRenderer.enabled = false;
        if (playerCollider) playerCollider.enabled = false;
        if (rb) rb.simulated = false;
        if (pseudoLabel) pseudoLabel.enabled = false;
        if (chatBubble) chatBubble.SetActive(false);
    }


    // --- Détection des Triggers (MODIFIÉE - Vérifie isLocallyDead) ---
    void OnTriggerEnter2D(Collider2D other)
    {
        // <<< MODIFICATION: Vérifier isLocallyDead >>>
        if (!isLocalPlayer || isLocallyDead) return;

        if (other.gameObject.CompareTag("MouseGoal"))
        {
            Debug.Log("Trigger MouseGoal détecté");
            if (!hasMouseGoal) { hasMouseGoal = true; Debug.Log("Objectif souris obtenu !"); }
        }
        else if (other.gameObject.CompareTag("CatTree"))
        {
            Debug.Log("Trigger CatTree détecté");
            if (hasMouseGoal)
            {
                Debug.Log("VICTOIRE ! Objectif atteint !");
                hasMouseGoal = false;
                Die(); // Considérer la victoire comme une "mort" pour respawn
            }
            else { Debug.Log("Arbre à chat atteint, mais il faut d'abord la souris !"); }
        }
    }


    // --- Méthodes appelées par PlayerManager (Setters inchangés) ---
    public void SetNetworkId(string networkId) { this.playerId = networkId; }
    public void SetPseudo(string pseudo) { this.pseudo = pseudo; if (pseudoLabel != null) pseudoLabel.text = pseudo; }
    public void SetColor(Color color) { if (spriteRenderer != null) spriteRenderer.color = color; }
    public void SetAsLocalPlayer() { isLocalPlayer = true; }

    // --- Gestion Chat Bubble --- (Inchangé)
    public void DisplayChatMessage(string message, float duration = 5f)
    {
        if (chatBubble && chatBubbleText)
        {
            chatBubbleText.text = message;
            chatBubble.SetActive(true);
            if (hideBubbleCoroutine != null) { StopCoroutine(hideBubbleCoroutine); }
            hideBubbleCoroutine = StartCoroutine(HideBubbleAfterSeconds(duration));
        }
        else { Debug.LogWarning($"[{gameObject.name}] Références ChatBubble manquantes."); }
    }
    private IEnumerator HideBubbleAfterSeconds(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (chatBubble) { chatBubble.SetActive(false); }
        hideBubbleCoroutine = null;
    }
    // --- Fin Gestion Chat Bubble ---

    // --- Attribut ReadOnly (inchangé) ---
    public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
}