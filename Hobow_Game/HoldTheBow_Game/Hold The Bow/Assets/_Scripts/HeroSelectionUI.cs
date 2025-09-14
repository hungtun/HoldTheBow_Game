using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Networking;
using Newtonsoft.Json;
using SharedLibrary.Responses;
using System;


public class HeroSelectionUI : MonoBehaviour
{
    public GameObject optionPrefab;
    public Button startButton;
    public Button createHeroButton;
    public TMP_InputField createHeroNameInput;
    public GameObject createHeroPanel; 
    public Button confirmCreateHeroButton;
    public Button cancelCreateHeroButton;
    public Transform prevHero;
    public Transform selectedHero;
    public string gameplaySceneName = "Gameplay";
    private void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        while (HeroSelectionManager.instance == null)
            yield return new WaitForSeconds(0.1f);

        while (!HeroSelectionManager.instance.IsLoaded)
            yield return null;

        if (optionPrefab == null) yield break;

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false;
        }

        if (createHeroButton != null) createHeroButton.onClick.AddListener(OnCreateHeroClicked);

        if (createHeroPanel != null) createHeroPanel.SetActive(false);
        if (confirmCreateHeroButton != null) confirmCreateHeroButton.onClick.AddListener(OnConfirmCreateHero);
        if (cancelCreateHeroButton != null) cancelCreateHeroButton.onClick.AddListener(OnCancelCreateHero);
        
        if (HeroSelectionManager.instance.heroes != null && HeroSelectionManager.instance.heroes.Length > 0)
        {
            foreach (Session.HeroSummary h in HeroSelectionManager.instance.heroes)
            {
                if (h == null) continue;
                GameObject option = Instantiate(optionPrefab, transform);
                Button button = option.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        HeroSelectionManager.instance.SetHero(h);
                        Session.SelectedHeroId = h.Id;
                        StartCoroutine(SelectHeroOnServer(h.Id));

                        if (selectedHero != null)
                        {
                            prevHero = selectedHero;
                        }
                        selectedHero = option.transform;

                        if (startButton != null)
                        {
                            startButton.interactable = true;
                        }
                    });
                }
                TextMeshProUGUI[] texts = option.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0 && texts[0] != null) texts[0].text = h.Name ?? "Unknown";

                if (texts.Length > 1 && texts[1] != null) texts[1].text = "Level: " + h.Level.ToString();
            }
        }
        else
        {
            createHeroPanel.SetActive(true);
            if (createHeroNameInput != null) createHeroNameInput.text = string.Empty;
        }
    }

    private void OnStartButtonClicked()
    {
        if (HeroSelectionManager.instance.currentHero == null) return;
        SceneManager.LoadScene(gameplaySceneName);
    }
    private void OnCreateHeroClicked()
    {
        if (createHeroPanel != null)
        {
            createHeroPanel.SetActive(true);
            if (createHeroNameInput != null) createHeroNameInput.text = string.Empty;
        }
        else
        {
            string name = createHeroNameInput != null ? createHeroNameInput.text : "";
            if (string.IsNullOrWhiteSpace(name)) name = "New Hero";
            StartCoroutine(CreateAndRefresh(name));
        }
    }

    private void OnConfirmCreateHero()
    {
        string name = createHeroNameInput != null ? createHeroNameInput.text : "";
        if (string.IsNullOrWhiteSpace(name)) name = "New Hero";
        if (createHeroPanel != null) createHeroPanel.SetActive(false);
        StartCoroutine(CreateAndRefresh(name));
    }

    private void OnCancelCreateHero()
    {
        if (createHeroPanel != null) createHeroPanel.SetActive(false);
    }

    private IEnumerator CreateAndRefresh(string heroName)
    {
        yield return StartCoroutine(HeroSelectionManager.instance.CreateHeroCoroutine(heroName));
        RebuildOptions();
    }

    private void RebuildOptions()
    {
        try
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HeroSelectionUI] RebuildOptions failed: {ex.Message}");
        }

        foreach (Session.HeroSummary h in HeroSelectionManager.instance.heroes)
        {
            if (h == null) continue;
            GameObject option = Instantiate(optionPrefab, transform);
            var button = option.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    HeroSelectionManager.instance.SetHero(h);
                    Session.SelectedHeroId = h.Id;
                    StartCoroutine(SelectHeroOnServer(h.Id));
                    if (selectedHero != null) { prevHero = selectedHero; }
                    selectedHero = option.transform;
                    if (startButton != null) startButton.interactable = true;
                });
            }
            var texts = option.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0 && texts[0] != null) texts[0].text = h.Name ?? "Unknown";
            if (texts.Length > 1 && texts[1] != null) texts[1].text = "Level: " + h.Level.ToString();
        }
    }
    

    private void Update()
    {
        if (selectedHero != null)
        {
            selectedHero.localScale = Vector3.Lerp(
                selectedHero.localScale,
                new Vector3(1.2f, 1.2f, 1.2f),
                Time.deltaTime * 10f
            );
        }
        if (prevHero != null)
        {
            prevHero.localScale = Vector3.Lerp(
                prevHero.localScale,
                new Vector3(1.0f, 1.0f, 1.0f),
                Time.deltaTime * 10f
            );
        }
    }
    private IEnumerator SelectHeroOnServer(int heroId)
    {   
        yield return StartCoroutine(CheckSessionStatus());
        
        string url = $"{Session.ServerBaseUrl}/Hero/select/{heroId}";

        using (var uwr = new UnityWebRequest(url, "POST"))
        {
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Authorization", $"Bearer {Session.JwtToken}");
            uwr.SetRequestHeader("Accept", "application/json");

            yield return uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.Success)
            {
               
            }
            else
            {
                if (uwr.responseCode == 401)
                {
                    Session.JwtToken = null;
                    UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
                }
            }
        }
    }

    private IEnumerator CheckSessionStatus()
    {
        string url = $"{Session.ServerBaseUrl}/Authentication/session-status";
        
        using (var uwr = new UnityWebRequest(url, "GET"))
        {
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Authorization", $"Bearer {Session.JwtToken}");
            uwr.SetRequestHeader("Accept", "application/json");
            
            yield return uwr.SendWebRequest();
            
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<SessionStatusResponse>(uwr.downloadHandler.text);
                    
                    if (response != null && !response.IsOnline)
                    {
                        Session.JwtToken = null;
                        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HeroSelectionUI] Failed to parse session status: {ex.Message}");
                }
            }
        }
    }
}

