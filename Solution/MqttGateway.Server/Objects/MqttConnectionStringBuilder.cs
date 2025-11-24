using System.Data.Common;

namespace MqttGateway.Server.Objects;

public class MqttConnectionStringBuilder : DbConnectionStringBuilder
{
    public string ClientId
    {
        get => TryGetValue("ClientId", out var value) ? (string)value : string.Empty;
        set => this["ClientId"] = value;
    }

    public string Server
    {
        get => TryGetValue("Server", out var value) ? (string)value : string.Empty;
        set => this["Server"] = value;
    }

    public int Port
    {
        get => TryGetValue("Port", out var value) ? Convert.ToInt32(value) : 1883;
        set => this["Port"] = value;
    }

    public string? User
    {
        get => TryGetValue("User", out var value) ? (string)value : null;
        set => this["User"] = value;
    }

    public string? Password
    {
        get => TryGetValue("Password", out var value) ? (string)value : null;
        set => this["Password"] = value;
    }

    public bool? TrustedConnection
    {
        get => TryGetValue("TrustedConnection", out var value) ? Convert.ToBoolean(value) : null;
        set => this["TrustedConnection"] = value;
    }

    public bool CleanSession
    {
        get => TryGetValue("CleanSession", out var value) && Convert.ToBoolean(value);
        set => this["CleanSession"] = value;
    }
}