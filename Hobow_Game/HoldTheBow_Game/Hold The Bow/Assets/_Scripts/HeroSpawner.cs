using UnityEngine;
using System.Collections;

public class HeroSpawner : MonoBehaviour
{
    [Header("Hero Prefabs")]
    public GameObject defaultHeroPrefab; 
    
    private void Start()
    {
        StartCoroutine(SpawnHeroWhenReady());
    }
    private Vector3 GetRandomSpawnPosition()
    {
        float x = Random.Range(-3f, 3f);
        float y = Random.Range(-3f, 3f);
        
        Vector3 spawnPos = new Vector3(x, y, 0f);
        
        GameObject[] existingPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in existingPlayers)
        {
            if (Vector3.Distance(spawnPos, player.transform.position) < 1.5f)
            {
                x = Random.Range(-3f, 3f);
                y = Random.Range(-3f, 3f);
                spawnPos = new Vector3(x, y, 0f);
                break;
            }
        }
        
        return spawnPos;
    }
    
    private IEnumerator SpawnHeroWhenReady()
    {
        while (HeroSelectionManager.instance == null || HeroSelectionManager.instance.currentHero == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        HeroSelectionManager.instance.ForceAssignPrefabs();

        var currentHero = HeroSelectionManager.instance.currentHero;

        GameObject heroPrefab = currentHero.Prefab;
        if (heroPrefab == null)
        {
            heroPrefab = defaultHeroPrefab;

            if (heroPrefab == null && HeroSelectionManager.instance != null)
            {
                heroPrefab = HeroSelectionManager.instance.defaultHeroPrefab;
            }

            if (heroPrefab == null)
            {
                yield break;
            }

        }

        Vector3 spawnPosition = GetRandomSpawnPosition();
        GameObject spawnedHero = Instantiate(heroPrefab, spawnPosition, Quaternion.identity);
        spawnedHero.name = currentHero.Name;


        CamMovement camMovement = FindObjectOfType<CamMovement>();
        if (camMovement != null)
        {
            camMovement.target = spawnedHero.transform;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 viewportPoint = mainCamera.WorldToViewportPoint(spawnedHero.transform.position);

            bool inView = viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
                         viewportPoint.y >= 0 && viewportPoint.y <= 1 &&
                         viewportPoint.z > 0;
        }
    }
}