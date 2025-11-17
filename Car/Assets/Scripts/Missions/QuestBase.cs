using System;
using UnityEngine;

public abstract class QuestBase : MonoBehaviour
{
    [Header("Quest")]
    [Tooltip("Display name shown in the quest picker.")]
    public string displayName = "Quest";

    public bool IsActive { get; protected set; }

    protected GameObject player;

    public event Action<string> OnStatus;       // status/progress text updates
    public event Action<bool> OnCompleted;      // completed(success)

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    public virtual void StartQuest(GameObject playerObj)
    {
        if (IsActive) return;
        player = playerObj != null ? playerObj : GameObject.FindGameObjectWithTag("playerCar");
        IsActive = true;
    }

    public virtual void CancelQuest()
    {
        if (!IsActive) return;
        IsActive = false;
        player = null;
    }

    protected void Status(string msg)
    {
        OnStatus?.Invoke(msg);
    }

    protected void Complete(bool success)
    {
        if (!IsActive) return;
        IsActive = false;
        OnCompleted?.Invoke(success);
    }
}
