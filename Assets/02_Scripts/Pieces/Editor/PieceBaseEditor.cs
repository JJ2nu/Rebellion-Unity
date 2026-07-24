using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PieceBase), true)]
public sealed class PieceBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Impact VFX Test", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test the hit impact VFX.", MessageType.Info);
            return;
        }

        PieceBase piece = (PieceBase)target;
        bool canPlay = piece != null && StageManager.Instance != null;

        using (new EditorGUI.DisabledScope(!canPlay))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Slash"))
            {
                PlayImpact(piece, HitImpactAttackType.Slash);
            }

            if (GUILayout.Button("Blunt"))
            {
                PlayImpact(piece, HitImpactAttackType.Blunt);
            }

            if (GUILayout.Button("Projectile"))
            {
                PlayImpact(piece, HitImpactAttackType.Projectile);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (!canPlay)
        {
            EditorGUILayout.HelpBox("StageManager is not available in the current scene.", MessageType.Warning);
        }
    }

    private static void PlayImpact(PieceBase piece, HitImpactAttackType attackType)
    {
        Camera camera = Camera.main;
        Vector3 direction = camera != null ? -camera.transform.forward : Vector3.forward;
        Vector3 position = piece.transform.position + Vector3.up * 0.8f;

        StageManager.Instance.PlayHitImpact(position, direction, attackType);
        SceneView.RepaintAll();
    }
}
