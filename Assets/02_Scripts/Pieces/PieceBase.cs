using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion
{
    /// <summary>
    /// 모든 기물의 베이스 클래스. 그리드 위에 배치되어 시뮬레이션에 참여한다.
    /// 새로운 기물 추가 시 이 클래스를 상속하고 ExecuteAction()과 FindTarget()을 구현한다.
    /// </summary>
    public abstract class PieceBase : MonoBehaviour
    {
        // ─── Inspector ──────────────────────────────────────────────────
        [Header("Piece Config")]
        [SerializeField] private Faction _faction = Faction.Ally;
        [SerializeField] private PieceType _pieceType = PieceType.Brawler;
        [SerializeField] private int _maxHealth = 1;
        [SerializeField] private int _attackRange = 1;

        // ─── Properties ─────────────────────────────────────────────────
        public Faction Faction => _faction;
        public PieceType PieceType => _pieceType;
        public int MaxHealth => _maxHealth;
        public int AttackRange => _attackRange;

        public int GridX { get; set; }
        public int GridY { get; set; }
        public Direction FacingDirection { get; set; } = Direction.East;

        public int CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsActionFinished { get; protected set; }

        // ─── Events ─────────────────────────────────────────────────────
        public event Action<PieceBase> OnDied;
        public event Action<PieceBase, int> OnDamageTaken;
        public event Action<PieceBase> OnActionFinished;

        // ─── Simulation lifecycle ────────────────────────────────────────

        public virtual void OnSimulationStart()
        {
            CurrentHealth = _maxHealth;
            IsDead = false;
            IsActionFinished = false;
        }

        /// <summary>
        /// 시뮬레이션 단계에서 이 기물이 수행할 행동. 코루틴으로 구현한다.
        /// </summary>
        public abstract IEnumerator ExecuteAction(IReadOnlyList<PieceBase> allPieces);

        /// <summary>
        /// 현재 방향과 사정거리를 기준으로 공격 대상을 탐색한다.
        /// </summary>
        protected abstract PieceBase FindTarget(IReadOnlyList<PieceBase> allPieces);

        // ─── Combat ─────────────────────────────────────────────────────

        public virtual void TakeDamage(int damage)
        {
            if (IsDead) return;

            CurrentHealth -= damage;
            OnDamageTaken?.Invoke(this, damage);

            if (CurrentHealth <= 0)
                Die();
        }

        public virtual void Die()
        {
            if (IsDead) return;

            IsDead = true;
            IsActionFinished = true;
            OnDied?.Invoke(this);
        }

        // ─── Grid Helpers ────────────────────────────────────────────────

        /// <summary>
        /// 현재 방향 기준의 그리드 델타 (dx, dy)를 반환한다.
        /// </summary>
        public (int dx, int dy) GetFacingDelta()
        {
            return FacingDirection switch
            {
                Direction.North => (0, 1),
                Direction.East  => (1, 0),
                Direction.South => (0, -1),
                Direction.West  => (-1, 0),
                _ => (0, 0),
            };
        }

        /// <summary>
        /// 두 기물 사이의 맨해튼 거리를 반환한다.
        /// </summary>
        public int ManhattanDistanceTo(PieceBase other)
        {
            return Mathf.Abs(GridX - other.GridX) + Mathf.Abs(GridY - other.GridY);
        }

        /// <summary>
        /// 대상이 이 기물의 정면 방향 직선상 사정거리 이내에 있는지 확인한다.
        /// </summary>
        public bool IsInLineOfFire(PieceBase target)
        {
            var (dx, dy) = GetFacingDelta();

            for (int i = 1; i <= _attackRange; i++)
            {
                if (GridX + dx * i == target.GridX && GridY + dy * i == target.GridY)
                    return true;
            }
            return false;
        }

        // ─── Protected Utilities ─────────────────────────────────────────

        protected void FinishAction()
        {
            IsActionFinished = true;
            OnActionFinished?.Invoke(this);
        }

        protected IEnumerator WaitForSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
