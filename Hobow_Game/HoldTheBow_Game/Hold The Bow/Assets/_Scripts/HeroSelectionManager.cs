
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

using System.Text;
using System;
using UnityEngine.Networking;
using Newtonsoft.Json;
using SharedLibrary.Requests;

public class HeroSelectionManager : MonoBehaviour
{
    public static HeroSelectionManager instance;

    public Session.HeroSummary[] heroes = Array.Empty<Session.HeroSummary>();
    public Session.HeroSummary currentHero;
    public GameObject defaultHeroPrefab;
    public bool IsLoaded { get; set; } = false;

    [SerializeField] private bool allowSelfSignedCertificates = true;
    [SerializeField] private int requestTimeoutSeconds = 20;

    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }

    private void Start()
    {
        LoadSessionFromPlayerPrefs();
        
        if (Session.Heroes != null && Session.Heroes.Count > 0)
        {
            heroes = Session.Heroes.ToArray();
            AssignDefaultPrefabs();
            SetDefaultHero();
            IsLoaded = true;
        }
        else if (!string.IsNullOrEmpty(Session.JwtToken))
        {
            StartCoroutine(LoadHeroesFromServer(Session.JwtToken, setAsCurrent: true));
        }
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
            }
        }
    }
    
    public void ForceReloadHeroes()
    {
        heroes = Array.Empty<Session.HeroSummary>();
        Session.Heroes = new List<Session.HeroSummary>();
        currentHero = null;
        
        if (!string.IsNullOrEmpty(Session.JwtToken))
        {
            StartCoroutine(LoadHeroesFromServer(Session.JwtToken, setAsCurrent: true));
        }
    }

    private void AssignDefaultPrefabs()
    {
        if (defaultHeroPrefab == null)
        {
            return;
        }
        
        foreach (var hero in heroes)
        {
            if (hero.Prefab == null)
            {
                hero.Prefab = defaultHeroPrefab;
            }
        }
    }

    private void SetDefaultHero()
    {
        if (heroes.Length > 0 && currentHero == null)
        {
            currentHero = heroes[0];
            Session.SelectedHeroId = currentHero.Id;
        }
    }

    public void SetHero(Session.HeroSummary hero)
    {
        currentHero = hero;
        Session.SelectedHeroId = hero.Id;
        
        if (currentHero.Prefab == null && defaultHeroPrefab != null)
        {
            currentHero.Prefab = defaultHeroPrefab;
        }
    }

    public void CreateNewHero()
    {
        StartCoroutine(CreateHeroCoroutine("New Hero"));
    }
    
    public void ForceAssignPrefabs()
    {
        AssignDefaultPrefabs();
        if (currentHero != null && currentHero.Prefab == null && defaultHeroPrefab != null)
        {
            currentHero.Prefab = defaultHeroPrefab;
        }
    }

    public IEnumerator CreateHeroCoroutine(string heroName)
    {
        string token = Session.JwtToken;
        if (string.IsNullOrEmpty(token))
        {
            yield break;
        }

        string url = $"{Session.ServerBaseUrl}/Hero";
        var request = new CreateHeroRequest { Name = string.IsNullOrWhiteSpace(heroName) ? "New Hero" : heroName };

        string json = JsonConvert.SerializeObject(request);

        using (var uwr = new UnityWebRequest(url, "POST"))
        {
            uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return uwr.SendWebRequest();
        }

        yield return LoadHeroesFromServer(Session.JwtToken, setAsCurrent: true);
    }



    public IEnumerator LoadHeroesFromServer(string token, bool setAsCurrent = false)
    {
        if (string.IsNullOrEmpty(token))
        {
            IsLoaded = true;
            yield break;
        }

        string url = $"{Session.ServerBaseUrl}/Hero/my";

        using (var uwr = new UnityWebRequest(url, "GET"))
        {
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Authorization", $"Bearer {token}");
            uwr.SetRequestHeader("Accept", "application/json");
            uwr.timeout = Mathf.Max(5, requestTimeoutSeconds);

            if (allowSelfSignedCertificates)
            {
                uwr.certificateHandler = new BypassCertificateHandler();
                uwr.disposeCertificateHandlerOnDispose = true;
            }

            yield return uwr.SendWebRequest();

            bool isError = uwr.result != UnityWebRequest.Result.Success;

            if (isError)
            {
                yield break;
            }

            try
            {
                string responseText = uwr.downloadHandler.text;

                var loadedHeroes = JsonConvert.DeserializeObject<List<Session.HeroSummary>>(responseText);

                if (loadedHeroes != null)
                {
                    heroes = loadedHeroes.ToArray();
                    Session.Heroes = loadedHeroes;
                    AssignDefaultPrefabs();
                    if (setAsCurrent)
                        SetDefaultHero();
                }
                else
                {
                    heroes = Array.Empty<Session.HeroSummary>();
                    Session.Heroes = new List<Session.HeroSummary>();
                }
            }
            catch (Exception ex)
            {
                heroes = Array.Empty<Session.HeroSummary>();
                Session.Heroes = new List<Session.HeroSummary>();
                Debug.LogWarning($"[HeroSelectionManager] Parse heroes failed: {ex.Message}");
            }
        }
        IsLoaded = true;
    }
}