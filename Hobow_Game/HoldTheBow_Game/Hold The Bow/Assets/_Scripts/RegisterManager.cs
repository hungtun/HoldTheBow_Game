using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using SharedLibrary.Requests;
using SharedLibrary.Responses;
using Newtonsoft.Json;

public class RegisterManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TMP_InputField confirmPasswordInputField;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private UnityEngine.UI.Button registerButton;
    [SerializeField] private UnityEngine.UI.Button backToLoginButton;
    [SerializeField] private string loginSceneName = "Login";
    [SerializeField] private string heroSelectionSceneName = "HeroSelection";
    [SerializeField] private string serverBaseUrl = "http://localhost:5172";
    private bool allowSelfSignedCertificates = true;
    private int requestTimeoutSeconds = 20;

    private void Start()
    {
        Debug.Log("[RegisterManager] Register scene shown");
        SetupButtons();
    }
    
    private void SetupButtons()
    {
        if (registerButton != null)
        {
            registerButton.onClick.AddListener(OnRegisterButtonClicked);
        }
        
        if (backToLoginButton != null)
        {
            backToLoginButton.onClick.AddListener(OnBackToLoginButtonClicked);
        }
    }
    
    private void OnRegisterButtonClicked()
    {
        if (usernameInputField == null || passwordInputField == null || confirmPasswordInputField == null)
        {
            ShowError("Lỗi: UI elements chưa được assign trong Inspector");
            return;
        }
        
        string username = usernameInputField.text ?? "";
        string password = passwordInputField.text ?? "";
        string confirmPassword = confirmPasswordInputField.text ?? "";
        
        string validationError = ValidateInput(username, password, confirmPassword);
        if (!string.IsNullOrEmpty(validationError))
        {
            ShowError(validationError);
            return;
        }
        
        ClearError();
        
        StartCoroutine(RegisterCoroutine(username, password));
    }
    
    private void OnBackToLoginButtonClicked()
    {
        SceneManager.LoadScene(loginSceneName);
    }
    
    private string ValidateInput(string username, string password, string confirmPassword)
    {
        if (string.IsNullOrEmpty(username))
            return "Vui lòng nhập tên đăng nhập";
            
        if (string.IsNullOrEmpty(password))
            return "Vui lòng nhập mật khẩu";
            
        if (password.Length < 6)
            return "Mật khẩu phải có ít nhất 6 ký tự";
            
        if (password != confirmPassword)
            return "Mật khẩu xác nhận không khớp";
            
        return null; 
    }
    
    private IEnumerator RegisterCoroutine(string username, string password)
    {
        var req = new RegisterRequest { Username = username, Password = password };
        string json = JsonConvert.SerializeObject(req);
        string url = $"{serverBaseUrl}/Authentication/register";

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
                ShowError($"Lỗi đăng ký: {(int)uwr.responseCode} {uwr.error}");
                yield break;
            }

            string responseText = uwr.downloadHandler.text;
            
            var auth = JsonConvert.DeserializeObject<AuthenticationResponse>(responseText);
            if (auth != null && !string.IsNullOrEmpty(auth.Token))
            {
                PlayerPrefs.SetString("jwt_token", auth.Token);
                PlayerPrefs.SetString("server_base_url", serverBaseUrl);
                PlayerPrefs.Save();

                Session.JwtToken = auth.Token;
                Session.ServerBaseUrl = serverBaseUrl;

                SceneManager.LoadScene(heroSelectionSceneName);
                yield break;
            }
        }
    }
    
    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = Color.red;
        }
    }
    
    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = "";
        }
    }
    
    private void ClearForm()
    {
        if (usernameInputField != null) usernameInputField.text = "";
        if (passwordInputField != null) passwordInputField.text = "";
        if (confirmPasswordInputField != null) confirmPasswordInputField.text = "";
    }

    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}
