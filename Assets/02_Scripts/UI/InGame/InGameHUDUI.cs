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


    void Start()
    {
        // Main Camera 캐싱
        mainCameraTransform = Camera.main.transform;
        Clear();
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
        // 카메라가 바라보는 방향을 향해 회전
        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                         mainCameraTransform.rotation * Vector3.up);
    }
    public void Active()
    {
        _activeUI.SetActive(true);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false); 
    }
    public void Inactive()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(true);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false); 
    }
    public void Rotate()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(true);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false); 
    }
    public void Dead()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(true);
        _targetUI.SetActive(false); 
    }
    public void Target()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(true); 
    }
    public void Clear()
    {
        _activeUI.SetActive(false);
        _inactiveUI.SetActive(false);
        _rotateUI.SetActive(false);
        _deadUI.SetActive(false);
        _targetUI.SetActive(false); 
    }
}
