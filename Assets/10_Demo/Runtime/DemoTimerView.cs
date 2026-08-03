#if UNITY_EDITOR || REBELLION_DEMO_BUILD
using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 시연 제한 시간의 최종 문자열과 색상만 화면 상단 중앙에 표현한다.
/// 시간 계산과 경고 단계 판정은 DemoSessionController가 담당한다.
/// </summary>
public sealed class DemoTimerView : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;

    public bool HasRequiredReferences => timerText != null;

    public void SetVisible(bool isVisible)
    {
        gameObject.SetActive(isVisible);
    }

    public void Render(double remainingSeconds, Color color, float alphaMultiplier)
    {
        if (timerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt((float)remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
        color.a *= Mathf.Clamp01(alphaMultiplier);
        timerText.color = color;
    }
}
#endif
