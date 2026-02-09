using UnityEngine;
using UnityEngine.UI;
using Antymology.Terrain;

public class NestCountUI : MonoBehaviour
{
    private Text nestCountText;
    private Text antCountText;

    void Start()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("NestCountCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create Nest Count text element
        GameObject textObj = new GameObject("NestCountText");
        textObj.transform.SetParent(canvasObj.transform);

        nestCountText = textObj.AddComponent<Text>();
        nestCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nestCountText.fontSize = 24;
        nestCountText.color = Color.white;
        nestCountText.alignment = TextAnchor.UpperLeft;
        nestCountText.text = "Nest Blocks: 0";

        // Add outline for readability against any background
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        // Position in top-left corner
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(10, -10);
        rect.sizeDelta = new Vector2(300, 40);

        // Create Ants Left text element (below nest count)
        GameObject antCountObj = new GameObject("AntCountText");
        antCountObj.transform.SetParent(canvasObj.transform);

        antCountText = antCountObj.AddComponent<Text>();
        antCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        antCountText.fontSize = 24;
        antCountText.color = Color.white;
        antCountText.alignment = TextAnchor.UpperLeft;
        antCountText.text = "Ants Left: 0";

        Outline antOutline = antCountObj.AddComponent<Outline>();
        antOutline.effectColor = Color.black;
        antOutline.effectDistance = new Vector2(1, -1);

        RectTransform antRect = antCountObj.GetComponent<RectTransform>();
        antRect.anchorMin = new Vector2(0, 1);
        antRect.anchorMax = new Vector2(0, 1);
        antRect.pivot = new Vector2(0, 1);
        antRect.anchoredPosition = new Vector2(10, -50);
        antRect.sizeDelta = new Vector2(300, 40);
    }

    void Update()
    {
        if (WorldManager.Instance != null)
        {
            int count = WorldManager.Instance.CountNestBlocks();
            nestCountText.text = "Nest Blocks: " + count;
        }

        int antCount = FindObjectsByType<AntBase>(FindObjectsSortMode.None).Length;
        antCountText.text = "Ants Left: " + antCount;
    }
}

