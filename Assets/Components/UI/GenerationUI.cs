using UnityEngine;
using UnityEngine.UI;

// Displays "Generation: X" in the top-right corner of the screen
public class GenerationUI : MonoBehaviour
{
    private Text generationText;
    private Text bestNestText;

    void Start()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("GenerationCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create Generation text element
        GameObject textObj = new GameObject("GenerationText");
        textObj.transform.SetParent(canvasObj.transform);

        generationText = textObj.AddComponent<Text>();
        generationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        generationText.fontSize = 24;
        generationText.color = Color.white;
        generationText.alignment = TextAnchor.UpperRight;
        generationText.text = "Generation: 1";

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-10, -10);
        rect.sizeDelta = new Vector2(300, 40);

        // Create Best Nest Count text element (below generation)
        GameObject bestNestObj = new GameObject("BestNestText");
        bestNestObj.transform.SetParent(canvasObj.transform);

        bestNestText = bestNestObj.AddComponent<Text>();
        bestNestText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bestNestText.fontSize = 24;
        bestNestText.color = Color.white;
        bestNestText.alignment = TextAnchor.UpperRight;
        bestNestText.text = "Best Nest Count: 0";

        Outline bestNestOutline = bestNestObj.AddComponent<Outline>();
        bestNestOutline.effectColor = Color.black;
        bestNestOutline.effectDistance = new Vector2(1, -1);

        RectTransform bestNestRect = bestNestObj.GetComponent<RectTransform>();
        bestNestRect.anchorMin = new Vector2(1, 1);
        bestNestRect.anchorMax = new Vector2(1, 1);
        bestNestRect.pivot = new Vector2(1, 1);
        bestNestRect.anchoredPosition = new Vector2(-10, -50);
        bestNestRect.sizeDelta = new Vector2(300, 40);
    }

    void Update()
    {
        if (EvolutionManager.Instance != null)
        {
            generationText.text = "Generation: " + EvolutionManager.Instance.Generation;
            bestNestText.text = "Best Nest Count: " + EvolutionManager.Instance.BestNestCount;
        }
    }
}

