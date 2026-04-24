// Adapted from Client/Contents/GameObjects/Map/Grid/GridObject.h and
// GridObject.cpp of 4Q-Rebellion (C++).
//
// Key adaptations:
//  - C++ raw pointer arrays → Unity List<GridCell> and Character[,] lookup table.
//  - C++ world->CreateGameObject<GridObject>() and position set inside
//    the Map constructor → Unity Instantiate in Awake / SetupGrid().
//  - GetActualPositionAt → WorldPositionAt using cellSize and transform.
//  - Selection / hover modes preserved; Unity uses raycasting against cell
//    colliders (triggered externally) instead of the custom PhysX queries.
//  - idx() helper is replaced by a 2D array for O(1) lookup.

using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Manages a rectangular grid of <see cref="GridCell"/> objects.
    /// Provides placement, movement, and query APIs used by
    /// <see cref="BattleManager"/> and <see cref="Character"/>.
    /// </summary>
    public class GridMap : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Grid Dimensions")]
        [SerializeField] private int width  = 6;
        [SerializeField] private int height = 6;
        [SerializeField] private float cellSize = 1.4f;

        [Header("Prefabs")]
        [Tooltip("Prefab with a GridCell component placed at each grid position.")]
        [SerializeField] private GridCell cellPrefab;

        // ── Runtime state ─────────────────────────────────────────────────
        /// <summary>Flat list of all instantiated cells (row-major, w + h*width).</summary>
        private List<GridCell> _cells = new List<GridCell>();

        /// <summary>Fast O(1) lookup: which Character occupies [w, h].</summary>
        private Character[,] _occupants;

        public int Width  => width;
        public int Height => height;
        public float CellSize => cellSize;

        /// <summary>The cell currently selected by the player (may be null).</summary>
        public GridCell SelectedCell { get; private set; }

        public bool IsSelectionModeOn  { get; private set; }
        public bool IsGridHoverTurnedOn { get; private set; }

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            SetupGrid(width, height, cellSize);
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// (Re)create the grid.  Mirrors GridObject::CreateGrid.
        /// </summary>
        public void SetupGrid(int w, int h, float size)
        {
            ClearGrid();

            width    = w;
            height   = h;
            cellSize = size;
            _occupants = new Character[w, h];

            for (int row = 0; row < h; row++)
            {
                for (int col = 0; col < w; col++)
                {
                    Vector3 worldPos = WorldPositionAt(col, row);
                    GridCell cell;
                    if (cellPrefab != null)
                        cell = Instantiate(cellPrefab, worldPos, Quaternion.identity, transform);
                    else
                    {
                        var go = new GameObject($"Cell_{col}_{row}");
                        go.transform.SetParent(transform);
                        go.transform.position = worldPos;
                        cell = go.AddComponent<GridCell>();
                    }
                    cell.SetCellPosition(col, row);
                    _cells.Add(cell);
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="GridCell"/> at (w, h), or null if out of bounds.
        /// Mirrors GridObject::GetCellObjectAt.
        /// </summary>
        public GridCell GetCellAt(int w, int h)
        {
            if (!InBounds(w, h)) return null;
            return _cells[Idx(w, h)];
        }

        /// <summary>
        /// Place a character on a cell that is not yet occupied.
        /// Mirrors GridObject::PlaceGameObjectAt.
        /// </summary>
        /// <returns>True if successfully placed.</returns>
        public bool PlaceCharacterAt(Character character, int w, int h)
        {
            if (!InBounds(w, h)) return false;
            if (_occupants[w, h] != null) return false;

            _occupants[w, h] = character;
            GridCell cell = GetCellAt(w, h);
            if (cell != null) { cell.IsOccupied = true; cell.Occupant = character; }
            return true;
        }

        /// <summary>
        /// Force-place a character, displacing any existing occupant.
        /// Mirrors GridObject::ReplaceGameObjectAt.
        /// </summary>
        public void ReplaceCharacterAt(Character character, int w, int h)
        {
            if (!InBounds(w, h)) return;
            _occupants[w, h] = character;
            GridCell cell = GetCellAt(w, h);
            if (cell != null) { cell.IsOccupied = true; cell.Occupant = character; }
        }

        /// <summary>
        /// Remove a character from whichever cell it currently occupies.
        /// Mirrors GridObject::RemoveGameObject.
        /// </summary>
        public void RemoveCharacter(Character character)
        {
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    if (_occupants[w, h] == character)
                    {
                        _occupants[w, h] = null;
                        GridCell cell = GetCellAt(w, h);
                        if (cell != null) { cell.IsOccupied = false; cell.Occupant = null; }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Move an already-placed character to a new cell.
        /// Mirrors GridObject::MoveGameObjectTo.
        /// </summary>
        /// <returns>True if the destination was vacant and the move succeeded.</returns>
        public bool MoveCharacterTo(Character character, int w, int h)
        {
            if (!InBounds(w, h)) return false;
            if (_occupants[w, h] != null && _occupants[w, h] != character) return false;

            RemoveCharacter(character);
            return PlaceCharacterAt(character, w, h);
        }

        /// <summary>
        /// Returns the character at (w, h), or null if the cell is empty.
        /// Mirrors GridObject::GetGameObjectAt.
        /// </summary>
        public Character GetCharacterAt(int w, int h)
        {
            if (!InBounds(w, h)) return null;
            return _occupants[w, h];
        }

        /// <summary>
        /// Whether (w, h) is occupied by <paramref name="character"/>.
        /// Mirrors GridObject::IsGameObjectAt.
        /// </summary>
        public bool IsCharacterAt(Character character, int w, int h)
        {
            if (!InBounds(w, h)) return false;
            return _occupants[w, h] == character;
        }

        /// <summary>
        /// Convert grid coordinates to a world-space position.
        /// Mirrors GridObject::GetActualPositionAt.
        /// Grid origin is at the transform's position (bottom-left corner).
        /// </summary>
        public Vector3 WorldPositionAt(int w, int h)
        {
            return transform.position + new Vector3(w * cellSize, 0f, h * cellSize);
        }

        /// <summary>
        /// Remove all characters and reset every cell.
        /// Mirrors GridObject::ClearGrid.
        /// </summary>
        public void ClearGrid()
        {
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    cell.ClearCell();
                    Destroy(cell.gameObject);
                }
            }
            _cells.Clear();

            if (_occupants != null)
                System.Array.Clear(_occupants, 0, _occupants.Length);
        }

        /// <summary>Reset every cell's type to Default without removing occupants.</summary>
        public void ResetCellTypes()
        {
            foreach (var cell in _cells)
                cell?.SetCellType(CellType.Default);
        }

        /// <summary>Mark a cell as the selected one (used by BattleManager/UI).</summary>
        public void SelectCell(GridCell cell) => SelectedCell = cell;

        // ── Selection / hover toggles (mirrors GridObject API) ────────────

        public void TurnOnSelectionMode()  => IsSelectionModeOn = true;
        public void TurnOffSelectionMode() => IsSelectionModeOn = false;

        public void TurnOnGridHover()  => IsGridHoverTurnedOn = true;
        public void TurnOffGridHover() => IsGridHoverTurnedOn = false;

        // ── Helpers ───────────────────────────────────────────────────────

        public bool InBounds(int w, int h) =>
            w >= 0 && w < width && h >= 0 && h < height;

        private int Idx(int w, int h) => w + h * width;
    }
}
