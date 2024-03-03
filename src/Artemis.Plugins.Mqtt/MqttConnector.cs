using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace Artemis.Plugins.Mqtt;

/// <summary>
///     MQTT client that has a connection to a single server.
/// </summary>
public sealed class MqttConnector : IDisposable
{
    private static readonly MqttFactory ClientFactory = new();
    private readonly IManagedMqttClient _client;

    public MqttConnector()
    {
        _client = ClientFactory.CreateManagedMqttClient();
        _client.ApplicationMessageReceivedAsync += OnClientMessageReceived;
        _client.ConnectedAsync += OnClientConnected;
        _client.DisconnectedAsync += OnClientDisconnected;
    }

    /// <summary>
    ///     The ID of the server this connector is connected to.
    ///     <para />
    ///     Based on the 'ServerId' property from the settings object passed to <see cref="Start(MqttConnectionSettings,IEnumerable{string})" />.
    /// </summary>
    public Guid ServerId { get; private set; }

    /// <summary>
    ///     Whether or not this connector is currently connected to a server.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool IsConnected { get; private set; }

    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    ///     Event that fires when this connector receives a message from the server it is connected to.
    /// </summary>
    public event EventHandler<MqttApplicationMessageReceivedEventArgs>? MessageReceived;

    public event EventHandler<MqttClientConnectedEventArgs>? Connected;
    public event EventHandler<MqttClientDisconnectedEventArgs>? Disconnected;

    /// <summary>
    ///     Sets up and starts listening with the MQTT client behind this connector.
    ///     <para />
    ///     If the client is already connected, it will first be stopped then restarted with the new settings.
    /// </summary>
    /// <param name="settings">Settings to use to initialise the client.</param>
    /// <param name="topics">Topics to subscribe to.</param>
    public async Task Start(MqttConnectionSettings settings, IEnumerable<string> topics)
    {
        // Setup client options
        ServerId = settings.ServerId;

        var clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(settings.ServerUrl, settings.ServerPort)
            .WithClientId(settings.ClientId);

        if (!string.IsNullOrEmpty(settings.Username))
            clientOptions.WithCredentials(settings.Username, settings.Password);


        // Build options and start client
        var managedClientOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions.Build())
            .Build();

        await _client.StopAsync();
        await _client.StartAsync(managedClientOptions);
        await Task.WhenAll(
            topics.Select(topic => _client.SubscribeAsync(topic))
        );
        await _client.SubscribeAsync("#");
    }

    /// <summary>
    ///     Disconnects the connector from the server it is connected to.
    /// </summary>
    public Task Stop()
    {
        return _client.StopAsync();
    }

    private Task OnClientMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, e);
        return Task.CompletedTask;
    }

    private Task OnClientConnected(MqttClientConnectedEventArgs e)
    {
        IsConnected = true;
        Connected?.Invoke(this, e);
        return Task.CompletedTask;
    }

    private Task OnClientDisconnected(MqttClientDisconnectedEventArgs e)
    {
        IsConnected = false;
        Disconnected?.Invoke(this, e);
        return Task.CompletedTask;
    }
}