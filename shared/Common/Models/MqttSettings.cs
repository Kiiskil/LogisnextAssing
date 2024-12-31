namespace Common.Models;

public class MqttSettings
{
    public string BrokerAddress { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseTls { get; set; } = false;
    public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
} 