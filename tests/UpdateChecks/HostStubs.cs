// Only the host boundary is simulated; tests link the real service sources.
namespace BotPlusAutocadTool.Services
{
    internal static class PluginPaths
    {
        public static readonly string TestRoot = Path.Combine(Path.GetTempPath(), "BotPlus-SettingsChecks-" + Guid.NewGuid().ToString("N"));
        public static string UserDataDirectory { get { Directory.CreateDirectory(TestRoot); return TestRoot; } }
    }
}
namespace Autodesk.AutoCAD.ApplicationServices
{
    internal static class Application
    {
        public static event EventHandler Idle;
        public static int SubscriberCount => Idle?.GetInvocationList().Length ?? 0;
        public static readonly Documents DocumentManager = new Documents();
        public static void RaiseIdle() => Idle?.Invoke(null, EventArgs.Empty);
    }
    internal sealed class Documents
    {
        public bool ThrowOnRead;
        public Document MdiActiveDocument => ThrowOnRead ? throw new InvalidOperationException("Simulated host failure") : new Document();
    }
    internal sealed class Document { public string CommandInProgress => ""; }
}
namespace System.Windows
{
    internal enum MessageBoxButton { OK, YesNo }
    internal enum MessageBoxImage { Information, Warning }
    internal enum MessageBoxResult { No, Yes }
    internal static class MessageBox
    {
        public static int Shown;
        public static MessageBoxResult Show(string text, string title, MessageBoxButton button, MessageBoxImage icon)
        { Shown++; return MessageBoxResult.No; }
    }
}
