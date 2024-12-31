namespace Common.Services;

public interface IMqttSubscriberService
{
    Task ConnectAsync();
    Task SubscribeAsync(string topic, Action<string> messageHandler);
    Task DisconnectAsync();
} 