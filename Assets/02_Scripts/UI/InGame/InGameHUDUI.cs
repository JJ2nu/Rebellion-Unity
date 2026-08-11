using UnityEngine;

public class InGameHUDUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Transform mainCameraTransform;
    [SerializeField] private GameObject _activeUI;
    [SerializeField] private GameObject _inactiveUI;
    [SerializeField] private GameObject _rotateUI;
    [SerializeField] private GameObject _deadUI;
    [SerializeField] private GameObject _targetUI;
    // 튜토리얼 가이드 기물 전용 표식이다. 가이드 Prefab(TutorialGuide_*)의 HUD에만 연결하고,
    // 실제 기물 HUD에서는 비워 둔다. 비어 있으면 Guide 상태는 기존처럼 비활성 아이콘으로 표시된다.
    [SerializeField] private GameObject _guideUI;
    private bool keepGuideStateOnStart;


    private void OnEnable()
    {
        RefreshMainCameraTransform();
    }

    void Start()
    {
        RefreshMainCameraTransform();
        if (keepGuideStateOnStart)
        {
            Guide();
        }
        else
        {
            Clear();
        }
    }
    void Awake()
    {
        if (_activeUI == null || _inactiveUI == null || _rotateUI == null || _deadUI == null || _targetUI == null)
        {
            Debug.LogWarning($"{nameof(InGameHUDUI)} has missing HUD state references.", this);
        }
    }

    void LateUpdate()
    {
        // 피스가 Scene 전환 뒤 풀에서 재사용되면 이전 Main Camera는 이미 파괴됐을 수 있다.
        if (mainCameraTransform == null)
        {
            RefreshMainCameraTransform();
            if (mainCameraTransform == null)
            {
                return;
            }
        }

        // 카메라가 바라보는 방향을 향해 회전
        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                         mainCameraTransform.rotation * Vector3.up);
    }

    private void RefreshMainCameraTransform()
    {
        Camera mainCamera = Camera.main;
        mainCameraTransform = mainCamera != null ? mainCamera.transform : null;
    }

    public void Active()
    {
        _activeUI.SetActive(true);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false);
        SetGuideActive(false);
    }

    public void InitializeGuide()
    {
        keepGuideStateOnStart = true;
        Guide();
    }

    private void Guide()
    {
        // 가이드 전용 표식이 연결된 HUD(가이드 Prefab)는 실제 기물과 구분되는 표식만 표시한다.
        // 표식이 없는 기존 HUD는 이전과 같이 비활성 색상의 종류 아이콘으로 대체한다.
        if (_guideUI != null)
        {
            _activeUI.SetActive(false);
            _inactiveUI.SetActive(false);
            _rotateUI.SetActive(false);
            _deadUI.SetActive(false);
            _targetUI.SetActive(false);
            _guideUI.SetActive(true);
            return;
        }

        Inactive();
    }
    public void Inactive()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(true);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false);
        SetGuideActive(false);
    }
    public void Rotate()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(true);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false);
        SetGuideActive(false);
    }
    public void Dead()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(true);
        _targetUI.SetActive(false);
        SetGuideActive(false);
    }
    public void Target()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(true);
        SetGuideActive(false);
    }
    public void Clear()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false);
        SetGuideActive(false);
    }

    private void SetGuideActive(bool isActive)
    {
        // 가이드 표식은 선택 연결이므로 없는 HUD에서는 아무 것도 하지 않는다.
        if (_guideUI != null)
        {
            _guideUI.SetActive(isActive);
        }
    }
}
