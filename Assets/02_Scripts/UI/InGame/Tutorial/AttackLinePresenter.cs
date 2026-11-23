using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기물의 공격 판정 근거를 보드 위 사선으로 그린다.
/// 판정 규칙은 PieceBase와 같다: 정면 직선, 사거리 안에서 가장 가까운 기물이 적이면 공격한다.
/// - 공격: 빨간 사선 + 대상 원형 표식. 권총은 총알이 관통하므로 보드 끝까지 긋고, 사선 위 아군은 주황 경고 표식.
/// - 막힘: 회색 사선을 막은 기물까지만 긋고 X 표식.
/// - 대상 없음: 흐린 사선을 사거리 끝까지 긋는다.
/// 튜토리얼 Director가 생성할 때만 존재하며, 인스턴스가 없으면 정적 호출은 아무 것도 하지 않는다.
/// </summary>
public sealed class AttackLinePresenter : MonoBehaviour
{
    private const float GroundOffset = 0.09f;
    private const float LineStartInset = 0.32f;
    private const float MarkerRadius = 0.42f;
    private const int RingSegments = 28;

    private static readonly Color AttackColor = new(0.96f, 0.22f, 0.18f, 0.95f);
    private static readonly Color FriendlyFireColor = new(1f, 0.62f, 0.12f, 1f);
    private static readonly Color BlockedColor = new(0.85f, 0.85f, 0.82f, 0.75f);
    private static readonly Color NoTargetColor = new(0.85f, 0.85f, 0.82f, 0.3f);

    private static AttackLinePresenter instance;

    private readonly List<LineRenderer> pool = new();
    private readonly List<PieceBase> lineBuffer = new();
    private Material lineMaterial;
    private PieceBase hoverPiece;
    // 튜토리얼 대사가 설명 중인 기물. 대사 모달 동안에는 hover가 꺼지므로 따로 고정해 보여준다.
    private PieceBase pinnedPiece;
    private int usedCount;

    /// <summary>true면 배치 단계 동안 모든 적의 사선을 항상 표시한다.</summary>
    public bool ShowEnemyLines { get; set; }

    public static AttackLinePresenter Create(Transform parent)
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject root = new("AttackLinePresenter");
        root.transform.SetParent(parent, false);
        return root.AddComponent<AttackLinePresenter>();
    }

    public static void ShowHover(PieceBase piece)
    {
        if (instance != null)
        {
            instance.hoverPiece = piece;
        }
    }

    public static void HideHover()
    {
        if (instance != null)
        {
            instance.hoverPiece = null;
        }
    }

    public void SetPinned(PieceBase piece)
    {
        pinnedPiece = piece;
    }

    private void Awake()
    {
        instance = this;

        // Sprites/Default는 GraphicsSettings의 Always Included Shaders에 포함돼 빌드에서도 찾을 수 있다.
        Shader shader = Shader.Find("Sprites/Default");
        lineMaterial = shader != null ? new Material(shader) : new Material(Canvas.GetDefaultCanvasMaterial());
        lineMaterial.renderQueue = 3100;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }

    private void LateUpdate()
    {
        usedCount = 0;

        if (CanDraw(out IReadOnlyList<PieceBase> pieces))
        {
            if (ShowEnemyLines)
            {
                for (int index = 0; index < pieces.Count; index++)
                {
                    PieceBase piece = pieces[index];
                    if (piece != null && !piece.IsDead && piece.Faction == Faction.Enemy &&
                        piece != hoverPiece && piece != pinnedPiece)
                    {
                        DrawPiece(piece, pieces, 0.75f, 0.05f);
                    }
                }
            }

            if (pinnedPiece != null && !pinnedPiece.IsDead && pinnedPiece != hoverPiece)
            {
                DrawPiece(pinnedPiece, pieces, 1f, 0.075f);
            }

            // Unity null 비교로 파괴된 배치 미리보기 기물도 걸러낸다.
            if (hoverPiece != null && !hoverPiece.IsDead)
            {
                DrawPiece(hoverPiece, pieces, 1f, 0.075f);
            }
        }

        for (int index = usedCount; index < pool.Count; index++)
        {
            if (pool[index].gameObject.activeSelf)
            {
                pool[index].gameObject.SetActive(false);
            }
        }
    }

    private static bool CanDraw(out IReadOnlyList<PieceBase> pieces)
    {
        pieces = null;
        SimulationController simulation = SimulationController.Instance;
        if (simulation != null && (simulation._isRunning || simulation.IsExecutingSimulation))
        {
            return false;
        }

        if (StageManager.Instance == null || GameManager.Instance == null)
        {
            return false;
        }

        pieces = StageManager.Instance.GetAllActivePieces();
        return pieces != null;
    }

    private void DrawPiece(PieceBase piece, IReadOnlyList<PieceBase> pieces, float alphaScale, float width)
    {
        if (piece.SimulationPhaseIndex <= 0)
        {
            // 시민처럼 행동하지 않는 기물은 사선이 없다.
            return;
        }

        int boardSize = StageManager.Instance.CurrentStageData?.boardSize ?? 6;
        var (dx, dy) = piece.GetFacingDelta();

        // 사거리와 보드 경계 중 가까운 쪽까지 정면 직선의 첫 기물을 찾는다.
        int lastInBoard = 0;
        int firstDistance = 0;
        PieceBase first = null;
        for (int distance = 1; distance <= piece.AttackRange; distance++)
        {
            int x = piece.GridX + dx * distance;
            int y = piece.GridY + dy * distance;
            if (!IsInBoard(x, y, boardSize))
            {
                break;
            }

            lastInBoard = distance;
            PieceBase occupant = FindPieceAt(pieces, piece, x, y);
            if (occupant != null)
            {
                first = occupant;
                firstDistance = distance;
                break;
            }
        }

        PieceBase target = piece.GetCurrentActionTarget(pieces);
        bool willAttack = target != null && target == first;

        Vector3 origin = CellPosition(boardSize, piece.GridX, piece.GridY);
        Vector3 direction = new(dx, 0f, dy);
        Vector3 start = origin + direction * LineStartInset;

        if (willAttack && piece is GunmanPiece)
        {
            // 총알은 관통해 보드 끝까지 날아가므로 사선 위 모든 기물이 맞는다.
            int edgeDistance = DistanceToEdge(piece.GridX, piece.GridY, dx, dy, boardSize);
            Vector3 end = CellPosition(boardSize, piece.GridX + dx * edgeDistance, piece.GridY + dy * edgeDistance) +
                          direction * 0.5f;
            DrawSegment(start, end, Scale(AttackColor, alphaScale), width);

            CollectPiecesOnLine(pieces, piece, dx, dy, edgeDistance, boardSize);
            for (int index = 0; index < lineBuffer.Count; index++)
            {
                PieceBase hit = lineBuffer[index];
                Color color = piece.IsEnemyOf(hit) ? AttackColor : FriendlyFireColor;
                DrawRing(hit.transform.position, Scale(color, alphaScale), width);
            }

            return;
        }

        if (willAttack)
        {
            Vector3 end = CellPosition(boardSize, first.GridX, first.GridY) - direction * MarkerRadius;
            DrawSegment(start, end, Scale(AttackColor, alphaScale), width);
            DrawRing(first.transform.position, Scale(AttackColor, alphaScale), width);
            return;
        }

        if (first != null)
        {
            Vector3 end = CellPosition(boardSize, first.GridX, first.GridY) - direction * MarkerRadius;
            DrawSegment(start, end, Scale(BlockedColor, alphaScale), width * 0.8f);
            DrawCross(first.transform.position, Scale(BlockedColor, alphaScale), width);
            return;
        }

        if (lastInBoard > 0)
        {
            Vector3 end = CellPosition(boardSize, piece.GridX + dx * lastInBoard, piece.GridY + dy * lastInBoard);
            DrawSegment(start, end, Scale(NoTargetColor, alphaScale), width * 0.7f);
        }
    }

    private void CollectPiecesOnLine(
        IReadOnlyList<PieceBase> pieces,
        PieceBase shooter,
        int dx,
        int dy,
        int maxDistance,
        int boardSize)
    {
        lineBuffer.Clear();
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            int x = shooter.GridX + dx * distance;
            int y = shooter.GridY + dy * distance;
            if (!IsInBoard(x, y, boardSize))
            {
                break;
            }

            PieceBase occupant = FindPieceAt(pieces, shooter, x, y);
            if (occupant != null)
            {
                lineBuffer.Add(occupant);
            }
        }
    }

    private static PieceBase FindPieceAt(IReadOnlyList<PieceBase> pieces, PieceBase self, int x, int y)
    {
        for (int index = 0; index < pieces.Count; index++)
        {
            PieceBase piece = pieces[index];
            if (piece != null && piece != self && !piece.IsDead && piece.GridX == x && piece.GridY == y)
            {
                return piece;
            }
        }

        return null;
    }

    private static int DistanceToEdge(int x, int y, int dx, int dy, int boardSize)
    {
        int distance = 0;
        while (IsInBoard(x + dx * (distance + 1), y + dy * (distance + 1), boardSize))
        {
            distance++;
        }

        return distance;
    }

    private static bool IsInBoard(int x, int y, int boardSize)
    {
        return x >= 0 && y >= 0 && x < boardSize && y < boardSize;
    }

    private static Vector3 CellPosition(int boardSize, int x, int y)
    {
        Vector3 position = GameManager.Instance.GetCellPosition(StageGridIndexUtility.ToCellIndex(boardSize, x, y));
        position.y += GroundOffset;
        return position;
    }

    private void DrawSegment(Vector3 from, Vector3 to, Color color, float width)
    {
        LineRenderer line = Acquire(color, width, false);
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }

    private void DrawRing(Vector3 center, Color color, float width)
    {
        LineRenderer line = Acquire(color, width, true);
        line.positionCount = RingSegments;
        float y = GroundHeight(center);
        for (int index = 0; index < RingSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / RingSegments;
            line.SetPosition(index, new Vector3(
                center.x + Mathf.Cos(angle) * MarkerRadius,
                y,
                center.z + Mathf.Sin(angle) * MarkerRadius));
        }
    }

    private void DrawCross(Vector3 center, Color color, float width)
    {
        float y = GroundHeight(center);
        float size = MarkerRadius * 0.75f;
        DrawSegment(
            new Vector3(center.x - size, y, center.z - size),
            new Vector3(center.x + size, y, center.z + size),
            color,
            width);
        DrawSegment(
            new Vector3(center.x - size, y, center.z + size),
            new Vector3(center.x + size, y, center.z - size),
            color,
            width);
    }

    private static float GroundHeight(Vector3 piecePosition)
    {
        // 기물 루트는 칸 바닥에 서 있으므로 같은 높이에 띄워 바닥 표식처럼 보이게 한다.
        return piecePosition.y + GroundOffset;
    }

    private LineRenderer Acquire(Color color, float width, bool loop)
    {
        LineRenderer line;
        if (usedCount < pool.Count)
        {
            line = pool[usedCount];
        }
        else
        {
            GameObject lineObject = new($"AttackLine_{pool.Count}");
            lineObject.transform.SetParent(transform, false);
            // TransformZ 정렬 + 위를 향한 Z축으로 선을 바닥에 눕혀 카메라 회전과 무관하게 같은 두께로 보이게 한다.
            lineObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.sharedMaterial = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            pool.Add(line);
        }

        usedCount++;
        line.gameObject.SetActive(true);
        line.loop = loop;
        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = width;
        return line;
    }

    private static Color Scale(Color color, float alphaScale)
    {
        color.a *= alphaScale;
        return color;
    }
}
