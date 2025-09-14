using System.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using SharedLibrary.Requests;
using SharedLibrary.Responses;
using Newtonsoft.Json;

public class LoginManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private UnityEngine.UI.Button registerButton;
    [SerializeField] private string serverBaseUrl = "http://localhost:5172";
    [SerializeField] private string registerSceneName = "Register";
    [SerializeField] private string heroSelectionSceneName = "HeroSelection";
    private bool allowSelfSignedCertificates = true;
    private int requestTimeoutSeconds = 20;

    private void Start()
    {
        SetupButtons();
    }
    
    private void SetupButtons()
    {
        if (registerButton != null)
        {
            registerButton.onClick.AddListener(OnRegisterButtonClicked);
        }
    }

    private void OnRegisterButtonClicked()
    {
        SceneManager.LoadScene(registerSceneName);
    }
    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }

    private bool TryExtractToken(string responseText, out string token)
    {
        token = null;
        if (string.IsNullOrEmpty(responseText)) return false;
        try
        {
            var r = JsonConvert.DeserializeObject<AuthenticationResponse>(responseText);
            if (r != null && !string.IsNullOrEmpty(r.Token)) { token = r.Token; return true; }
        }
        catch { }

        if (responseText.Length > 2 && responseText[0] == '"' && responseText[responseText.Length - 1] == '"')
        {
            token = responseText.Substring(1, responseText.Length - 2);
            if (!string.IsNullOrEmpty(token)) return true;
        }

        int keyIdx = responseText.IndexOf("\"token\"", StringComparison.OrdinalIgnoreCase);
        if (keyIdx >= 0)
        {
            int colon = responseText.IndexOf(':', keyIdx);
            if (colon > keyIdx)
            {
                int q1 = responseText.IndexOf('"', colon + 1);
                if (q1 > 0)
                {
                    int q2 = responseText.IndexOf('"', q1 + 1);
                    if (q2 > q1)
                    {
                        token = responseText.Substring(q1 + 1, q2 - q1 - 1);
                        if (!string.IsNullOrEmpty(token)) return true;
                    }
                }
            }
        }

        return false;
    }

    public void OnSubmitLogin()
    {
        RemoveErrorText();

        string username = usernameInputField.text;
        string password = passwordInputField.text;

        string validate = CheckLoginInfo(username, password);
        if (!string.IsNullOrEmpty(validate))
        {
            errorText.text = "Error: " + validate;
            return;
        }

        StartCoroutine(LoginCoroutine(username, password));

    }

    private IEnumerator LoginCoroutine(string username, string password)
    {
        var req = new AuthenticationRequest { Username = username, Password = password };
        string json = JsonConvert.SerializeObject(req);
        string url = $"{serverBaseUrl}/Authentication/login";

        using (var uwr = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");

            if (allowSelfSignedCertificates)
                uwr.certificateHandler = new BypassCertificateHandler();

            uwr.timeout = Mathf.Max(5, requestTimeoutSeconds);

            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                errorText.text = $"Error: {(int)uwr.responseCode} {uwr.error}";
                yield break;
            }

            string responseText = uwr.downloadHandler.text;
            string token;
            if (!TryExtractToken(responseText, out token))
            {
                errorText.text = "Error: Token rỗng hoặc response không hợp lệ";
                yield break;
            }

            PlayerPrefs.SetString("jwt_token", token);
            PlayerPrefs.Save();
            Session.JwtToken = token;
            Session.ServerBaseUrl = serverBaseUrl;

            if (HeroSelectionManager.instance != null)
            {
                HeroSelectionManager.instance.ForceReloadHeroes();
            }

            SceneManager.LoadScene(heroSelectionSceneName);
        }
    }
    private string CheckLoginInfo(string username, string password)
    {
        if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            return "Both username and password are empty";
        if (string.IsNullOrEmpty(username))
            return "Username was empty";
        if (string.IsNullOrEmpty(password))
            return "Password was empty";
        return "";
    }

    public void RemoveErrorText()
    {
        errorText.text = string.Empty;
    }
}