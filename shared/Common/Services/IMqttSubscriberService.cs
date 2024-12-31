namespace Common.Services;

public interface IMqttSubscriberService
{
    bool IsConnected { get; }
    Task ConnectAsync();
    Task DisconnectAsync();
    Task SubscribeAsync(string topic);
    Task SubscribeAsync(string topic, Action<string> messageHandler);
    Task UnsubscribeAsync(string topic);
    event Func<string, string, Task> MessageReceived;
} 