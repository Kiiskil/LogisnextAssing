namespace Common.Services;

public interface IMqttPublisherService
{
    bool IsConnected { get; }
    Task ConnectAsync();
    Task DisconnectAsync();
    Task PublishAsync(string topic, string message);
} 