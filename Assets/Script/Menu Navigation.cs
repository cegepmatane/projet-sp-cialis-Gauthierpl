using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuNavigation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void LoadEditorScene()
    {
        SceneManager.LoadScene("Editor"); // Assure-toi que "Editor" est dans Build Settings
    }

    public void LoadGameScene()
    {
        SceneManager.LoadScene("SampleScene"); // Assure-toi que "Editor" est dans Build Settings
    }
}
