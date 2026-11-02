namespace Encore;

public class DeviceNotReadyException(string reason) : Exception(reason) { }