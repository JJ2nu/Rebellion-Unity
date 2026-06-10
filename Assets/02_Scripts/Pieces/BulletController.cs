using System.Collections;
using UnityEngine;

/// <summary>
/// 총알 충돌 데미지 처리. 관통형 — 피격 후에도 계속 날아감.
/// GunmanPiece에서 Faction을 설정한 뒤 발사해야 한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BulletController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _lifeTime = 3f;

    private Rigidbody _rigidbody;
    private Coroutine _lifeRoutine;
    private Transform _originParent;
    private Vector3 _originLocalPosition;
    private Quaternion _originLocalRotation;

    public bool IsFlying { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _originParent = transform.parent;
        _originLocalPosition = transform.localPosition;
        _originLocalRotation = transform.localRotation;

        _rigidbody.useGravity = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    public void Fire(Vector3 direction, float speed)
    {
        if (_rigidbody == null)
        {
            Initialize();
        }

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        transform.SetParent(null, true);
        gameObject.SetActive(true);

        IsFlying = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.WakeUp();
        _rigidbody.AddForce(direction.normalized * speed, ForceMode.VelocityChange);

        if (_lifeRoutine != null)
        {
            StopCoroutine(_lifeRoutine);
        }

        _lifeRoutine = StartCoroutine(DeactivateAfterLifetime());
    }

    public void Stop()
    {
        if (_lifeRoutine != null)
        {
            StopCoroutine(_lifeRoutine);
            _lifeRoutine = null;
        }

        IsFlying = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;

        if (_originParent != null)
        {
            transform.SetParent(_originParent, false);
            transform.localPosition = _originLocalPosition;
            transform.localRotation = _originLocalRotation;
        }

        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsFlying)
        {
            return;
        }

        if (other.CompareTag("Wall"))
        {
            Stop();
        }
    }

    private IEnumerator DeactivateAfterLifetime()
    {
        yield return new WaitForSeconds(_lifeTime);
        Stop();
    }
}
