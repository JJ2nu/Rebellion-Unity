using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼이 지금 조작할 곳(슬롯, 보드 칸, 버튼)을 화면에서 강조하는 오버레이.
/// 대상 주변만 뚫린 반투명 어둠과 맥동하는 모서리 괄호로 표시한다.
/// 시작의 총성 스코프와 시각 언어가 겹치지 않도록 원형 조준경 대신 사각 괄호를 쓴다.
/// 클릭은 막지 않는다. 입력 제한은 StageTutorialGate가 담당한다.
/// </summary>
public sealed class TutorialSpotlightOverlay : MonoBehaviour
{
    private enum TargetKind
    {
        None,
        Rect,
        WorldCell,
    }

    private const float BracketLength = 26f;
    private const float BracketThickness = 5f;
    private const float PulseCycleSeconds = 1.2f;

    private static readonly Color BracketColor = new(0.87f, 0.62f, 0.48f, 1f);

    private readonly Image[] dims = new Image[4];
    private readonly Image[] brackets = new Image[8];
    private RectTransform root;
    private RectTransform frame;
    private Canvas canvas;

    private TargetKind targetKind;
    private RectTransform targetRect;
    private Vector3 targetWorldCenter;
    private float targetWorldHalfSize;
    private float padding;
    private float dimAlpha;

    public static TutorialSpotlightOverlay Create(RectTransform parent)
    {
        GameObject overlayObject = new("TutorialSpotlight", typeof(RectTransform));
        RectTransform overlayRect = (RectTransform)overlayObject.transform;
        overlayRect.SetParent(parent, false);
        Stretch(overlayRect);

        TutorialSpotlightOverlay overlay = overlayObject.AddComponent<TutorialSpotlightOverlay>();
        overlay.Build(overlayRect);
        return overlay;
    }

    public void FocusRect(RectTransform target, float targetPadding, float alpha)
    {
        targetKind = target != null ? TargetKind.Rect : TargetKind.None;
        targetRect = target;
        padding = targetPadding;
        dimAlpha = alpha;
    }

    public void FocusWorldCell(Vector3 cellCenter, float cellSize, float targetPadding, float alpha)
    {
        targetKind = TargetKind.WorldCell;
        targetWorldCenter = cellCenter;
        targetWorldHalfSize = cellSize * 0.5f;
        padding = targetPadding;
        dimAlpha = alpha;
    }

    public void Clear()
    {
        targetKind = TargetKind.None;
        targetRect = null;
    }

    private void Build(RectTransform overlayRect)
    {
        root = overlayRect;
        canvas = GetComponentInParent<Canvas>();

        for (int index = 0; index < dims.Length; index++)
        {
            dims[index] = CreateImage($"Dim_{index}", root, Color.black);
        }

        GameObject frameObject = new("Frame", typeof(RectTransform));
        frame = (RectTransform)frameObject.transform;
        frame.SetParent(root, false);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.pivot = new Vector2(0.5f, 0.5f);

        // 모서리마다 가로·세로 막대 하나씩 L자로 붙인다.
        Vector2[] corners = { new(0f, 0f), new(0f, 1f), new(1f, 0f), new(1f, 1f) };
        for (int index = 0; index < corners.Length; index++)
        {
            Vector2 corner = corners[index];
            Image horizontalImage = CreateImage($"Bracket_{index}_H", frame, BracketColor);
            Image verticalImage = CreateImage($"Bracket_{index}_V", frame, BracketColor);
            RectTransform horizontal = horizontalImage.rectTransform;
            RectTransform vertical = verticalImage.rectTransform;
            horizontal.anchorMin = horizontal.anchorMax = corner;
            vertical.anchorMin = vertical.anchorMax = corner;
            horizontal.pivot = corner;
            vertical.pivot = corner;
            horizontal.sizeDelta = new Vector2(BracketLength, BracketThickness);
            vertical.sizeDelta = new Vector2(BracketThickness, BracketLength);
            brackets[index * 2] = horizontalImage;
            brackets[index * 2 + 1] = verticalImage;
        }

        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (targetKind == TargetKind.None || !TryGetTargetScreenRect(out Rect screenRect))
        {
            SetVisible(false);
            return;
        }

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenRect.min, uiCamera, out Vector2 localMin) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenRect.max, uiCamera, out Vector2 localMax))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // 맥동은 괄호가 대상 바깥으로 살짝 벌어졌다 돌아오는 움직임으로 표현한다.
        float wave = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / PulseCycleSeconds) + 1f) * 0.5f;
        float pulse = padding + wave * 8f;
        Rect hole = Rect.MinMaxRect(localMin.x - pulse, localMin.y - pulse, localMax.x + pulse, localMax.y + pulse);
        Rect bounds = root.rect;

        // 위·아래·왼쪽·오른쪽 네 장으로 구멍 바깥을 덮는다.
        SetDim(dims[0], Rect.MinMaxRect(bounds.xMin, hole.yMax, bounds.xMax, bounds.yMax));
        SetDim(dims[1], Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, hole.yMin));
        SetDim(dims[2], Rect.MinMaxRect(bounds.xMin, hole.yMin, hole.xMin, hole.yMax));
        SetDim(dims[3], Rect.MinMaxRect(hole.xMax, hole.yMin, bounds.xMax, hole.yMax));

        frame.anchoredPosition = hole.center - bounds.center;
        frame.sizeDelta = hole.size;

        Color bracketColor = BracketColor;
        bracketColor.a = Mathf.Lerp(0.65f, 1f, wave);
        for (int index = 0; index < brackets.Length; index++)
        {
            brackets[index].color = bracketColor;
        }
    }

    private bool TryGetTargetScreenRect(out Rect screenRect)
    {
        screenRect = default;

        if (targetKind == TargetKind.Rect)
        {
            if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
            {
                return false;
            }

            Canvas targetCanvas = targetRect.GetComponentInParent<Canvas>();
            Camera camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera
                : null;
            Vector3[] corners = new Vector3[4];
            targetRect.GetWorldCorners(corners);
            screenRect = ScreenBounds(
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[1]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[2]),
                RectTransformUtility.WorldToScreenPoint(camera, corners[3]));
            return true;
        }

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return false;
        }

        // 카메라가 기울어져 있어 칸 네 모서리를 투영한 외곽 사각형을 구멍으로 쓴다.
        float half = targetWorldHalfSize;
        Vector3 center = targetWorldCenter;
        Vector3 a = worldCamera.WorldToScreenPoint(center + new Vector3(-half, 0f, -half));
        Vector3 b = worldCamera.WorldToScreenPoint(center + new Vector3(-half, 0f, half));
        Vector3 c = worldCamera.WorldToScreenPoint(center + new Vector3(half, 0f, -half));
        Vector3 d = worldCamera.WorldToScreenPoint(center + new Vector3(half, 0f, half));
        if (a.z < 0f || b.z < 0f || c.z < 0f || d.z < 0f)
        {
            return false;
        }

        screenRect = ScreenBounds(a, b, c, d);
        return true;
    }

    private static Rect ScreenBounds(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return Rect.MinMaxRect(
            Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)),
            Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)),
            Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)),
            Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)));
    }

    private void SetDim(Image dim, Rect localRect)
    {
        RectTransform rect = dim.rectTransform;
        Rect bounds = root.rect;
        rect.anchoredPosition = localRect.center - bounds.center;
        rect.sizeDelta = new Vector2(Mathf.Max(0f, localRect.width), Mathf.Max(0f, localRect.height));
        Color color = dim.color;
        color.a = dimAlpha;
        dim.color = color;
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
        {
            return;
        }

        for (int index = 0; index < dims.Length; index++)
        {
            if (dims[index] != null && dims[index].enabled != visible)
            {
                dims[index].enabled = visible;
            }
        }

        if (frame != null && frame.gameObject.activeSelf != visible)
        {
            frame.gameObject.SetActive(visible);
        }
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
