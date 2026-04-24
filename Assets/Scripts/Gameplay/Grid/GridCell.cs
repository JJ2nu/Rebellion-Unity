// Adapted from Client/Contents/GameObjects/Map/Grid/CellObject.h and
// CellObject.cpp of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - C++ model-swapping for visual cell state → Unity child GameObjects
//    activated/deactivated per CellType (same design, Unity idiom).
//  - C++ BoundingOrientedBox for mouse hover → Unity Collider on the
//    cell prefab (configured in the Inspector).
//  - Grid coordinate tracking (w, h) and occupancy flag are preserved
//    as-is.

using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// A single cell in the tactical grid.
    /// Tracks its grid coordinate, current visual type, and whether it
    /// is occupied by a <see cref="Character"/>.
    /// </summary>
    public class GridCell : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Visual State Objects")]
        [Tooltip("Root shown when CellType is Default.")]
        [SerializeField] private GameObject defaultVisual;
        [Tooltip("Root shown when CellType is Placement.")]
        [SerializeField] private GameObject placementVisual;
        [Tooltip("Root shown when CellType is RangeZone.")]
        [SerializeField] private GameObject rangeVisual;
        [Tooltip("Root shown when CellType is DashZone.")]
        [SerializeField] private GameObject dashVisual;
        [Tooltip("Root shown when CellType is DamageZone.")]
        [SerializeField] private GameObject damageVisual;

        // ── Runtime state (mirrors CellObject fields) ─────────────────────
        /// <summary>Grid column (w) and row (h) of this cell.</summary>
        public int GridW { get; private set; } = -1;
        public int GridH { get; private set; } = -1;

        /// <summary>Whether a character currently occupies this cell.</summary>
        public bool IsOccupied { get; set; }

        /// <summary>The character currently standing on this cell (may be null).</summary>
        public Character Occupant { get; set; }

        private CellType _cellType = CellType.Default;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Assign the grid coordinates of this cell.
        /// Mirrors CellObject::SetCellPosition.
        /// </summary>
        public void SetCellPosition(int w, int h)
        {
            GridW = w;
            GridH = h;
        }

        /// <returns>The current grid position as a value tuple.</returns>
        public (int w, int h) GetCellPosition() => (GridW, GridH);

        /// <summary>
        /// Change the visual state of the cell.
        /// Mirrors CellObject::SetCellType – swaps the active child visual.
        /// </summary>
        public void SetCellType(CellType type)
        {
            _cellType = type;
            ApplyCellType();
        }

        public CellType GetCellType() => _cellType;

        /// <summary>
        /// Show or hide the cell mesh.
        /// Mirrors CellObject::SetVisible / SetInvisible.
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// Reset the cell to its default visual and clear occupancy.
        /// Mirrors CellObject::ClearCell.
        /// </summary>
        public void ClearCell()
        {
            IsOccupied = false;
            Occupant   = null;
            SetCellType(CellType.Default);
        }

        // ── Private helpers ───────────────────────────────────────────────

        private void ApplyCellType()
        {
            if (defaultVisual)   defaultVisual.SetActive(_cellType   == CellType.Default);
            if (placementVisual) placementVisual.SetActive(_cellType == CellType.Placement);
            if (rangeVisual)     rangeVisual.SetActive(_cellType     == CellType.RangeZone);
            if (dashVisual)      dashVisual.SetActive(_cellType      == CellType.DashZone);
            if (damageVisual)    damageVisual.SetActive(_cellType    == CellType.DamageZone);
        }

        private void Awake()
        {
            ApplyCellType();
        }
    }
}
