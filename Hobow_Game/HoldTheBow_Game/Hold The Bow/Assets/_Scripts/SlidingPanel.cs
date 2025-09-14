using UnityEngine;
using UnityEngine.UI;
using TMPro;  
using System.Collections;

public class SlidingPanelTMP : MonoBehaviour
{
    public RectTransform panel;    
    public Button toggleButton;    

    [SerializeField] private float slideSpeed = 500f; 
    private bool isOpen = false;
    private Vector2 closedPos;
    private Vector2 openPos;
    private TMP_Text arrowText;   

    void Start()
    {
        openPos = panel.anchoredPosition;
        closedPos = new Vector2(openPos.x + panel.rect.width, openPos.y);

        panel.anchoredPosition = closedPos;

        arrowText = toggleButton.GetComponentInChildren<TMP_Text>();
        arrowText.text = "<";  

        toggleButton.onClick.AddListener(TogglePanel);
    }

    public void TogglePanel()
    {
        isOpen = !isOpen;

        StopAllCoroutines();
        StartCoroutine(Slide(isOpen ? openPos : closedPos));

        arrowText.text = isOpen ? ">" : "<";
    }

    private IEnumerator Slide(Vector2 target)
    {
        while (Vector2.Distance(panel.anchoredPosition, target) > 0.1f)
        {
            panel.anchoredPosition = Vector2.MoveTowards(
                panel.anchoredPosition,
                target,
                slideSpeed * Time.deltaTime
            );
            yield return null;
        }
        panel.anchoredPosition = target;
    }
}
