using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Networking;


public class HeroSelectionBackLogoutButton : MonoBehaviour
{
    public Button backLogoutButton;
    
    private void Start()
    {
        SetupButtons();
    }
    
    private void SetupButtons()
    {
        if (backLogoutButton != null)
        {
            backLogoutButton.onClick.AddListener(OnBackLogoutButtonClicked);
        }
    }
    
    private void OnBackLogoutButtonClicked()
    {
        Session.JwtToken = null;
        Session.Heroes = null;
        Session.SelectedHeroId = 0;
        
        PlayerPrefs.DeleteKey("jwt_token");
        PlayerPrefs.Save();
        
        SceneManager.LoadScene("Login");
    }
}
