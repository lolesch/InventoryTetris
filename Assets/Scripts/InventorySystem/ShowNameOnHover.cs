using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShowNameOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI label;
    public void OnPointerEnter(PointerEventData eventData)
    {
        label.text = this.name;
        label.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        label.gameObject.SetActive(false);
    }
}
