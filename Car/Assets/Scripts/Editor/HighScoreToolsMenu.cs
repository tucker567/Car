using UnityEditor;
using UnityEngine;

public static class HighScoreToolsMenu
{
    [MenuItem("Tools/High Score/Reset")] 
    public static void ResetHighScore()
    {
        Score.ResetHighScore();
        Score.highScore = 0;
        // Update any Score labels in the current scene(s)
        var scores = Object.FindObjectsOfType<Score>(true);
        foreach (var s in scores)
        {
            if (s.highScoreText != null)
            {
                s.highScoreText.text = "High Score: 0";
                EditorUtility.SetDirty(s.highScoreText);
            }
        }
        EditorUtility.DisplayDialog("High Score", "High score has been reset.", "OK");
    }

    [MenuItem("Tools/High Score/Refresh Labels")] 
    public static void RefreshHighScoreLabels()
    {
        Score.highScore = Score.GetHighScore();
        var scores = Object.FindObjectsOfType<Score>(true);
        foreach (var s in scores)
        {
            if (s.highScoreText != null)
            {
                s.highScoreText.text = "High Score: " + Score.highScore.ToString();
                EditorUtility.SetDirty(s.highScoreText);
            }
        }
    }
}
