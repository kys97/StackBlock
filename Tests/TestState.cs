using System.Reflection;

// Fixture setup only; gameplay callers cannot write the manager's counters and flags.
public static class TestState
{
    public static void Set(GameManager owner, string field, object value) => typeof(GameManager)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

    public static void NotifyCompletion(GameManager owner) => typeof(GameManager)
        .GetMethod("NotifyPieceCompleted", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
}
