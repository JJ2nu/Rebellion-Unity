using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ResultDialogPanel의 캐릭터·대사창 연출을 복사해 스테이지 위에 겹쳐 쓰는 작은 튜토리얼 대사창 Prefab을 만든다.
/// 대사창 스프라이트, 폰트, 진행 버튼, SFX는 원본을 쓰고, 캐릭터는 엘리자 idle 시트(idle.png) 루프로 바꾼다.
/// 생성 후 배치는 Prefab에서 직접 다듬어도 되며, 다시 실행하면 원본 기준으로 덮어쓴다.
/// </summary>
public static class TutorialDialoguePanelBuilder
{
    private const string SourcePrefabPath = "Assets/03_Prefabs/UI/InGame/ResultDialogPanel.prefab";
    private const string OutputFolder = "Assets/Resources/UI";
    private const string OutputPrefabPath = OutputFolder + "/TutorialDialoguePanel.prefab";

    // 엘리자 idle 시트. 원본 idle.png(1000x1500 프레임 13장 가로 배열)는 프레임마다 몸 전체가 좌우로 최대 7px 밀려 있어
    // 다리 중심을 기준으로 프레임을 서브픽셀 정렬한 뒤, 다리가 화면 밖으로 나가므로 상반신(머리~총구 아래)만 잘라
    // 604x656으로 줄이고 5열 격자(3020x1968)로 묶었다. 원본 대비 텍스처 메모리가 약 1/3.3이다.
    private const string IdleSheetPath = "Assets/04_Images/UI/new/idle_packed.png";
    private const int IdleFrameWidth = 604;
    private const int IdleFrameHeight = 656;
    private const int IdleColumns = 5;
    private const int IdleFrameCount = 13;
    private const int IdleMaxTextureSize = 4096;
    private const float IdleFramesPerSecond = 12f;

    // 대사 글자 시작 x. 일반 대사는 x=1780까지, 슬롯 조작 안내(대사창이 슬롯 뒤)는 슬롯 왼쪽 끝 x=1200 안쪽까지 쓴다.
    private const float TextStartX = 520f;
    private const float FullTextWidth = 1260f;
    private const float CompactTextWidth = 680f;

    [MenuItem("Rebellion/Tutorial/Build Tutorial Dialogue Panel")]
    public static void Build()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
        {
            Debug.LogError($"[TutorialDialoguePanelBuilder] Source prefab not found: {SourcePrefabPath}");
            return;
        }

        GameObject panel = Object.Instantiate(source);
        panel.name = "TutorialDialoguePanel";

        try
        {
            ResultDialogView resultView = panel.GetComponent<ResultDialogView>();
            AudioClip advanceClip = ReadAdvanceClip(resultView);

            // Controller가 View를 RequireComponent로 요구하므로 Controller부터 지운다.
            Object.DestroyImmediate(panel.GetComponent<ResultDialogController>());
            Object.DestroyImmediate(resultView);

            DestroyChild(panel.transform, "DarkPanel");
            DestroyChild(panel.transform, "Btn_ConfirmCommand");
            DestroyChild(panel.transform, "Btn_Reconsider");

            RectTransform character = FindRect(panel.transform, "Img_Char");
            RectTransform textbox = FindRect(panel.transform, "Img_Textbox");
            RectTransform speaker = FindRect(panel.transform, "Txt_Speaker");
            RectTransform dialogue = FindRect(panel.transform, "Txt_Dialogue");
            RectTransform advance = FindRect(panel.transform, "Btn_Textbox");

            // 기준 해상도 1920x1080. 대사창 배경은 반투명이라 전체 너비로 깔고 높이만 줄인다.
            // 일반 대사 중에는 대사창이 대원 슬롯 앞에 오므로 글자를 x=1780까지 쓴다.
            // 슬롯 조작 안내 중에는 Director가 대사창을 슬롯 뒤로 내리고 View가 글자 폭을 줄인다.
            textbox.anchorMin = new Vector2(0f, 0f);
            textbox.anchorMax = new Vector2(1f, 0f);
            textbox.pivot = new Vector2(0.5f, 0f);
            textbox.anchoredPosition = Vector2.zero;
            textbox.sizeDelta = new Vector2(0f, 250f);

            // 엘리자는 대사창 앞에 그려지도록 대사창 바로 뒤 형제로 옮긴다.
            // 상반신 프레임을 크게 띄우고 잘린 아랫단은 화면 밑으로 40px 내려 보이지 않게 한다.
            // 오른쪽으로 뻗은 총구가 x=490 근처에서 끝나므로 대사 글자는 x=520부터 시작한다.
            Place(character, new Vector2(0f, 0f), new Vector2(-20f, -40f), new Vector2(516f, 560f));
            character.SetSiblingIndex(textbox.GetSiblingIndex() + 1);
            ApplyIdleCharacter(character);

            Place(speaker, new Vector2(0f, 0.5f), new Vector2(TextStartX, 200f), new Vector2(520f, 52f));
            Place(dialogue, new Vector2(0f, 1f), new Vector2(TextStartX, 140f), new Vector2(FullTextWidth, 124f));

            // 진행 삼각형은 대사창 오른쪽 끝에 붙인다. 대사를 넘길 수 있는 동안(대사창이 슬롯 앞)에만 보인다.
            advance.anchorMin = advance.anchorMax = new Vector2(1f, 0f);
            advance.pivot = new Vector2(0.5f, 0.5f);
            advance.anchoredPosition = new Vector2(-70f, 40f);
            advance.sizeDelta = new Vector2(38f, 28f);

            TMP_Text speakerText = speaker.GetComponent<TMP_Text>();
            speakerText.fontSize = 38f;

            TMP_Text dialogueText = dialogue.GetComponent<TMP_Text>();
            dialogueText.fontSize = 30f;
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.raycastTarget = false;
            speakerText.raycastTarget = false;

            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }

            TutorialDialogueView view = panel.AddComponent<TutorialDialogueView>();
            SerializedObject serializedView = new(view);
            serializedView.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serializedView.FindProperty("characterRoot").objectReferenceValue = character.gameObject;
            // 표정별 Animator 대신 idle 루프를 쓰므로 표정 전환은 하지 않는다.
            serializedView.FindProperty("characterAnimator").objectReferenceValue = null;
            serializedView.FindProperty("speakerText").objectReferenceValue = speakerText;
            serializedView.FindProperty("dialogueText").objectReferenceValue = dialogueText;
            serializedView.FindProperty("textboxButton").objectReferenceValue = textbox.GetComponent<Button>();
            serializedView.FindProperty("advanceButton").objectReferenceValue = advance.GetComponent<Button>();
            serializedView.FindProperty("advanceAudioSource").objectReferenceValue = panel.GetComponent<AudioSource>();
            serializedView.FindProperty("advanceClip").objectReferenceValue = advanceClip;
            serializedView.FindProperty("fullTextWidth").floatValue = FullTextWidth;
            serializedView.FindProperty("compactTextWidth").floatValue = CompactTextWidth;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            // Director가 생성 직후 표시 상태를 직접 제어하므로 Prefab 루트는 활성 상태로 저장한다.
            panel.SetActive(true);

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
                AssetDatabase.Refresh();
            }

            PrefabUtility.SaveAsPrefabAsset(panel, OutputPrefabPath, out bool success);
            if (success)
            {
                Debug.Log($"[TutorialDialoguePanelBuilder] Saved {OutputPrefabPath}");
            }
            else
            {
                Debug.LogError($"[TutorialDialoguePanelBuilder] Failed to save {OutputPrefabPath}");
            }
        }
        finally
        {
            Object.DestroyImmediate(panel);
        }
    }

    private static void ApplyIdleCharacter(RectTransform character)
    {
        Sprite[] frames = LoadIdleFrames();
        if (frames.Length == 0)
        {
            Debug.LogWarning($"[TutorialDialoguePanelBuilder] No idle frames in {IdleSheetPath}. Keeping the original character animator.");
            return;
        }

        Animator animator = character.GetComponent<Animator>();
        if (animator != null)
        {
            Object.DestroyImmediate(animator);
        }

        Image image = character.GetComponent<Image>();
        image.sprite = frames[0];
        image.color = Color.white;
        image.preserveAspect = true;

        ImageFrameLoop loop = character.gameObject.AddComponent<ImageFrameLoop>();
        SerializedObject serializedLoop = new(loop);
        SerializedProperty framesProperty = serializedLoop.FindProperty("frames");
        framesProperty.arraySize = frames.Length;
        for (int index = 0; index < frames.Length; index++)
        {
            framesProperty.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
        }

        serializedLoop.FindProperty("framesPerSecond").floatValue = IdleFramesPerSecond;
        serializedLoop.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// idle 시트를 Sprite(Multiple)로 가져오고 프레임 크기 격자로 잘라 왼쪽부터 순서대로 돌려준다.
    /// </summary>
    private static Sprite[] LoadIdleFrames()
    {
        TextureImporter importer = AssetImporter.GetAtPath(IdleSheetPath) as TextureImporter;
        if (importer == null)
        {
            return System.Array.Empty<Sprite>();
        }

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int frameCount = IdleFrameCount;

        bool needsReimport = importer.textureType != TextureImporterType.Sprite ||
                             importer.spriteImportMode != SpriteImportMode.Multiple ||
                             importer.maxTextureSize != IdleMaxTextureSize ||
                             importer.mipmapEnabled ||
#pragma warning disable CS0618 // 2D Sprite 패키지 없이 격자 분할을 하기 위해 spritesheet API를 쓴다.
                             importer.spritesheet == null ||
                             importer.spritesheet.Length != frameCount ||
                             !HasFrameSize(importer.spritesheet);
#pragma warning restore CS0618

        if (needsReimport && frameCount > 0)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            // 시트(2560x2232)를 줄이지 않고 담는 최소 크기다. 가로·세로가 4의 배수라 블록 압축이 그대로 적용된다.
            importer.maxTextureSize = IdleMaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            List<SpriteMetaData> sheet = new();
            for (int index = 0; index < frameCount; index++)
            {
                sheet.Add(new SpriteMetaData
                {
                    name = $"idle_{index:00}",
                    // 격자는 왼쪽 위부터 채웠고 Sprite rect는 왼쪽 아래 기준이라 행을 위에서부터 뒤집어 계산한다.
                    rect = new Rect(
                        (index % IdleColumns) * IdleFrameWidth,
                        height - (index / IdleColumns + 1) * IdleFrameHeight,
                        IdleFrameWidth,
                        IdleFrameHeight),
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                });
            }

#pragma warning disable CS0618
            importer.spritesheet = sheet.ToArray();
#pragma warning restore CS0618
            importer.SaveAndReimport();
        }

        List<Sprite> frames = new();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(IdleSheetPath))
        {
            if (asset is Sprite sprite)
            {
                frames.Add(sprite);
            }
        }

        // 이름(idle_00 ~ idle_12) 순서가 재생 순서다.
        frames.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return frames.ToArray();
    }

    // 시트를 새 프레임 크기로 다시 만들었을 때 기존 분할 정보가 남아 있으면 다시 자르도록 확인한다.
    private static bool HasFrameSize(SpriteMetaData[] sheet)
    {
        foreach (SpriteMetaData frame in sheet)
        {
            if (!Mathf.Approximately(frame.rect.width, IdleFrameWidth) ||
                !Mathf.Approximately(frame.rect.height, IdleFrameHeight))
            {
                return false;
            }
        }

        return true;
    }

    private static AudioClip ReadAdvanceClip(ResultDialogView resultView)
    {
        if (resultView == null)
        {
            return null;
        }

        SerializedObject serializedResultView = new(resultView);
        return serializedResultView.FindProperty("advanceClip")?.objectReferenceValue as AudioClip;
    }

    private static void DestroyChild(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static RectTransform FindRect(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child == null)
        {
            throw new System.InvalidOperationException($"ResultDialogPanel has no '{childName}' child.");
        }

        return (RectTransform)child;
    }

    private static void Place(RectTransform rect, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
