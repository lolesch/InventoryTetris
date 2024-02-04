using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShowAltTextOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] protected TextMeshProUGUI label;
    [SerializeField] protected string altText;
    private string originalText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        originalText = label.text;
        label.text = altText;
    }

    public void OnPointerExit(PointerEventData eventData) => label.text = originalText;
}
