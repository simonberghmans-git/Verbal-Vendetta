using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("UI References")]
    public GameObject tooltipObject;
    public TMP_Text tooltipText;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        HideTooltip();
    }

    private void Update()
    {
        if (tooltipObject != null && tooltipObject.activeSelf)
        {
            Vector2 mousePos = Input.mousePosition;
            // Offset slightly from cursor
            tooltipObject.transform.position = mousePos + new Vector2(15f, -15f);
        }
    }

    public static void Show(string text)
    {
        if (Instance != null && Instance.tooltipText != null && Instance.tooltipObject != null)
        {
            Instance.tooltipText.text = text;
            Instance.tooltipObject.SetActive(true);
        }
    }

    public static void Hide()
    {
        if (Instance != null && Instance.tooltipObject != null)
        {
            Instance.tooltipObject.SetActive(false);
        }
    }

    private void HideTooltip()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }
}
