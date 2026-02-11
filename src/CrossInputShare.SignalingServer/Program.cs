using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CrossInputShare.Core.Models;
using CrossInputShare.Network.Models;
using CrossInputShare.Security.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrossInputShare.SignalingServer
{
    public class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Configure services
            builder.Services.AddLogging(configure => configure.AddConsole());
            builder.Services.AddSingleton<ServerSessionManager>();
            builder.Services.Configure<ServerSessionManagerOptions>(options =>
            {
                options.SessionTimeout = TimeSpan.FromMinutes(30);
                options.MaxSessionsPerDevice = 5;
                options.CleanupInterval = TimeSpan.FromMinutes(5);
            });

            var app = builder.Build();
            
            // Enable WebSockets
            app.UseWebSockets();
            
            // Simple WebSocket endpoint for signaling
            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await HandleWebSocketConnection(webSocket, context.RequestServices);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            });

            // Health check endpoint
            app.MapGet("/health", () => "OK");

            Console.WriteLine("Signaling server starting on http://localhost:5000");
            Console.WriteLine("WebSocket endpoint: ws://localhost:5000/ws");
            Console.WriteLine("Health check: http://localhost:5000/health");
            
            await app.RunAsync("http://localhost:5000");
        }

        private static async Task HandleWebSocketConnection(WebSocket webSocket, IServiceProvider services)
        {
            var buffer = new byte[1024 * 4]; // 4KB buffer
            var sessionManager = services.GetRequiredService<ServerSessionManager>();
            var logger = services.GetRequiredService<ILogger<Program>>();
            
            var connectionId = Guid.NewGuid();
            logger.LogInformation("WebSocket connection established: {ConnectionId}", connectionId);

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessage(message, webSocket, sessionManager, logger, connectionId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        logger.LogInformation("WebSocket connection closed: {ConnectionId}", connectionId);
                        break;
                    }
                }
            }
            catch (WebSocketException ex)
            {
                logger.LogError(ex, "WebSocket error for connection {ConnectionId}", connectionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing WebSocket connection {ConnectionId}", connectionId);
            }
            finally
            {
                logger.LogInformation("WebSocket connection terminated: {ConnectionId}", connectionId);
            }
        }

        private static async Task ProcessMessage(string message, WebSocket webSocket, ServerSessionManager sessionManager, ILogger logger, Guid connectionId)
        {
            try
            {
                logger.LogDebug("Received message from {ConnectionId}: {Message}", connectionId, message);

                // Parse the message
                var jsonDoc = JsonDocument.Parse(message);
                if (!jsonDoc.RootElement.TryGetProperty("type", out var typeElement))
                {
                    await SendError(webSocket, "invalid-message", "Missing 'type' field");
                    return;
                }

                var messageType = typeElement.GetString();
                switch (messageType)
                {
                    case "create-session":
                        await HandleCreateSession(message, webSocket, sessionManager, logger, connectionId);
                        break;
                    case "join-session":
                        await HandleJoinSession(message, webSocket, sessionManager, logger, connectionId);
                        break;
                    case "ping":
                        await HandlePing(message, webSocket, logger, connectionId);
                        break;
                    default:
                        logger.LogWarning("Unknown message type: {MessageType} from {ConnectionId}", messageType, connectionId);
                        await SendError(webSocket, "unknown-message-type", $"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "JSON parsing error from {ConnectionId}", connectionId);
                await SendError(webSocket, "invalid-json", "Invalid JSON format");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message from {ConnectionId}", connectionId);
                await SendError(webSocket, "internal-error", "Internal server error");
            }
        }

        private static async Task HandleCreateSession(string message, WebSocket webSocket, ServerSessionManager sessionManager, ILogger logger, Guid connectionId)
        {
            var request = JsonSerializer.Deserialize<CreateSessionRequest>(message, JsonOptions);
            if (request == null)
            {
                await SendError(webSocket, "invalid-request", "Invalid create session request");
                return;
            }

            logger.LogInformation("Create session request from device {DeviceId} ({DeviceName})", 
                request.DeviceInfo.Id, request.DeviceInfo.Name);

            try
            {
                // Generate a session code
                var sessionCode = SessionCode.Generate();
                
                // Create session info
                var sessionInfo = new SessionInfo(
                    id: Guid.NewGuid(),
                    code: new SessionCode(sessionCode.ToString()),
                    serverDevice: request.DeviceInfo,
                    maxClients: 4,
                    timeout: TimeSpan.FromMinutes(30)
                );

                // Update features if provided
                if (request.Features != null)
                {
                    sessionInfo.UpdateFeatures(request.Features);
                }

                // Register the session with the session manager
                sessionManager.RegisterSession(sessionInfo);

                // Send response
                var response = new CreateSessionResponse(
                    sessionCode: sessionCode.ToString(),
                    sessionInfo: sessionInfo,
                    success: true
                );

                var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                await SendMessage(webSocket, responseJson);
                
                logger.LogInformation("Session created: {SessionCode} for device {DeviceId}", sessionCode, request.DeviceInfo.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating session for device {DeviceId}", request.DeviceInfo.Id);
                await SendError(webSocket, "session-creation-failed", $"Failed to create session: {ex.Message}");
            }
        }

        private static async Task HandleJoinSession(string message, WebSocket webSocket, ServerSessionManager sessionManager, ILogger logger, Guid connectionId)
        {
            var request = JsonSerializer.Deserialize<JoinSessionRequest>(message, JsonOptions);
            if (request == null)
            {
                await SendError(webSocket, "invalid-request", "Invalid join session request");
                return;
            }

            logger.LogInformation("Join session request from device {DeviceId} for session {SessionCode}", 
                request.DeviceInfo.Id, request.SessionCode);

            try
            {
                // Validate session code
                if (!SessionCode.IsValid(request.SessionCode))
                {
                    var errorResponse = new JoinSessionResponse(
                        success: false,
                        error: "Invalid session code format"
                    );
                    var errorJson = JsonSerializer.Serialize(errorResponse, JsonOptions);
                    await SendMessage(webSocket, errorJson);
                    return;
                }

                // TODO: In a real implementation, we would:
                // 1. Look up the session by code
                // 2. Validate the session exists and is joinable
                // 3. Add the device to the session
                // 4. Notify all connected devices about the new participant
                // 5. Return session info with all participants

                // For now, return a mock successful response
                var sessionCode = new SessionCode(request.SessionCode);
                var mockSessionInfo = new SessionInfo(
                    id: Guid.NewGuid(),
                    code: sessionCode,
                    serverDevice: request.DeviceInfo, // In reality, this would be the server device
                    maxClients: 4
                );

                var response = new JoinSessionResponse(
                    success: true,
                    sessionInfo: mockSessionInfo
                );

                var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                await SendMessage(webSocket, responseJson);
                
                logger.LogInformation("Device {DeviceId} joined session {SessionCode}", request.DeviceInfo.Id, request.SessionCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error joining session for device {DeviceId}", request.DeviceInfo.Id);
                await SendError(webSocket, "join-failed", $"Failed to join session: {ex.Message}");
            }
        }

        private static async Task HandlePing(string message, WebSocket webSocket, ILogger logger, Guid connectionId)
        {
            try
            {
                var ping = JsonSerializer.Deserialize<PingMessage>(message, JsonOptions);
                if (ping == null)
                {
                    return;
                }

                var pong = new PongMessage(ping.Sequence, DateTime.UtcNow);
                var pongJson = JsonSerializer.Serialize(pong, JsonOptions);
                await SendMessage(webSocket, pongJson);
                
                logger.LogDebug("Ping-pong: {Sequence} from {ConnectionId}", ping.Sequence, connectionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing ping from {ConnectionId}", connectionId);
            }
        }

        private static async Task SendMessage(WebSocket webSocket, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }

        private static async Task SendError(WebSocket webSocket, string code, string message)
        {
            var error = new ErrorMessage(code, message);
            var errorJson = JsonSerializer.Serialize(error, JsonOptions);
            await SendMessage(webSocket, errorJson);
        }
    }
}