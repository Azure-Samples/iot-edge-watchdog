namespace GWManagementFunctions
{
    public class MessageExpiredException : System.Exception
    {
        private static readonly string DefaultMessage =
            "Message is older than the expiration window.";
        public MessageExpiredException() : base(DefaultMessage) { }
        public MessageExpiredException(string message) : base(message) { }
        public MessageExpiredException(string message, System.Exception innerException)
        : base(message, innerException) { }
    }
}
