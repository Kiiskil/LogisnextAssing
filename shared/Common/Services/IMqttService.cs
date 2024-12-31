namespace Common.Services;

public interface IMqttPublisherService
{
    Task ConnectAsync();
    Task PublishAsync(string topic, string message);
    Task DisconnectAsync();
}

public interface IMqttSubscriberService
{
    Task ConnectAsync();
    Task SubscribeAsync(string topic, Action<string> messageHandler);
    Task DisconnectAsync();
} 