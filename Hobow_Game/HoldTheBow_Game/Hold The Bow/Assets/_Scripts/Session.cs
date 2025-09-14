using System.Collections.Generic;
using UnityEngine;

public static class Session
{
    public static string ServerBaseUrl = "";
    public static string JwtToken = "";
    public static int SelectedHeroId = -1;
    public static List<HeroSummary> Heroes = new List<HeroSummary>();

    [System.Serializable]
    public class HeroSummary
    {
        public int Id;
        public GameObject Prefab;
        public string Name;
        public int Level;
    }
}

