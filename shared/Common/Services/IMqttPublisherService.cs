namespace Common.Services;

public interface IMqttPublisherService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task PublishAsync(string topic, string message);
} 