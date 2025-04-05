using System;
using System.Collections.Concurrent; // Utiliser ConcurrentQueue pour la s�curit� des threads
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    // Utiliser ConcurrentQueue pour une meilleure s�curit� des threads par rapport au verrouillage de Queue
    private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();
    private static UnityMainThreadDispatcher _instance = null;
    private static bool _instanceExists = false; // Drapeau pour v�rifier si l'instance a �t� trouv�e/cr��e

    // S'assurer que cela s'ex�cute sur le thread principal lorsque la sc�ne se charge
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            _instanceExists = true;
            DontDestroyOnLoad(this.gameObject); // S'assurer qu'il persiste entre les sc�nes si n�cessaire
            Debug.Log("[UnityMainThreadDispatcher] Instance initialis�e via Awake.");
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[UnityMainThreadDispatcher] Instance dupliqu�e trouv�e. Destruction de soi-m�me.");
            Destroy(gameObject);
        }
    }

    // M�thode statique pour obtenir l'instance
    public static UnityMainThreadDispatcher Instance()
    {
        if (!_instanceExists)
        {
            // Tenter de le trouver UNIQUEMENT si nous sommes sur le thread principal
            if (IsMainThread()) // N�cessite un moyen de v�rifier si on est sur le thread principal
            {
                // Utiliser UnityEngine.Object.FindFirstObjectByType explicitement
                _instance = UnityEngine.Object.FindFirstObjectByType<UnityMainThreadDispatcher>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("UnityMainThreadDispatcher");
                    _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    // Note : Awake s'ex�cutera sur la nouvelle instance imm�diatement ou � la prochaine frame
                    Debug.LogWarning("[UnityMainThreadDispatcher] Instance cr��e dynamiquement. Assurez-vous qu'une instance existe dans la sc�ne.");
                }
                _instanceExists = (_instance != null); // Mettre � jour le drapeau bas� sur la recherche/cr�ation
            }
            else
            {
                // Erreur critique si acc�d� depuis un thread d'arri�re-plan avant initialisation
                Debug.LogError("[UnityMainThreadDispatcher] CRITIQUE : Tentative d'acc�s � Instance() depuis un thread d'arri�re-plan avant son initialisation sur le thread principal ! Enqueue �chouera.");
                // Retourner null pourrait �tre plus s�r que de lever une exception qui tue le thread
                return null;
            }
        }
        // Si Awake a �t� ex�cut� ou si nous l'avons trouv�/cr�� sur le thread principal, _instance devrait �tre d�fini.
        return _instance;
    }

    // Aide pour v�rifier si nous sommes sur le thread principal (n�cessite un moyen de stocker l'ID du thread principal)
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

    // Traiter la file d'attente dans Update (s'ex�cute sur le thread principal)
    private void Update()
    {
        // Retirer et ex�cuter les actions
        while (_executionQueue.TryDequeue(out Action action))
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMainThreadDispatcher] Erreur lors de l'ex�cution de l'action : {ex}");
            }
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _instanceExists = false;
            Debug.Log("[UnityMainThreadDispatcher] Instance d�truite.");
        }
    }
}