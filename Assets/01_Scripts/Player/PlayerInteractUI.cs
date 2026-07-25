using UnityEngine;
using UnityEngine.UI;

public class PlayerInteractUI : MonoBehaviour
{
    public GameObject interactButton;
    public Text interactText;
    public Slider interactSlider;

    void Start()
    {
        HideButton();
        HideSlider();
        interactSlider.value = 0f;
    }

    public void ShowButton(string text)
    {
        if (interactButton != null)
        {
            interactButton.SetActive(true);
        }

        if (interactText != null)
        {
            interactText.text = text;
        }
    }

    public void HideButton()
    {
        if (interactButton != null)
        {
            interactButton.SetActive(false);
        }
    }

    public void HideSlider()
    {
        if(interactSlider != null)
        {
            interactSlider.gameObject.SetActive(false);
        }
    }

    public void ShowSlider()
    {
        if(interactSlider != null)
        {
            interactSlider.gameObject.SetActive(true);
        }
    }
}