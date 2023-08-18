using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShowNameOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI label;

    private void Awake() { if (label) label.color = Color.white; }
    public void OnPointerEnter(PointerEventData eventData)
    {
        label.text = name;
        label.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData) => label.gameObject.SetActive(false);
}
