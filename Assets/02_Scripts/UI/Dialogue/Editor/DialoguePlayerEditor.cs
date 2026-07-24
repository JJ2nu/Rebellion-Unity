using UnityEditor;
using UnityEngine;

// Adds preview controls to the DialoguePlayer Inspector.
[CustomEditor(typeof(DialoguePlayer))]
public sealed class DialoguePlayerEditor : Editor
{
    #region Inspector

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();

        DialoguePlayer player = (DialoguePlayer)target;

        if (changed && player.PreviewInEditMode)
        {
            player.PlayPreviewDialogue();
            EditorUtility.SetDirty(player);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Play Preview Dialogue"))
        {
            player.PlayPreviewDialogue();
            EditorUtility.SetDirty(player);
        }

        EditorGUILayout.HelpBox(
            $"Preview: {player.PreviewLevel} / {player.PreviewResult} / Line {player.CurrentIndex + 1} of {player.CurrentLineCount}",
            MessageType.None);
    }

    #endregion
}
