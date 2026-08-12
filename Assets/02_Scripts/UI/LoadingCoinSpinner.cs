using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 화면의 동전 연출을 담당한다.
/// 로딩 중에는 Spinning 프레임을 일정 속도로 반복하고, BeginStop 호출 뒤에는
/// stopping 프레임 시퀀스를 점점 느려지는 간격으로 한 번 재생해 마지막(누운) 프레임에서 멈춘다.
/// 프레임 배열과 속도는 LoadingScreenView Prefab의 Img_Coin Inspector에서 조정한다.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class LoadingCoinSpinner : MonoBehaviour
{
    [Header("Spin Loop")]
    [SerializeField] private Sprite[] spinFrames;
    [Tooltip("회전 루프의 초당 프레임 수. 값을 올리면 동전이 더 빠르게 회전한다.")]
    [SerializeField, Min(1f)] private float spinFramesPerSecond = 12f;

    [Header("Stop Sequence")]
    [SerializeField] private Sprite[] stopFrames;
    [Tooltip("정지 시퀀스 시작 시점의 초당 프레임 수.")]
    [SerializeField, Min(1f)] private float stopStartFramesPerSecond = 18f;
    [Tooltip("정지 시퀀스 마지막 프레임 직전의 초당 프레임 수. 시작값보다 낮게 두면 점점 느려진다.")]
    [SerializeField, Min(0.5f)] private float stopEndFramesPerSecond = 5f;

    private Image image;
    private float elapsed;
    private int frameIndex;
    private bool stopRequested;
    private bool stopped;

    /// <summary>
    /// 정지 시퀀스가 마지막 프레임까지 끝났는지 여부다.
    /// SceneTransitionOverlay가 로딩 화면을 걷어도 되는 시점을 판단할 때 읽는다.
    /// 정지 프레임이 비어 있으면 연출을 기다릴 수 없으므로 즉시 끝난 것으로 본다.
    /// </summary>
    public bool IsStopFinished => stopped || stopFrames == null || stopFrames.Length == 0;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        // 로딩 화면이 다시 표시될 때마다 이전 정지 상태를 버리고 회전 루프부터 시작한다.
        RestartSpin();
    }

    /// <summary>
    /// 새 로딩 표시가 시작될 때 회전 루프 상태로 되돌린다.
    /// </summary>
    public void RestartSpin()
    {
        elapsed = 0f;
        frameIndex = 0;
        stopRequested = false;
        stopped = false;
        ApplyFrame(spinFrames, 0);
    }

    /// <summary>
    /// 로딩 완료 시점에 호출한다. 회전 루프를 감속 정지 시퀀스로 전환하며 중복 호출은 무시한다.
    /// </summary>
    public void BeginStop()
    {
        if (stopRequested)
        {
            return;
        }

        stopRequested = true;
        elapsed = 0f;
        frameIndex = 0;

        if (stopFrames == null || stopFrames.Length == 0)
        {
            // 정지 프레임이 없으면 연출 없이 즉시 완료로 처리해 로딩 종료 흐름을 막지 않는다.
            stopped = true;
            return;
        }

        ApplyFrame(stopFrames, 0);
    }

    private void Update()
    {
        if (stopped)
        {
            return;
        }

        // 로딩 화면은 timeScale 영향 없이 항상 같은 속도로 움직여야 하므로 unscaled 시간을 쓴다.
        elapsed += Time.unscaledDeltaTime;
        if (stopRequested)
        {
            UpdateStopSequence();
        }
        else
        {
            UpdateSpinLoop();
        }
    }

    private void UpdateSpinLoop()
    {
        if (spinFrames == null || spinFrames.Length == 0)
        {
            return;
        }

        float interval = 1f / spinFramesPerSecond;
        while (elapsed >= interval)
        {
            elapsed -= interval;
            frameIndex = (frameIndex + 1) % spinFrames.Length;
        }

        ApplyFrame(spinFrames, frameIndex);
    }

    private void UpdateStopSequence()
    {
        // 프레임이 진행될수록 간격을 늘려 회전이 점점 느려지고, 마지막(누운) 프레임에 도달하면 멈춘 상태를 유지한다.
        float interval = CurrentStopInterval();
        while (!stopped && elapsed >= interval)
        {
            elapsed -= interval;
            frameIndex++;
            if (frameIndex >= stopFrames.Length - 1)
            {
                frameIndex = stopFrames.Length - 1;
                stopped = true;
            }

            interval = CurrentStopInterval();
        }

        ApplyFrame(stopFrames, frameIndex);
    }

    private float CurrentStopInterval()
    {
        // 시퀀스 진행도(0~1)에 따라 시작 fps에서 끝 fps로 보간한 값을 현재 프레임 간격으로 쓴다.
        float lastIndex = Mathf.Max(1, stopFrames.Length - 1);
        float progress = Mathf.Clamp01(frameIndex / lastIndex);
        float framesPerSecond = Mathf.Lerp(stopStartFramesPerSecond, stopEndFramesPerSecond, progress);
        return 1f / Mathf.Max(0.5f, framesPerSecond);
    }

    private void ApplyFrame(Sprite[] frames, int index)
    {
        if (frames == null || frames.Length == 0 || image == null)
        {
            return;
        }

        Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        if (frame != null && image.sprite != frame)
        {
            image.sprite = frame;
        }
    }
}
