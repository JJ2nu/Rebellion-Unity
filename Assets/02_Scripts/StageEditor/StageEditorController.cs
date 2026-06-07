using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class StageEditorController : MonoBehaviour
{
    private const float PanelWidth = 560f;
    private const float PanelLeft = 12f;
    private const float WorldGridY = 0.1f;

    private enum BrushMode
    {
        Select,
        Enemy,
        Civilian,
        Object,
        Erase,
    }

    [Header("Stage Files")]
    [SerializeField] private string stageFolder = "Stages";

    [Header("Preview Prefabs")]
    [SerializeField] private GameObject gridCellPrefab;
    [SerializeField] private GameObject[] mapPrefabs;
    [SerializeField] private GameObject[] enemyPiecePrefabs;
    [SerializeField] private GameObject[] civilianPiecePrefabs;

    [Header("Prefab Folders")]
    [SerializeField] private string mapPrefabFolder = "Assets/03_Prefabs/Maps/InGame";
    [SerializeField] private string enemyPrefabFolder = "Assets/03_Prefabs/Pieces/Enemy";
    [SerializeField] private string civilianPrefabFolder = "Assets/03_Prefabs/Pieces/Civilian";

    [Header("Preview Layout")]
    [SerializeField] private float cellSize = 1.3f;
    [SerializeField] private float entityYOffset = 0.12f;
    [SerializeField] private Transform previewRoot;

    private readonly List<string> stagePaths = new();
    private readonly List<GameObject> spawnedPreviewObjects = new();
    private readonly List<GridCell> spawnedGridCells = new();
    private readonly Dictionary<string, string> inputBuffers = new();
    private readonly Dictionary<string, bool> dropdownStates = new();

    private StageData currentStage;
    private string currentStagePath;
    private Vector2 fileScroll;
    private Vector2 panelScroll;
    private BrushMode brushMode = BrushMode.Select;
    private int selectedEntityIndex = -1;
    private int brushDetailType;
    private int brushFacing;
    private int newStageNumber = 1;
    private GameObject currentMapInstance;
    private GridCell hoveredGridCell;

    private void Awake()
    {
        EnsurePreviewRoot();
        RefreshPrefabReferences();
        RefreshStageList();
        Input.imeCompositionMode = IMECompositionMode.On;
        if (stagePaths.Count > 0)
        {
            LoadStage(stagePaths[0]);
        }
    }

    private void OnDisable()
    {
        if (Input.imeCompositionMode == IMECompositionMode.On)
        {
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }
    }

    private void OnGUI()
    {
        DrawPanel();
    }

    private void Update()
    {
        HandleKeyboardShortcuts();
        HandleWorldGridInteraction();
    }

    private void DrawPanel()
    {
        GUILayout.BeginArea(new Rect(PanelLeft, 12f, PanelWidth, Screen.height - 24f), GUI.skin.box);
        panelScroll = GUILayout.BeginScrollView(panelScroll);

        GUILayout.Label("Stage Editor", GUI.skin.box);
        DrawFileList();
        GUILayout.Space(8f);

        if (currentStage == null)
        {
            GUILayout.Label("No stage loaded.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        DrawStageFields();
        GUILayout.Space(8f);
        DrawBrushControls();
        GUILayout.Space(8f);
        DrawSelectedEntityControls();
        GUILayout.Space(8f);
        DrawGridEditor();
        GUILayout.Space(8f);
        DrawSaveControls();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawFileList()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
        {
            RefreshStageList();
        }

        if (GUILayout.Button("Reload", GUILayout.Width(90f)) && !string.IsNullOrEmpty(currentStagePath))
        {
            LoadStage(currentStagePath);
        }
        GUILayout.EndHorizontal();

        fileScroll = GUILayout.BeginScrollView(fileScroll, GUI.skin.box, GUILayout.Height(145f));
        for (int index = 0; index < stagePaths.Count; index++)
        {
            string stagePath = stagePaths[index];
            string fileName = Path.GetFileName(stagePath);
            bool isCurrent = string.Equals(stagePath, currentStagePath, StringComparison.OrdinalIgnoreCase);
            GUI.enabled = !isCurrent;
            if (GUILayout.Button(isCurrent ? $"> {fileName}" : fileName))
            {
                LoadStage(stagePath);
            }
            GUI.enabled = true;
        }
        GUILayout.EndScrollView();
    }

    private void DrawStageFields()
    {
        bool changed = false;
        GUILayout.Label(Path.GetFileName(currentStagePath), GUI.skin.box);
        changed |= IntField("stage.version", "Version", currentStage.version, out currentStage.version);
        if (IntField("stage.boardSize", "Board Size", currentStage.boardSize, out int boardSize))
        {
            currentStage.boardSize = Mathf.Clamp(boardSize, 1, 12);
            changed = true;
        }

        if (DrawPrefabDropdown("stage.mapIndex", "Map", mapPrefabs, currentStage.mapIndex, out int mapIndex))
        {
            currentStage.mapIndex = Mathf.Clamp(mapIndex, 0, Mathf.Max(0, MapCount - 1));
            brushDetailType = 0;
            changed = true;
        }

        changed |= TextField("stage.mainMission", "Main Mission", currentStage.mainMission, out currentStage.mainMission);
        changed |= TextField("stage.subMission1", "Sub Mission 1", currentStage.subMission1, out currentStage.subMission1);
        changed |= TextField("stage.subMission2", "Sub Mission 2", currentStage.subMission2, out currentStage.subMission2);

        bool hasOrder = GUILayout.Toggle(currentStage.hasOrder, "Has Order Skill");
        if (hasOrder != currentStage.hasOrder)
        {
            currentStage.hasOrder = hasOrder;
            changed = true;
        }

        GUILayout.Label("Ally Slots", GUI.skin.box);
        EnsureAllySlots();
        changed |= DrawAllySlot(PieceType.Brawler);
        changed |= DrawAllySlot(PieceType.Slasher);
        changed |= DrawAllySlot(PieceType.Gunman);

        if (changed)
        {
            RebuildPreview();
        }
    }

    private void DrawBrushControls()
    {
        GUILayout.Label("Placement", GUI.skin.box);
        DrawBrushModeButtons();

        GUILayout.Label("Enemy Slots", GUI.skin.box);
        DrawPlacementButtons(BrushMode.Enemy, enemyPiecePrefabs);

        GUILayout.Label("Civilian Slots", GUI.skin.box);
        DrawPlacementButtons(BrushMode.Civilian, civilianPiecePrefabs);

        GUILayout.Label("Object Slots", GUI.skin.box);
        DrawPlacementButtons(BrushMode.Object, GetObjectPrefabs(currentStage.mapIndex));

        if (DrawFacingList("Placement Direction", brushFacing, out int facing))
        {
            brushFacing = facing;
        }

        string prefabName = brushMode switch
        {
            BrushMode.Enemy => GetName(enemyPiecePrefabs, brushDetailType),
            BrushMode.Civilian => GetName(civilianPiecePrefabs, brushDetailType),
            BrushMode.Object => GetObjectPrefabName(currentStage.mapIndex, brushDetailType),
            _ => "-",
        };
        GUILayout.Label($"Selected: {brushMode} / {prefabName}");
        GUILayout.Label("Tip: Press Tab to rotate the current placement.");
    }

    private void DrawBrushModeButtons()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        DrawBrushModeButton(BrushMode.Select, "Select");
        DrawBrushModeButton(BrushMode.Erase, "Erase");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawBrushModeButton(BrushMode mode, string label)
    {
        Color oldColor = GUI.backgroundColor;
        if (brushMode == mode)
        {
            GUI.backgroundColor = Color.yellow;
        }

        if (GUILayout.Button(label, GUILayout.Width(110f), GUILayout.Height(28f)))
        {
            brushMode = mode;
        }

        GUI.backgroundColor = oldColor;
    }

    private void DrawPlacementButtons(BrushMode mode, GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            GUILayout.Label("No prefabs found.", GUI.skin.box);
            return;
        }

        GUILayout.BeginVertical(GUI.skin.box);
        int columnCount = 2;
        for (int index = 0; index < prefabs.Length; index += columnCount)
        {
            GUILayout.BeginHorizontal();
            for (int offset = 0; offset < columnCount; offset++)
            {
                int prefabIndex = index + offset;
                if (prefabIndex >= prefabs.Length)
                {
                    GUILayout.FlexibleSpace();
                    continue;
                }

                GameObject prefab = prefabs[prefabIndex];
                string label = GetPaletteButtonLabel(mode, prefabIndex, prefab);
                bool isSelected = brushMode == mode && brushDetailType == prefabIndex;

                Color oldColor = GUI.backgroundColor;
                if (isSelected)
                {
                    GUI.backgroundColor = Color.yellow;
                }

                bool isSelectable = prefab != null;
                GUI.enabled = isSelectable;
                bool pressed = GUILayout.Button(label, GUILayout.Width(240f), GUILayout.Height(28f));
                GUI.enabled = true;

                if (pressed && isSelectable)
                {
                    brushMode = mode;
                    brushDetailType = prefabIndex;
                }

                GUI.backgroundColor = oldColor;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawSelectedEntityControls()
    {
        GUILayout.Label("Selected Cell Entity", GUI.skin.box);
        StageEntityData selected = GetSelectedEntity();
        if (selected == null)
        {
            GUILayout.Label("None");
            return;
        }

        bool changed = false;
        string keyPrefix = $"entity.{selectedEntityIndex}";
        if (DrawEntityKindList(selected.entityKind, out int entityKind))
        {
            selected.entityKind = entityKind;
            changed = true;
        }

        BrushMode selectedMode = selected.entityKind switch
        {
            0 => BrushMode.Enemy,
            1 => BrushMode.Civilian,
            2 => BrushMode.Object,
            _ => BrushMode.Select,
        };
        if (DrawDetailTypeList("Prefab", selectedMode, selected.detailType, out int detailType))
        {
            selected.detailType = detailType;
            changed = true;
        }

        if (DrawFacingList("Direction", selected.facing, out int facing))
        {
            selected.facing = facing;
            changed = true;
        }

        if (IntField($"{keyPrefix}.cellIndex", "Cell Index", selected.cellIndex, out int cellIndex))
        {
            selected.cellIndex = Mathf.Clamp(cellIndex, 0, CellCount - 1);
            changed = true;
        }

        if (GUILayout.Button("Use As Brush"))
        {
            brushMode = selected.entityKind switch
            {
                0 => BrushMode.Enemy,
                1 => BrushMode.Civilian,
                2 => BrushMode.Object,
                _ => BrushMode.Select,
            };
            brushDetailType = selected.detailType;
            brushFacing = selected.facing;
        }

        if (GUILayout.Button("Delete"))
        {
            RemoveSelectedEntity();
        }

        if (changed)
        {
            RebuildPreview();
        }
    }

    private void DrawGridEditor()
    {
        GUILayout.Label("Board (or click world tiles)", GUI.skin.box);
        int boardSize = Mathf.Max(1, currentStage.boardSize);

        for (int z = 0; z < boardSize; z++)
        {
            GUILayout.BeginHorizontal();
            for (int x = boardSize - 1; x >= 0; x--)
            {
                int cellIndex = StageGridIndexUtility.ToCellIndex(boardSize, x, z);
                int entityIndex = FindEntityIndexAtCell(cellIndex);
                string label = entityIndex >= 0
                    ? $"{cellIndex}\n{GetEntityShortLabel(currentStage.entities[entityIndex])}"
                    : $"{cellIndex}\n.";

                Color oldColor = GUI.backgroundColor;
                if (entityIndex == selectedEntityIndex)
                {
                    GUI.backgroundColor = Color.yellow;
                }

                if (GUILayout.Button(label, GUILayout.Width(58f), GUILayout.Height(46f)))
                {
                    HandleCellClick(cellIndex, entityIndex);
                }

                GUI.backgroundColor = oldColor;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawSaveControls()
    {
        GUILayout.Label("Save", GUI.skin.box);
        if (GUILayout.Button("Save Current"))
        {
            SaveCurrentStage();
        }

        if (IntField("save.newStageNumber", "New #", newStageNumber, out int stageNumber))
        {
            newStageNumber = Mathf.Max(1, stageNumber);
        }

        if (GUILayout.Button("Save Copy"))
        {
            string fileName = $"stage_{newStageNumber:000}.json";
            currentStagePath = Path.Combine(GetStageDirectory(), fileName);
            SaveCurrentStage();
            RefreshStageList();
        }
    }

    private void HandleCellClick(int cellIndex, int entityIndex)
    {
        switch (brushMode)
        {
            case BrushMode.Select:
                selectedEntityIndex = entityIndex;
                break;
            case BrushMode.Erase:
                if (entityIndex >= 0)
                {
                    RemoveEntityAt(entityIndex);
                }
                break;
            default:
                PlaceEntity(cellIndex, entityIndex);
                break;
        }

        RebuildPreview();
    }

    private void PlaceEntity(int cellIndex, int existingIndex)
    {
        StageEntityData entity = existingIndex >= 0 ? currentStage.entities[existingIndex] : new StageEntityData();
        entity.entityKind = brushMode switch
        {
            BrushMode.Enemy => 0,
            BrushMode.Civilian => 1,
            BrushMode.Object => 2,
            _ => entity.entityKind,
        };
        entity.detailType = brushDetailType;
        entity.facing = brushFacing;
        entity.cellIndex = cellIndex;

        if (existingIndex < 0)
        {
            List<StageEntityData> entities = new(currentStage.entities ?? Array.Empty<StageEntityData>())
            {
                entity
            };
            currentStage.entities = entities.ToArray();
            selectedEntityIndex = currentStage.entities.Length - 1;
        }
        else
        {
            selectedEntityIndex = existingIndex;
        }
    }

    private void RebuildPreview()
    {
        EnsurePreviewRoot();
        ClearPreview();
        SpawnMapPreview();
        SpawnGridPreview();
        SpawnEntityPreviews();
        RefreshGridCellVisuals();
    }

    private void SpawnMapPreview()
    {
        if (mapPrefabs == null || currentStage.mapIndex < 0 || currentStage.mapIndex >= mapPrefabs.Length)
        {
            return;
        }

        GameObject prefab = mapPrefabs[currentStage.mapIndex];
        if (prefab == null)
        {
            return;
        }

        currentMapInstance = Instantiate(prefab, previewRoot);
        currentMapInstance.name = $"{prefab.name}_Preview";
        SetLayerRecursively(currentMapInstance, LayerMask.NameToLayer("Ignore Raycast"));
    }

    private void SpawnEntityPreviews()
    {
        if (currentStage.entities == null)
        {
            return;
        }

        for (int index = 0; index < currentStage.entities.Length; index++)
        {
            StageEntityData entity = currentStage.entities[index];
            Vector3 position = GetCellPosition(entity.cellIndex) + Vector3.up * entityYOffset;
            Quaternion rotation = Quaternion.Euler(0f, entity.facing * 90f, 0f);

            GameObject instance = CreateEntityMarker(entity);
            instance.name = $"{GetEntityShortLabel(entity)}_{entity.cellIndex}";
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.SetParent(previewRoot, true);
            spawnedPreviewObjects.Add(instance);
        }
    }

    private void SpawnGridPreview()
    {
        if (gridCellPrefab == null || currentStage == null)
        {
            return;
        }

        int boardSize = Mathf.Max(1, currentStage.boardSize);
        spawnedGridCells.Clear();

        for (int z = 0; z < boardSize; z++)
        {
            for (int x = boardSize - 1; x >= 0; x--)
            {
                int cellIndex = StageGridIndexUtility.ToCellIndex(boardSize, x, z);
                Vector3 position = GetCellPosition(cellIndex);
                position.y = WorldGridY;

                GameObject cellObject = Instantiate(gridCellPrefab, position, Quaternion.identity, previewRoot);
                cellObject.name = $"EditorGridCell_{cellIndex}";
                GridCell gridCell = cellObject.GetComponent<GridCell>();
                if (gridCell != null)
                {
                    gridCell.Initialize(cellIndex, boardSize);
                    spawnedGridCells.Add(gridCell);
                }

                spawnedPreviewObjects.Add(cellObject);
            }
        }
    }

    private GameObject CreateEntityMarker(StageEntityData entity)
    {
        GameObject sourcePrefab = GetEntityPrefab(entity);
        GameObject marker = new GameObject(sourcePrefab != null ? $"{sourcePrefab.name}_Preview" : "MissingPrefab_Preview");
        bool copiedAnyRenderer = sourcePrefab != null && CopyVisualHierarchy(sourcePrefab.transform, marker.transform);

        if (!copiedAnyRenderer)
        {
            GameObject fallback = GameObject.CreatePrimitive(entity.entityKind == 2 ? PrimitiveType.Cube : PrimitiveType.Capsule);
            fallback.name = "FallbackMarker";
            fallback.transform.SetParent(marker.transform, false);
            fallback.transform.localScale = Vector3.one * 0.6f;
        }

        var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrow.name = "FacingArrow";
        arrow.transform.SetParent(marker.transform, false);
        arrow.transform.localPosition = new Vector3(0f, 0.3f, 0.55f);
        arrow.transform.localScale = new Vector3(0.18f, 0.08f, 0.45f);
        var arrowRenderer = arrow.GetComponent<Renderer>();
        if (arrowRenderer != null)
        {
            arrowRenderer.material.color = Color.white;
        }

        var label = new GameObject("Label");
        label.transform.SetParent(marker.transform, false);
        label.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        label.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
        var textMesh = label.AddComponent<TextMesh>();
        textMesh.text = GetEntityShortLabel(entity);
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.08f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        return marker;
    }

    private GameObject GetEntityPrefab(StageEntityData entity)
    {
        if (entity == null)
        {
            return null;
        }

        return entity.entityKind switch
        {
            0 => GetPrefab(enemyPiecePrefabs, entity.detailType),
            1 => GetPrefab(civilianPiecePrefabs, entity.detailType),
            2 => GetObjectPrefab(currentStage.mapIndex, entity.detailType),
            _ => null,
        };
    }

    private static bool CopyVisualHierarchy(Transform source, Transform targetParent)
    {
        bool copiedAnyRenderer = false;
        CopyRendererComponents(source, targetParent, ref copiedAnyRenderer);

        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            var childCopy = new GameObject(sourceChild.name);
            childCopy.transform.SetParent(targetParent, false);
            childCopy.transform.localPosition = sourceChild.localPosition;
            childCopy.transform.localRotation = sourceChild.localRotation;
            childCopy.transform.localScale = sourceChild.localScale;

            copiedAnyRenderer |= CopyVisualHierarchy(sourceChild, childCopy.transform);
        }

        return copiedAnyRenderer;
    }

    private static void CopyRendererComponents(Transform source, Transform target, ref bool copiedAnyRenderer)
    {
        MeshFilter meshFilter = source.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = source.GetComponent<MeshRenderer>();
        if (meshFilter != null && meshRenderer != null && meshFilter.sharedMesh != null)
        {
            MeshFilter copiedFilter = target.gameObject.AddComponent<MeshFilter>();
            copiedFilter.sharedMesh = meshFilter.sharedMesh;
            MeshRenderer copiedRenderer = target.gameObject.AddComponent<MeshRenderer>();
            copiedRenderer.sharedMaterials = meshRenderer.sharedMaterials;
            copiedAnyRenderer = true;
        }

        SkinnedMeshRenderer skinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
        {
            MeshFilter copiedFilter = target.gameObject.AddComponent<MeshFilter>();
            copiedFilter.sharedMesh = skinnedRenderer.sharedMesh;
            MeshRenderer copiedRenderer = target.gameObject.AddComponent<MeshRenderer>();
            copiedRenderer.sharedMaterials = skinnedRenderer.sharedMaterials;
            copiedAnyRenderer = true;
        }
    }

    private bool DrawEntityKindList(int currentKind, out int selectedKind)
    {
        return DrawStringDropdown(
            "entity.kind",
            "Kind",
            new[] { "Enemy", "Civilian", "Object" },
            currentKind,
            out selectedKind);
    }

    private bool DrawDetailTypeList(string title, BrushMode mode, int currentDetailType, out int selectedDetailType)
    {
        GameObject[] prefabs = GetPrefabListForMode(mode);
        return DrawPrefabDropdown($"detail.{mode}.{title}", title, prefabs, currentDetailType, out selectedDetailType);
    }

    private bool DrawFacingList(string title, int currentFacing, out int selectedFacing)
    {
        return DrawStringDropdown(
            $"facing.{title}",
            title,
            new[] { "북 North", "동 East", "남 South", "서 West" },
            currentFacing,
            out selectedFacing);
    }

    private GameObject[] GetPrefabListForMode(BrushMode mode)
    {
        return mode switch
        {
            BrushMode.Enemy => enemyPiecePrefabs,
            BrushMode.Civilian => civilianPiecePrefabs,
            BrushMode.Object => GetObjectPrefabs(currentStage?.mapIndex ?? 0),
            _ => null,
        };
    }

    private static string GetPrefabButtonLabel(int index, GameObject prefab)
    {
        return prefab != null ? $"{index}: {prefab.name}" : $"{index}: Missing";
    }

    private static string GetPaletteButtonLabel(BrushMode mode, int index, GameObject prefab)
    {
        if (prefab != null)
        {
            return $"{index}: {prefab.name}";
        }

        return mode == BrushMode.Enemy
            ? $"{index}: Reserved"
            : $"{index}: Missing";
    }

    private bool DrawPrefabDropdown(string key, string title, GameObject[] prefabs, int currentIndex, out int selectedIndex)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            selectedIndex = currentIndex;
            GUILayout.Label($"{title}: No prefabs found", GUI.skin.box);
            return false;
        }

        string[] labels = new string[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            labels[i] = GetPrefabButtonLabel(i, prefabs[i]);
        }

        return DrawStringDropdown(key, title, labels, currentIndex, out selectedIndex);
    }

    private bool DrawStringDropdown(string key, string title, string[] options, int currentIndex, out int selectedIndex)
    {
        selectedIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, options.Length - 1));
        string currentLabel = options.Length > 0 ? options[selectedIndex] : "-";

        GUILayout.Label(title, GUI.skin.box);
        bool isOpen = dropdownStates.TryGetValue(key, out bool open) && open;
        if (GUILayout.Button(isOpen ? $"v {currentLabel}" : $"> {currentLabel}", GUILayout.Height(28f)))
        {
            dropdownStates[key] = !isOpen;
            return false;
        }

        if (!isOpen)
        {
            return false;
        }

        bool changed = false;
        GUILayout.BeginVertical(GUI.skin.box);
        for (int i = 0; i < options.Length; i++)
        {
            Color oldColor = GUI.backgroundColor;
            if (i == selectedIndex)
            {
                GUI.backgroundColor = Color.yellow;
            }

            if (GUILayout.Button(options[i], GUILayout.Height(26f)))
            {
                selectedIndex = i;
                dropdownStates[key] = false;
                changed = i != currentIndex;
            }

            GUI.backgroundColor = oldColor;
        }
        GUILayout.EndVertical();

        return changed;
    }

    private GameObject GetObjectPrefab(int mapIndex, int detailType)
    {
        GameObject[] objectPrefabs = GetObjectPrefabs(mapIndex);
        if (objectPrefabs == null || detailType < 0 || detailType >= objectPrefabs.Length)
        {
            return null;
        }

        return objectPrefabs[detailType];
    }

    private GameObject[] GetObjectPrefabs(int mapIndex)
    {
        Transform objectGroup = FindObjectGroup(mapIndex);
        if (objectGroup == null)
        {
            return Array.Empty<GameObject>();
        }

        GameObject[] objectPrefabs = new GameObject[objectGroup.childCount];
        for (int i = 0; i < objectGroup.childCount; i++)
        {
            objectPrefabs[i] = objectGroup.GetChild(i).gameObject;
        }

        return objectPrefabs;
    }

    private Transform FindObjectGroup(int mapIndex)
    {
        GameObject mapPrefab = GetPrefab(mapPrefabs, mapIndex);
        if (mapPrefab == null)
        {
            return null;
        }

        Queue<Transform> queue = new();
        queue.Enqueue(mapPrefab.transform);
        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (current.CompareTag("ObjectGroup"))
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return null;
    }

    private void LoadStage(string path)
    {
        CommitPendingInputs();
        if (!File.Exists(path))
        {
            return;
        }

        string json = File.ReadAllText(path);
        currentStage = JsonUtility.FromJson<StageData>(json) ?? new StageData();
        currentStagePath = path;
        selectedEntityIndex = -1;
        inputBuffers.Clear();
        EnsureStageArrays();
        RebuildPreview();
    }

    private void SaveCurrentStage()
    {
        if (currentStage == null || string.IsNullOrEmpty(currentStagePath))
        {
            return;
        }

        CommitPendingInputs();
        EnsureStageArrays();
        Directory.CreateDirectory(Path.GetDirectoryName(currentStagePath));
        File.WriteAllText(currentStagePath, JsonUtility.ToJson(currentStage, true));
    }

    private void RefreshStageList()
    {
        stagePaths.Clear();
        string stageDirectory = GetStageDirectory();
        Directory.CreateDirectory(stageDirectory);
        stagePaths.AddRange(Directory.GetFiles(stageDirectory, "*.json"));
        stagePaths.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshPrefabReferences()
    {
#if UNITY_EDITOR
        gridCellPrefab = LoadPrefabAtPath("Assets/03_Prefabs/Indicators/GridTiles/GridCell.prefab", gridCellPrefab);
        mapPrefabs = LoadPrefabFolder(mapPrefabFolder, mapPrefabs);
        enemyPiecePrefabs = NormalizeEnemyPrefabIndices(LoadPrefabFolder(enemyPrefabFolder, enemyPiecePrefabs));
        civilianPiecePrefabs = LoadPrefabFolder(civilianPrefabFolder, civilianPiecePrefabs);
#endif
    }

#if UNITY_EDITOR
    private static GameObject[] LoadPrefabFolder(string folderPath, GameObject[] fallbackPrefabs)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            return fallbackPrefabs ?? Array.Empty<GameObject>();
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        var prefabs = new List<GameObject>();
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        prefabs.Sort(ComparePrefabOrder);
        return prefabs.Count > 0 ? prefabs.ToArray() : fallbackPrefabs ?? Array.Empty<GameObject>();
    }

    private static GameObject LoadPrefabAtPath(string assetPath, GameObject fallbackPrefab)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return fallbackPrefab;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return prefab != null ? prefab : fallbackPrefab;
    }

    private static int ComparePrefabOrder(GameObject left, GameObject right)
    {
        int leftPriority = GetPrefabOrderPriority(left != null ? left.name : string.Empty);
        int rightPriority = GetPrefabOrderPriority(right != null ? right.name : string.Empty);
        int priorityComparison = leftPriority.CompareTo(rightPriority);
        return priorityComparison != 0
            ? priorityComparison
            : string.Compare(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPrefabOrderPriority(string prefabName)
    {
        if (prefabName.IndexOf("Brawler", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
        if (prefabName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        if (prefabName.IndexOf("Gunman", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        if (prefabName.IndexOf("Slasher", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        if (prefabName.IndexOf("Civilian_01", StringComparison.OrdinalIgnoreCase) >= 0) return 0;
        if (prefabName.IndexOf("Eliza", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        return 100;
    }

    private static GameObject[] NormalizeEnemyPrefabIndices(GameObject[] prefabs)
    {
        GameObject[] normalized = new GameObject[4];
        if (prefabs == null)
        {
            return normalized;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
            {
                continue;
            }

            string prefabName = prefab.name;
            if (prefabName.IndexOf("Brawler", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                normalized[0] = prefab;
            }
            else if (prefabName.IndexOf("Slasher", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                normalized[1] = prefab;
            }
            else if (prefabName.IndexOf("Gunman", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                normalized[2] = prefab;
            }
            else if (prefabName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                normalized[3] = prefab;
            }
        }

        return normalized;
    }
#endif

    private void EnsureStageArrays()
    {
        currentStage.allySlots ??= Array.Empty<AllySlotData>();
        currentStage.entities ??= Array.Empty<StageEntityData>();
        if (currentStage.boardSize <= 0)
        {
            currentStage.boardSize = 6;
        }
    }

    private void EnsureAllySlots()
    {
        EnsureStageArrays();
        List<AllySlotData> slots = new(currentStage.allySlots);
        EnsureAllySlot(slots, PieceType.Brawler);
        EnsureAllySlot(slots, PieceType.Slasher);
        EnsureAllySlot(slots, PieceType.Gunman);
        currentStage.allySlots = slots.ToArray();
    }

    private static void EnsureAllySlot(List<AllySlotData> slots, PieceType type)
    {
        int typeInt = (int)type;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].pieceType == typeInt)
            {
                return;
            }
        }

        slots.Add(new AllySlotData { pieceType = typeInt, count = 0 });
    }

    private bool DrawAllySlot(PieceType type)
    {
        int index = FindAllySlotIndex(type);
        if (index < 0)
        {
            return false;
        }

        if (!IntField($"ally.{type}", type.ToString(), currentStage.allySlots[index].count, out int count))
        {
            return false;
        }

        currentStage.allySlots[index].count = Mathf.Max(0, count);
        return true;
    }

    private int FindAllySlotIndex(PieceType type)
    {
        int typeInt = (int)type;
        for (int i = 0; i < currentStage.allySlots.Length; i++)
        {
            if (currentStage.allySlots[i] != null && currentStage.allySlots[i].pieceType == typeInt)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindEntityIndexAtCell(int cellIndex)
    {
        if (currentStage?.entities == null)
        {
            return -1;
        }

        for (int i = 0; i < currentStage.entities.Length; i++)
        {
            if (currentStage.entities[i] != null && currentStage.entities[i].cellIndex == cellIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private StageEntityData GetSelectedEntity()
    {
        return currentStage?.entities != null &&
               selectedEntityIndex >= 0 &&
               selectedEntityIndex < currentStage.entities.Length
            ? currentStage.entities[selectedEntityIndex]
            : null;
    }

    private void RemoveSelectedEntity()
    {
        if (selectedEntityIndex >= 0)
        {
            RemoveEntityAt(selectedEntityIndex);
        }
    }

    private void RemoveEntityAt(int entityIndex)
    {
        List<StageEntityData> entities = new(currentStage.entities);
        entities.RemoveAt(entityIndex);
        currentStage.entities = entities.ToArray();
        selectedEntityIndex = -1;
    }

    private Vector3 GetCellPosition(int cellIndex)
    {
        int boardSize = Mathf.Max(1, currentStage.boardSize);
        Vector2Int coord = StageGridIndexUtility.ToGridCoord(boardSize, Mathf.Clamp(cellIndex, 0, CellCount - 1));
        float half = cellSize / 2f;
        float originOffset = boardSize - 1;
        return new Vector3((coord.x * 2f - originOffset) * half, 0f, (coord.y * 2f - originOffset) * half);
    }

    private void ClearPreview()
    {
        hoveredGridCell = null;
        spawnedGridCells.Clear();
        if (currentMapInstance != null)
        {
            Destroy(currentMapInstance);
            currentMapInstance = null;
        }

        for (int i = 0; i < spawnedPreviewObjects.Count; i++)
        {
            if (spawnedPreviewObjects[i] != null)
            {
                Destroy(spawnedPreviewObjects[i]);
            }
        }
        spawnedPreviewObjects.Clear();
    }

    private void EnsurePreviewRoot()
    {
        if (previewRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("StageEditorPreviewRoot");
        root.transform.SetParent(transform, false);
        previewRoot = root.transform;
    }

    private string GetStageDirectory()
    {
        return Path.Combine(Application.streamingAssetsPath, stageFolder);
    }

    private int CellCount => currentStage == null ? 0 : Mathf.Max(1, currentStage.boardSize * currentStage.boardSize);
    private int MapCount => mapPrefabs == null ? 0 : mapPrefabs.Length;

    private bool IntField(string key, string label, int value, out int result)
    {
        result = value;
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(210f));
        string controlName = $"StageEditorField_{key}";
        GUI.SetNextControlName(controlName);
        string raw = GUILayout.TextField(GetInputBuffer(key, value.ToString()), GUILayout.Width(300f));
        inputBuffers[key] = raw;
        GUILayout.EndHorizontal();

        bool pressedEnter = IsEnterPressedOn(controlName);
        bool lostFocus = GUI.GetNameOfFocusedControl() != controlName && raw != value.ToString();
        if ((!pressedEnter && !lostFocus) || !int.TryParse(raw, out int parsed))
        {
            return false;
        }

        result = parsed;
        inputBuffers[key] = result.ToString();
        if (pressedEnter)
        {
            GUI.FocusControl(null);
        }
        return result != value;
    }

    private bool TextField(string key, string label, string value, out string result)
    {
        result = value ?? string.Empty;
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(130f));
        string controlName = $"StageEditorField_{key}";
        GUI.SetNextControlName(controlName);
        string raw = GUILayout.TextField(GetInputBuffer(key, result), GUILayout.Width(380f));
        inputBuffers[key] = raw;
        GUILayout.EndHorizontal();

        bool pressedEnter = IsEnterPressedOn(controlName);
        bool lostFocus = GUI.GetNameOfFocusedControl() != controlName && raw != result;
        if (!pressedEnter && !lostFocus)
        {
            return false;
        }

        result = raw;
        if (pressedEnter)
        {
            GUI.FocusControl(null);
        }
        return result != (value ?? string.Empty);
    }

    private string GetInputBuffer(string key, string fallback)
    {
        if (!inputBuffers.TryGetValue(key, out string buffer))
        {
            buffer = fallback;
            inputBuffers[key] = buffer;
        }

        return buffer;
    }

    private static bool IsEnterPressedOn(string controlName)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null ||
            currentEvent.type != EventType.KeyDown ||
            (currentEvent.keyCode != KeyCode.Return && currentEvent.keyCode != KeyCode.KeypadEnter) ||
            GUI.GetNameOfFocusedControl() != controlName)
        {
            return false;
        }

        currentEvent.Use();
        return true;
    }

    private void CommitPendingInputs()
    {
        if (currentStage == null)
        {
            return;
        }

        ApplyBufferedInt("stage.version", value => currentStage.version = value);
        ApplyBufferedInt("stage.boardSize", value => currentStage.boardSize = Mathf.Clamp(value, 1, 12));
        ApplyBufferedText("stage.mainMission", value => currentStage.mainMission = value);
        ApplyBufferedText("stage.subMission1", value => currentStage.subMission1 = value);
        ApplyBufferedText("stage.subMission2", value => currentStage.subMission2 = value);
        ApplyBufferedInt("save.newStageNumber", value => newStageNumber = Mathf.Max(1, value));

        EnsureAllySlots();
        ApplyBufferedInt($"ally.{PieceType.Brawler}", value => SetAllySlotCount(PieceType.Brawler, Mathf.Max(0, value)));
        ApplyBufferedInt($"ally.{PieceType.Slasher}", value => SetAllySlotCount(PieceType.Slasher, Mathf.Max(0, value)));
        ApplyBufferedInt($"ally.{PieceType.Gunman}", value => SetAllySlotCount(PieceType.Gunman, Mathf.Max(0, value)));

        StageEntityData selected = GetSelectedEntity();
        if (selected == null)
        {
            return;
        }

        string keyPrefix = $"entity.{selectedEntityIndex}";
        ApplyBufferedInt($"{keyPrefix}.cellIndex", value => selected.cellIndex = Mathf.Clamp(value, 0, CellCount - 1));
    }

    private void ApplyBufferedInt(string key, Action<int> apply)
    {
        if (apply == null || !inputBuffers.TryGetValue(key, out string raw) || !int.TryParse(raw, out int parsed))
        {
            return;
        }

        apply(parsed);
        inputBuffers[key] = parsed.ToString();
    }

    private void ApplyBufferedText(string key, Action<string> apply)
    {
        if (apply == null || !inputBuffers.TryGetValue(key, out string raw))
        {
            return;
        }

        apply(raw);
    }

    private void SetAllySlotCount(PieceType type, int count)
    {
        int index = FindAllySlotIndex(type);
        if (index < 0)
        {
            return;
        }

        currentStage.allySlots[index].count = count;
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tabKey.wasPressedThisFrame && IsPlacementBrushMode(brushMode) && !IsTextFieldFocused())
        {
            brushFacing = (brushFacing + 1) % 4;
            RefreshGridCellVisuals();
        }
    }

    private static bool IsPlacementBrushMode(BrushMode mode)
    {
        return mode == BrushMode.Enemy || mode == BrushMode.Civilian || mode == BrushMode.Object;
    }

    private static bool IsTextFieldFocused()
    {
        return GUI.GetNameOfFocusedControl().StartsWith("StageEditorField_", StringComparison.Ordinal);
    }

    private void HandleWorldGridInteraction()
    {
        if (currentStage == null)
        {
            return;
        }

        if (IsPointerOverPanel())
        {
            UpdateHoveredGridCell(null);
            return;
        }

        GridCell hitGridCell = RaycastGridCell();
        UpdateHoveredGridCell(hitGridCell);

        if (hitGridCell == null)
        {
            return;
        }

        if (IsPointerButtonPressedThisFrame(true))
        {
            HandleCellClick(hitGridCell.CellIndex, FindEntityIndexAtCell(hitGridCell.CellIndex));
        }
        else if (IsPointerButtonPressedThisFrame(false) && IsPlacementBrushMode(brushMode))
        {
            brushMode = BrushMode.Select;
            RefreshGridCellVisuals();
        }
    }

    private GridCell RaycastGridCell()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return null;
        }

        Vector2 pointerScreenPosition = GetPointerScreenPosition();
        Ray ray = mainCamera.ScreenPointToRay(pointerScreenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int index = 0; index < hits.Length; index++)
        {
            GridCell gridCell = hits[index].collider.GetComponentInParent<GridCell>();
            if (gridCell != null && spawnedGridCells.Contains(gridCell))
            {
                return gridCell;
            }
        }

        return null;
    }

    private void UpdateHoveredGridCell(GridCell nextGridCell)
    {
        if (ReferenceEquals(hoveredGridCell, nextGridCell))
        {
            return;
        }

        hoveredGridCell = nextGridCell;
        RefreshGridCellVisuals();
    }

    private void RefreshGridCellVisuals()
    {
        for (int index = 0; index < spawnedGridCells.Count; index++)
        {
            GridCell gridCell = spawnedGridCells[index];
            if (gridCell == null)
            {
                continue;
            }

            int entityIndex = FindEntityIndexAtCell(gridCell.CellIndex);
            bool isHovered = ReferenceEquals(gridCell, hoveredGridCell);
            bool isSelected = entityIndex >= 0 && entityIndex == selectedEntityIndex;

            if (isHovered)
            {
                if (IsPlacementBrushMode(brushMode))
                {
                    gridCell.ShowMoveHighlight(true, Quaternion.Euler(0f, brushFacing * 90f, 0f));
                }
                else
                {
                    gridCell.ShowRangeHighlight(true);
                }

                continue;
            }

            if (isSelected)
            {
                gridCell.ShowRangeHighlight(true);
            }
            else if (entityIndex >= 0)
            {
                gridCell.ShowPlacementAvailability(false);
            }
            else
            {
                gridCell.ResetVisual();
            }
        }
    }

    private static bool IsPointerOverPanel()
    {
        Vector2 pointerPosition = GetPointerScreenPosition();
        return pointerPosition.x >= PanelLeft && pointerPosition.x <= PanelLeft + PanelWidth;
    }

    private static Vector2 GetPointerScreenPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }

        return Vector2.zero;
    }

    private static bool IsPointerButtonPressedThisFrame(bool primaryButton)
    {
        if (Mouse.current != null)
        {
            return primaryButton
                ? Mouse.current.leftButton.wasPressedThisFrame
                : Mouse.current.rightButton.wasPressedThisFrame;
        }

        if (primaryButton && Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        }

        return false;
    }

    private static string GetEntityShortLabel(StageEntityData entity)
    {
        if (entity == null)
        {
            return "?";
        }

        string prefix = entity.entityKind switch
        {
            0 => "E",
            1 => "C",
            2 => "O",
            _ => "?",
        };
        return $"{prefix}{entity.detailType}/{entity.facing}";
    }

    private static GameObject GetPrefab(GameObject[] prefabs, int index)
    {
        return prefabs != null && index >= 0 && index < prefabs.Length ? prefabs[index] : null;
    }

    private static string GetName(GameObject[] prefabs, int index)
    {
        GameObject prefab = GetPrefab(prefabs, index);
        return prefab != null ? prefab.name : "-";
    }

    private string GetObjectPrefabName(int mapIndex, int detailType)
    {
        GameObject prefab = GetObjectPrefab(mapIndex, detailType);
        return prefab != null ? prefab.name : "-";
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null || layer < 0)
        {
            return;
        }

        target.layer = layer;
        Transform targetTransform = target.transform;
        for (int i = 0; i < targetTransform.childCount; i++)
        {
            SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
        }
    }
}
