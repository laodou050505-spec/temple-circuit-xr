using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TempleCircuit
{
    [RequireComponent(typeof(Button))]
    public sealed class TempleWorldButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string actionId;
        public TempleCircuitGame game;

        Button button;
        Image image;
        Color normalColor;

        void Awake()
        {
            button = GetComponent<Button>();
            image = GetComponent<Image>();
            if (image != null)
                normalColor = image.color;
            button.onClick.AddListener(Trigger);
        }

        public void Trigger()
        {
            if (game != null && button.interactable)
            {
                game.audioManager?.PlayUiClick();
                game.HandleAction(actionId);
            }
        }

        public void SetInteractable(bool value)
        {
            if (button == null)
                button = GetComponent<Button>();
            button.interactable = value;
        }

        public void SetHover(bool value)
        {
            if (image == null)
                return;
            image.color = value ? Color.Lerp(normalColor, Color.white, 0.28f) : normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHover(true);
        public void OnPointerExit(PointerEventData eventData) => SetHover(false);
    }
}
