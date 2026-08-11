using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title 화면에서 동전이 제자리 회전하는 루프 연출을 담당한다.
/// Spinning.png에서 슬라이스한 5프레임 Sprite를 Inspector 배열 순서대로 반복 표시한다.
/// 프레임 배열과 재생 속도는 Title.unity의 Img_CoinSpin Inspector에서 조정한다.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class TitleCoinSpinner : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [Tooltip("초당 프레임 수. 값을 올리면 동전이 더 빠르게 회전한다.")]
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;

    private Image image;
    private float elapsed;
    private int frameIndex;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        // 재활성화될 때마다 첫 프레임부터 다시 회전을 시작한다.
        elapsed = 0f;
        frameIndex = 0;
        ApplyCurrentFrame();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        // Title 연출은 timeScale과 무관하게 항상 같은 속도로 돌도록 unscaled 시간을 쓴다.
        elapsed += Time.unscaledDeltaTime;
        float interval = 1f / framesPerSecond;
        while (elapsed >= interval)
        {
            elapsed -= interval;
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        ApplyCurrentFrame();
    }

    private void ApplyCurrentFrame()
    {
        if (frames == null || frames.Length == 0 || image == null)
        {
            return;
        }

        Sprite frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        if (frame != null && image.sprite != frame)
        {
            image.sprite = frame;
        }
    }
}
