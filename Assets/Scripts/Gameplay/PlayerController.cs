using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Controls player movement, jumping, and dash using the new Input System.
    /// Requires a Rigidbody2D and a collider on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float jumpForce = 16f;
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.15f;
        [SerializeField] private float dashCooldown = 0.8f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody2D rb;
        private Animator animator;

        private Vector2 moveInput;
        private bool isGrounded;
        private bool isDashing;
        private bool canDash = true;
        private float dashTimer;
        private float dashCooldownTimer;

        private static readonly int AnimMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimIsDashing = Animator.StringToHash("IsDashing");

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            CheckGround();
            UpdateDash();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (!isDashing)
                Move();
        }

        private void CheckGround()
        {
            if (groundCheck == null) return;
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        private void Move()
        {
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);

            if (moveInput.x > 0f)
                transform.localScale = new Vector3(1f, 1f, 1f);
            else if (moveInput.x < 0f)
                transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        private void UpdateDash()
        {
            if (isDashing)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    isDashing = false;
                    rb.gravityScale = 1f;
                }
            }

            if (!canDash)
            {
                dashCooldownTimer -= Time.deltaTime;
                if (dashCooldownTimer <= 0f)
                    canDash = true;
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            animator.SetFloat(AnimMoveSpeed, Mathf.Abs(moveInput.x));
            animator.SetBool(AnimIsGrounded, isGrounded);
            animator.SetBool(AnimIsDashing, isDashing);
        }

        // Input System callbacks
        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed && isGrounded)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        public void OnDash(InputValue value)
        {
            if (value.isPressed && canDash && !isDashing)
                StartDash();
        }

        private void StartDash()
        {
            isDashing = true;
            canDash = false;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;

            float dashDirection = moveInput.x != 0f ? Mathf.Sign(moveInput.x) : transform.localScale.x;
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
