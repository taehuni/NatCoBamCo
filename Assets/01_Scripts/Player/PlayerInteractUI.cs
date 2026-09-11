using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInteractUI : MonoBehaviour
{
    public GameObject interactButton;
    public Text interactText;
    public Slider interactSlider;

    private readonly Dictionary<MonoBehaviour, string> prompts = new Dictionary<MonoBehaviour, string>();
    private MonoBehaviour progressOwner;
    private bool ownedPromptVisible;

    void LateUpdate()
    {
        if (prompts.Count > 0 || ownedPromptVisible)
        {
            var selected = InteractionSelection.GetDisplayTarget(GetComponentInParent<PlayerController>());
            if (selected != null && prompts.TryGetValue(selected, out var text))
            {
                ShowButton(text);
                ownedPromptVisible = true;
            }
            else
            {
                HideButton();
                ownedPromptVisible = false;
            }
        }
    }

    public void ShowButton(string text, MonoBehaviour owner) => prompts[owner] = text;
    public void HideButton(MonoBehaviour owner) => prompts.Remove(owner);

    public void ShowSlider(MonoBehaviour owner)
    {
        progressOwner = owner;
        ShowSlider();
    }

    public void SetProgress(float value, MonoBehaviour owner)
    {
        if (progressOwner == owner && interactSlider != null) interactSlider.value = value;
    }

    public void HideSlider(MonoBehaviour owner)
    {
        if (progressOwner != owner) return;
        progressOwner = null;
        if (interactSlider != null) interactSlider.value = 0f;
        HideSlider();
    }

    void OnDisable()
    {
        prompts.Clear();
        progressOwner = null;
        ownedPromptVisible = false;
        HideButton();
        HideSlider();
    }

    void Start()
    {
        HideButton();
        HideSlider();
        if (interactSlider != null)
        {
            interactSlider.value = 0f;
        }
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
