using UnityEngine;

/// <summary>
/// 총알 충돌 데미지 처리. 관통형 — 피격 후에도 계속 날아감.
/// GunmanPiece에서 Faction을 설정한 뒤 발사해야 한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BulletController : MonoBehaviour
{
    private const float MinMapBounds = -5f;
    private const float MaxMapBounds = 5f;

    private Rigidbody _rigidbody;

    public bool IsFlying { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();

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
    }

    public void Stop()
    {
        IsFlying = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;

        Destroy(gameObject);
    }

    private void Update()
    {
        if (!IsFlying)
        {
            return;
        }

        Vector3 position = transform.position;
        if (position.x < MinMapBounds || position.x > MaxMapBounds ||
            position.z < MinMapBounds || position.z > MaxMapBounds)
        {
            Stop();
        }
    }
}
