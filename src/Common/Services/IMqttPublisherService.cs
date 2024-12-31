namespace Common.Services;

public interface IMqttPublisherService
{
    Task ConnectAsync();
    Task PublishAsync(string topic, string message);
    Task DisconnectAsync();
} 