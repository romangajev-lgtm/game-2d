using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AIInterrogation
{
    public class EvidenceHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Action PointerEntered;
        public Action PointerExited;
        public Action PointerPressed;
        public Action PointerReleased;
        public Action PointerClicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExited?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PointerReleased?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClicked?.Invoke();
        }
    }
}
