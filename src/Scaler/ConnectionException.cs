namespace miqm.sbss;

public class ConnectionException(string message, Exception exception) : Exception(message, exception);