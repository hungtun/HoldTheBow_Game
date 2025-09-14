using UnityEngine;

public class SessionLoader : MonoBehaviour
{
    private void Awake()
    {
        LoadSessionFromPlayerPrefs();
    }
    
    private void LoadSessionFromPlayerPrefs()
    {
        if (string.IsNullOrEmpty(Session.JwtToken))
        {
            string savedToken = PlayerPrefs.GetString("jwt_token", "");
            string savedServerUrl = PlayerPrefs.GetString("server_base_url", "");
            
            if (!string.IsNullOrEmpty(savedToken))
            {
                Session.JwtToken = savedToken;
                Session.ServerBaseUrl = savedServerUrl;
                
                Debug.Log($"[SessionLoader] Loaded session - Server: {Session.ServerBaseUrl}, Token: {(string.IsNullOrEmpty(Session.JwtToken) ? "Empty" : "Present")}");
            }
            else
            {
                Debug.LogWarning("[SessionLoader] No saved session found. User needs to login first.");
            }
        }
    }
}
