using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Image에 Sprite 프레임 배열을 일정 속도로 반복 표시한다.
/// 튜토리얼 대사창의 엘리자 idle 시트처럼 Animator 없이 도는 단순 루프 연출에 쓴다.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class ImageFrameLoop : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [Tooltip("초당 프레임 수. 값을 올리면 더 빠르게 움직인다.")]
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

        // 히트스톱이나 연출 스킵으로 timeScale이 바뀌어도 같은 속도로 돌도록 unscaled 시간을 쓴다.
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
