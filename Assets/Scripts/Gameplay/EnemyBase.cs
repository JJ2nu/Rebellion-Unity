using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Base class for all enemy AI. Defines common patrol, chase, and attack states.
    /// Subclass this to implement enemy-specific behavior.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    public class EnemyBase : MonoBehaviour
    {
        public enum EnemyState { Idle, Patrol, Chase, Attack, Dead }

        [Header("Detection")]
        [SerializeField] protected float detectionRange = 6f;
        [SerializeField] protected float attackRange = 1.5f;
        [SerializeField] protected LayerMask playerLayer;

        [Header("Movement")]
        [SerializeField] protected float moveSpeed = 3f;
        [SerializeField] protected float patrolDistance = 3f;

        [Header("Attack")]
        [SerializeField] protected int attackDamage = 10;
        [SerializeField] protected float attackCooldown = 1.5f;

        [Header("Drops")]
        [SerializeField] protected int expValue = 10;

        protected Rigidbody2D rb;
        protected Animator animator;
        protected HealthSystem healthSystem;
        protected Transform player;

        protected EnemyState currentState = EnemyState.Idle;
        protected float attackTimer;
        protected Vector3 startPosition;
        protected float patrolTarget;
        protected bool patrolRight = true;

        private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead = Animator.StringToHash("Dead");

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            healthSystem = GetComponent<HealthSystem>();
            startPosition = transform.position;
        }

        protected virtual void OnEnable()
        {
            healthSystem.OnDeath += OnDeath;
        }

        protected virtual void OnDisable()
        {
            healthSystem.OnDeath -= OnDeath;
        }

        protected virtual void Update()
        {
            if (currentState == EnemyState.Dead) return;

            FindPlayer();
            UpdateStateMachine();
            attackTimer -= Time.deltaTime;
        }

        protected virtual void FindPlayer()
        {
            Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
            player = playerCollider != null ? playerCollider.transform : null;
        }

        protected virtual void UpdateStateMachine()
        {
            if (player == null)
            {
                currentState = EnemyState.Patrol;
            }
            else
            {
                float distToPlayer = Vector2.Distance(transform.position, player.position);

                if (distToPlayer <= attackRange)
                    currentState = EnemyState.Attack;
                else
                    currentState = EnemyState.Chase;
            }

            switch (currentState)
            {
                case EnemyState.Patrol:
                    Patrol();
                    break;
                case EnemyState.Chase:
                    Chase();
                    break;
                case EnemyState.Attack:
                    Attack();
                    break;
            }

            animator?.SetFloat(AnimMoveSpeed, Mathf.Abs(rb.linearVelocity.x));
        }

        protected virtual void Patrol()
        {
            float direction = patrolRight ? 1f : -1f;
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);

            if (Mathf.Abs(transform.position.x - startPosition.x) >= patrolDistance)
            {
                patrolRight = !patrolRight;
                FlipSprite(patrolRight);
            }
        }

        protected virtual void Chase()
        {
            if (player == null) return;

            float direction = Mathf.Sign(player.position.x - transform.position.x);
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            FlipSprite(direction > 0f);
        }

        protected virtual void Attack()
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                PerformAttack();
            }
        }

        protected virtual void PerformAttack()
        {
            animator?.SetTrigger(AnimAttack);
            player?.GetComponent<HealthSystem>()?.TakeDamage(attackDamage);
        }

        protected virtual void OnDeath()
        {
            currentState = EnemyState.Dead;
            rb.linearVelocity = Vector2.zero;
            rb.isKinematic = true;
            animator?.SetBool(AnimDead, true);
            StartCoroutine(DeathRoutine());
        }

        protected virtual IEnumerator DeathRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            gameObject.SetActive(false);
        }

        protected void FlipSprite(bool facingRight)
        {
            transform.localScale = new Vector3(facingRight ? 1f : -1f, 1f, 1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
