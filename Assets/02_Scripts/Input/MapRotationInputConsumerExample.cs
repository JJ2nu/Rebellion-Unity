using UnityEngine;

namespace Rebellion.Input
{
    /// <summary>
    /// 다른 개발자가 참고할 수 있는 입력 소비 예시.
    /// 현재는 Transform 회전으로만 보여주고, 실제 사용 시 카메라 리그 회전 로직으로 교체하면 된다.
    /// </summary>
    public class MapRotationInputConsumerExample : MonoBehaviour
    {
        [SerializeField] private GameplayInputRouter _inputRouter;
        [SerializeField] private Transform _rotationTarget;
        [SerializeField] private float _rotationSpeed = 90f;

        private float _rotateInput;

        private void Awake()
        {
            if (_rotationTarget == null)
                _rotationTarget = transform;
        }

        private void OnEnable()
        {
            if (_inputRouter == null)
            {
                Debug.LogError("[MapRotationInputConsumerExample] GameplayInputRouter reference is missing.", this);
                enabled = false;
                return;
            }

            _rotateInput = _inputRouter.MapRotate;
            _inputRouter.OnMapRotateChanged += HandleMapRotateChanged;
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
                _inputRouter.OnMapRotateChanged -= HandleMapRotateChanged;
        }

        private void Update()
        {
            if (Mathf.Approximately(_rotateInput, 0f))
                return;

            // 현재 입력값을 기준으로 목표 대상을 계속 회전시킨다.
            _rotationTarget.Rotate(0f, _rotateInput * _rotationSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void HandleMapRotateChanged(float rotateInput)
        {
            _rotateInput = rotateInput;
        }
    }
}
