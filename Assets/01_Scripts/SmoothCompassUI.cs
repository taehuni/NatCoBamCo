using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SmoothCompassUI : MonoBehaviour
{
    [Header("ÂüÁ¶")]
    public Transform playerTransform;
    public RectTransform compassContent;

    [Header("³· / ¹ã UI")]
    public TMP_Text dayNightText;

    [Header("³·¹ã »óÅÂ")]
    public bool isNight = false;

    [Header("³ªÄ§¹Ý ¼³Á¤")]
    public float pixelsPerDegree = 4f;
    public float smoothTime = 0.08f;
    public TMP_FontAsset fontAsset;

    private readonly string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private float targetYaw;
    private float smoothYaw;
    private float yawVelocity;
    private float lastRawYaw;
    private bool initialized;

    void Start()
    {
        CreateCompassLabels();
    }

    void Update()
    {
        UpdateCompass();
        UpdateDayNight();
    }

    void CreateCompassLabels()
    {
        if (compassContent == null) return;

        for (int i = compassContent.childCount - 1; i >= 0; i--)
        {
            Destroy(compassContent.GetChild(i).gameObject);
        }

        for (int cycle = -1; cycle <= 2; cycle++)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                int degree = cycle * 360 + i * 45;
                CreateDirectionText(directions[i], degree);
                CreateTick(degree, true);
            }

            for (int d = cycle * 360; d < cycle * 360 + 360; d += 15)
            {
                if (d % 45 != 0)
                {
                    CreateTick(d, false);
                }
            }
        }
    }

    void CreateDirectionText(string dir, int degree)
    {
        GameObject obj = new GameObject("Dir_" + dir + "_" + degree);
        obj.transform.SetParent(compassContent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(60, 25);
        rect.anchoredPosition = new Vector2(degree * pixelsPerDegree, 8);

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = dir;
        text.fontSize = 18;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        if (fontAsset != null)
        {
            text.font = fontAsset;
        }
    }

    void CreateTick(int degree, bool bigTick)
    {
        GameObject obj = new GameObject(bigTick ? "BigTick_" + degree : "SmallTick_" + degree);
        obj.transform.SetParent(compassContent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(2, bigTick ? 12 : 7);
        rect.anchoredPosition = new Vector2(degree * pixelsPerDegree, -8);

        Image image = obj.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, bigTick ? 1f : 0.6f);
    }

    void UpdateCompass()
    {
        if (playerTransform == null || compassContent == null) return;

        float rawYaw = playerTransform.eulerAngles.y;

        if (!initialized)
        {
            initialized = true;
            lastRawYaw = rawYaw;
            targetYaw = rawYaw;
            smoothYaw = rawYaw;
        }

        float delta = Mathf.DeltaAngle(lastRawYaw, rawYaw);
        targetYaw += delta;
        lastRawYaw = rawYaw;

        smoothYaw = Mathf.SmoothDamp(smoothYaw, targetYaw, ref yawVelocity, smoothTime);

        if (smoothYaw > 360f)
        {
            smoothYaw -= 360f;
            targetYaw -= 360f;
        }
        else if (smoothYaw < 0f)
        {
            smoothYaw += 360f;
            targetYaw += 360f;
        }

        Vector2 pos = compassContent.anchoredPosition;
        pos.x = -smoothYaw * pixelsPerDegree;
        compassContent.anchoredPosition = pos;
    }

    void UpdateDayNight()
    {
        if (dayNightText == null) return;

        dayNightText.text = isNight ? "¹ã" : "³·";
    }
}