using UnityEngine;

public class InGameHUDUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Transform mainCameraTransform;
    private GameObject _activeUI;
    private GameObject _inactiveUI;
    private GameObject _rotateUI;
    private GameObject _deadUI;
    private GameObject _targetUI;


    void Start()
    {
        // Main Camera 캐싱
        mainCameraTransform = Camera.main.transform;
        _activeUI = transform.Find("Active").gameObject;
        _inactiveUI = transform.Find("Inactive").gameObject;
        _rotateUI = transform.Find("Rotate").gameObject;
        _deadUI = transform.Find("Dead").gameObject;
        _targetUI = transform.Find("Target").gameObject;
        Clear();
    }
    void Awake()
    {
        if (_activeUI == null) _activeUI = transform.Find("Active").gameObject;
        if (_inactiveUI == null) _inactiveUI = transform.Find("Inactive").gameObject;
        if (_rotateUI == null) _rotateUI = transform.Find("Rotate").gameObject;
        if (_deadUI == null) _deadUI = transform.Find("Dead").gameObject;
        if (_targetUI == null) _targetUI = transform.Find("Target").gameObject;
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
