using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Title 메뉴 버튼의 포인터 호버 진입/이탈을 공용 스플래시 연출(TitleMenuSelectSplash)로 전달한다.
/// entryBlocked 등으로 비활성화된 버튼(예: Challenge)은 스플래시를 표시하지 않는다.
/// splash 참조는 Title.unity의 Img_SelectSplash를 Inspector에서 연결한다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class TitleMenuHoverSplash : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TitleMenuSelectSplash splash;

    private Button button;
    private RectTransform rectTransform;

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = (RectTransform)transform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (splash == null || !button.interactable)
        {
            return;
        }

        splash.PlayFor(rectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (splash == null)
        {
            return;
        }

        splash.HideFor(rectTransform);
    }
}
