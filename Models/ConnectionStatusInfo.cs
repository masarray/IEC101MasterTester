namespace IEC101MasterTester.Models
{
    public sealed class ConnectionStatusInfo
    {
        public static readonly ConnectionStatusInfo Disconnected = new ConnectionStatusInfo("Disconnected", "Communication stopped.");
        public static readonly ConnectionStatusInfo Connecting = new ConnectionStatusInfo("Connecting", "Opening serial communication.");
        public static readonly ConnectionStatusInfo Connected = new ConnectionStatusInfo("Connected", "IEC-101 master active.");
        public static readonly ConnectionStatusInfo Disconnecting = new ConnectionStatusInfo("Disconnecting", "Stopping communication and cleaning up resources.");
        public static readonly ConnectionStatusInfo Faulted = new ConnectionStatusInfo("Faulted", "Communication error.");

        public ConnectionStatusInfo(string displayText, string detail)
        {
            DisplayText = displayText;
            Detail = detail;
        }

        public string DisplayText { get; }
        public string Detail { get; }
    }
}
