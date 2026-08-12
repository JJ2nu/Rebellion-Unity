using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title 메뉴 호버 시 버튼 뒤에 표시되는 빨간 스플래시 연출을 담당한다.
/// 호버된 버튼의 세로 위치로 이동해 Select.png 프레임 애니메이션을 한 번 재생하고 마지막 프레임을 유지한다.
/// 가로 위치는 씬에 배치한 값을 그대로 쓰므로 네 메뉴의 스플래시 시작 지점이 같은 열에 정렬된다.
/// GameObject는 항상 활성 상태로 두고 Image 표시만 켜고 꺼서 비호버 상태에서도 코루틴을 받을 수 있게 한다.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class TitleMenuSelectSplash : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [Tooltip("프레임 사이 간격(초). 마지막 프레임은 호버가 유지되는 동안 계속 표시된다.")]
    [SerializeField, Min(0.01f)] private float frameInterval = 0.06f;

    private Image image;
    private RectTransform rectTransform;
    private RectTransform currentTarget;
    private Coroutine playRoutine;

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = (RectTransform)transform;
        // 씬에서는 편집 편의를 위해 보이게 두고, 실행 중에는 호버 전까지 숨긴다.
        image.enabled = false;
    }

    /// <summary>호버된 버튼 위치에서 스플래시 애니메이션을 처음부터 한 번 재생한다.</summary>
    public void PlayFor(RectTransform target)
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[TitleMenuSelectSplash] frames or target is not assigned.", this);
            return;
        }

        currentTarget = target;

        // 가로 위치는 고정하고 호버된 버튼의 세로 위치만 따라간다.
        Vector2 position = rectTransform.anchoredPosition;
        position.y = target.anchoredPosition.y;
        rectTransform.anchoredPosition = position;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayOnceRoutine());
    }

    /// <summary>버튼에서 호버가 벗어났을 때 스플래시를 숨긴다. 이미 다른 버튼으로 옮겨갔으면 무시한다.</summary>
    public void HideFor(RectTransform target)
    {
        if (currentTarget != target)
        {
            return;
        }

        currentTarget = null;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        image.enabled = false;
    }

    private IEnumerator PlayOnceRoutine()
    {
        image.enabled = true;

        // 마지막 프레임은 대기 없이 남겨 호버가 끝날 때까지 유지한다.
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                image.sprite = frames[i];
            }

            if (i < frames.Length - 1)
            {
                yield return new WaitForSecondsRealtime(frameInterval);
            }
        }

        playRoutine = null;
    }
}
