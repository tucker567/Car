using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Score))]
[CanEditMultipleObjects]
public class ScoreEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var score = (Score)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("High Score Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset High Score"))
        {
            Score.ResetHighScore();
            Score.highScore = 0;
            // Update labels for all selected Score components
            foreach (var obj in targets)
            {
                var s = obj as Score;
                if (s != null && s.highScoreText != null)
                {
                    s.highScoreText.text = "High Score: 0";
                    EditorUtility.SetDirty(s.highScoreText);
                }
            }
            EditorUtility.DisplayDialog("High Score", "High score has been reset.", "OK");
        }

        if (GUILayout.Button("Refresh High Score"))
        {
            Score.highScore = Score.GetHighScore();
            foreach (var obj in targets)
            {
                var s = obj as Score;
                if (s != null && s.highScoreText != null)
                {
                    s.highScoreText.text = "High Score: " + Score.highScore.ToString();
                    EditorUtility.SetDirty(s.highScoreText);
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}
