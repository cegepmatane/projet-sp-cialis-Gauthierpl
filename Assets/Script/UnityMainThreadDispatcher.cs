using System;
using System.Collections.Concurrent; // Utiliser ConcurrentQueue pour la sécurité des threads
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    // Utiliser ConcurrentQueue pour une meilleure sécurité des threads par rapport au verrouillage de Queue
    private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();
    private static UnityMainThreadDispatcher _instance = null;
    private static bool _instanceExists = false; // Drapeau pour vérifier si l'instance a été trouvée/créée

    // S'assurer que cela s'exécute sur le thread principal lorsque la scène se charge
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            _instanceExists = true;
            DontDestroyOnLoad(this.gameObject); // S'assurer qu'il persiste entre les scènes si nécessaire
            Debug.Log("[UnityMainThreadDispatcher] Instance initialisée via Awake.");
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Instance dupliquée trouvée. Destruction de soi-même.");
            Destroy(gameObject);
        }
    }

    // Méthode statique pour obtenir l'instance
    public static UnityMainThreadDispatcher Instance()
    {
        if (!_instanceExists)
        {
            // Tenter de le trouver UNIQUEMENT si nous sommes sur le thread principal
            if (IsMainThread()) // Nécessite un moyen de vérifier si on est sur le thread principal
            {
                // Utiliser UnityEngine.Object.FindFirstObjectByType explicitement
                _instance = UnityEngine.Object.FindFirstObjectByType<UnityMainThreadDispatcher>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("UnityMainThreadDispatcher");
                    _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    // Note : Awake s'exécutera sur la nouvelle instance immédiatement ou à la prochaine frame
                    Debug.LogWarning("[UnityMainThreadDispatcher] Instance créée dynamiquement. Assurez-vous qu'une instance existe dans la scène.");
                }
                _instanceExists = (_instance != null); // Mettre à jour le drapeau basé sur la recherche/création
            }
            else
            {
                // Erreur critique si accédé depuis un thread d'arrière-plan avant initialisation
                Debug.LogError("[UnityMainThreadDispatcher] CRITIQUE : Tentative d'accès à Instance() depuis un thread d'arrière-plan avant son initialisation sur le thread principal ! Enqueue échouera.");
                // Retourner null pourrait être plus sûr que de lever une exception qui tue le thread
                return null;
            }
        }
        // Si Awake a été exécuté ou si nous l'avons trouvé/créé sur le thread principal, _instance devrait être défini.
        return _instance;
    }

    // Aide pour vérifier si nous sommes sur le thread principal (nécessite un moyen de stocker l'ID du thread principal)
    private static int _mainThreadId;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeMainThreadContext()
    {
        _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
    }
    public static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;
    }


    // Utiliser Enqueue de ConcurrentQueue
    public void Enqueue(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }
        _executionQueue.Enqueue(action);
    }

    // Traiter la file d'attente dans Update (s'exécute sur le thread principal)
    private void Update()
    {
        // Retirer et exécuter les actions
        while (_executionQueue.TryDequeue(out Action action))
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Erreur lors de l'exécution de l'action : {ex}");
            }
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _instanceExists = false;
            Debug.Log("[UnityMainThreadDispatcher] Instance détruite.");
        }
    }
}