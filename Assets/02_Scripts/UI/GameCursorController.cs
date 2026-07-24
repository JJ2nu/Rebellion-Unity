using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameCursorController : MonoBehaviour
{
    private static GameCursorController instance;
    private static bool isQuitting;

    [SerializeField] private Texture2D defaultCursor;
    [SerializeField] private Texture2D pressedCursor;
    [SerializeField] private Vector2 hotSpot = new Vector2(48f, 14f);
    [SerializeField] private Vector2 cursorSize = new Vector2(256f, 256f);
    [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.25f;
    [SerializeField] private int sortingOrder = 32767;
    [SerializeField] private bool showHardwareCursorForAlignment = true;

    private Canvas cursorCanvas;
    private RawImage cursorImage;
    private RectTransform cursorRect;
    private Texture2D currentCursor;
    private Texture2D currentSourceCursor;
    private Texture2D preparedDefaultCursor;
    private Texture2D preparedPressedCursor;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateSoftwareCursor();
        ApplyCursorTexture(defaultCursor);
        UpdateCursorVisibility(true);
    }

    private void Update()
    {
        UpdateHardwareCursorVisibility();
        UpdateCursorTexture();
        UpdateCursorPosition();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        UpdateCursorVisibility(hasFocus);
    }

    private void OnDisable()
    {
        if (instance == this && !isQuitting)
        {
            UpdateCursorVisibility(false);
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        DestroyPreparedCursorTextures();

        if (isQuitting)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void CreateSoftwareCursor()
    {
        if (cursorCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Software Cursor Canvas");
        canvasObject.transform.SetParent(transform, false);

        cursorCanvas = canvasObject.AddComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasObject.AddComponent<GraphicRaycaster>().enabled = false;

        GameObject imageObject = new GameObject("Software Cursor");
        imageObject.transform.SetParent(canvasObject.transform, false);

        cursorImage = imageObject.AddComponent<RawImage>();
        cursorImage.raycastTarget = false;

        cursorRect = cursorImage.rectTransform;
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.sizeDelta = cursorSize;
        ApplyPivot();
    }

    private void UpdateCursorTexture()
    {
        Texture2D targetCursor = IsPrimaryButtonPressed() && pressedCursor != null
            ? pressedCursor
            : defaultCursor;

        ApplyCursorTexture(targetCursor);
    }

    private void ApplyCursorTexture(Texture2D texture)
    {
        if (cursorImage == null || texture == null || currentSourceCursor == texture)
        {
            return;
        }

        currentSourceCursor = texture;
        currentCursor = GetPreparedCursorTexture(texture);
        cursorImage.texture = currentCursor;
        cursorRect.sizeDelta = cursorSize;
        ApplyPivot();
    }

    private void UpdateCursorPosition()
    {
        if (cursorRect == null)
        {
            return;
        }

        cursorRect.position = GetPointerPosition();
    }

    private void ApplyPivot()
    {
        if (cursorRect == null || cursorSize.x <= 0f || cursorSize.y <= 0f)
        {
            return;
        }

        cursorRect.pivot = new Vector2(
            Mathf.Clamp01(hotSpot.x / cursorSize.x),
            Mathf.Clamp01(1f - hotSpot.y / cursorSize.y));
    }

    private void UpdateCursorVisibility(bool isVisible)
    {
        if (isVisible)
        {
            UpdateHardwareCursorVisibility();
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
        }

        if (cursorCanvas != null)
        {
            cursorCanvas.enabled = isVisible;
        }
    }

    private void UpdateHardwareCursorVisibility()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = showHardwareCursorForAlignment;
    }

    private static bool IsPrimaryButtonPressed()
    {
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private static Vector2 GetPointerPosition()
    {
        return Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;
    }

    private Texture2D GetPreparedCursorTexture(Texture2D source)
    {
        if (source == null)
        {
            return null;
        }

        if (source == defaultCursor)
        {
            preparedDefaultCursor ??= CreateAlphaCutoffTexture(source);
            return preparedDefaultCursor;
        }

        if (source == pressedCursor)
        {
            preparedPressedCursor ??= CreateAlphaCutoffTexture(source);
            return preparedPressedCursor;
        }

        return CreateAlphaCutoffTexture(source);
    }

    private Texture2D CreateAlphaCutoffTexture(Texture2D source)
    {
        try
        {
            Color32[] pixels = source.GetPixels32();
            byte cutoff = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaCutoff) * byte.MaxValue);

            for (int index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].a < cutoff)
                {
                    pixels[index] = new Color32(0, 0, 0, 0);
                }
            }

            Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name}_AlphaCutoff",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
        catch (UnityException exception)
        {
            Debug.LogWarning($"Failed to apply cursor alpha cutoff to {source.name}. Make sure the texture is readable. {exception.Message}", this);
            return source;
        }
    }

    private void DestroyPreparedCursorTextures()
    {
        if (preparedDefaultCursor != null && preparedDefaultCursor != defaultCursor)
        {
            Destroy(preparedDefaultCursor);
        }

        if (preparedPressedCursor != null && preparedPressedCursor != pressedCursor)
        {
            Destroy(preparedPressedCursor);
        }

        preparedDefaultCursor = null;
        preparedPressedCursor = null;
        currentCursor = null;
        currentSourceCursor = null;
    }
}
