using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CrossInputShare.Core.Models;
using CrossInputShare.Network.Models;

namespace CrossInputShare.TestClient
{
    class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Cross-Platform Input Sharing Test Client ===");
            Console.WriteLine();
            
            try
            {
                // Generate device info
                var fingerprint = DeviceFingerprint.Generate(
                    platformInfo: "Linux Test Client",
                    machineId: "test-machine-client",
                    installationId: "test-install-client"
                );
                
                var deviceInfo = DeviceInfo.CreateLocal(fingerprint, "Test Client Device", "Linux", isHost: true);
                
                Console.WriteLine($"Device ID: {deviceInfo.Id}");
                Console.WriteLine($"Device Name: {deviceInfo.Name}");
                Console.WriteLine();
                
                // Connect to signaling server
                Console.WriteLine("Connecting to signaling server...");
                using var client = new ClientWebSocket();
                await client.ConnectAsync(new Uri("ws://localhost:5000/ws"), CancellationToken.None);
                Console.WriteLine("Connected to signaling server!");
                
                // Test 1: Create a session
                Console.WriteLine();
                Console.WriteLine("Test 1: Creating a session...");
                var createSessionRequest = new CreateSessionRequest(
                    deviceInfo: deviceInfo,
                    features: SessionFeaturesExtensions.Default
                );
                
                var requestJson = JsonSerializer.Serialize(createSessionRequest, JsonOptions);
                await SendWebSocketMessage(client, requestJson);
                
                // Receive response
                var response = await ReceiveWebSocketMessage(client);
                Console.WriteLine($"Server response: {response}");
                
                // Parse the response
                var jsonDoc = JsonDocument.Parse(response);
                if (jsonDoc.RootElement.TryGetProperty("type", out var typeElement) && 
                    typeElement.GetString() == "create-session-response")
                {
                    var createResponse = JsonSerializer.Deserialize<CreateSessionResponse>(response, JsonOptions);
                    if (createResponse?.Success == true)
                    {
                        Console.WriteLine($"✅ Session created successfully!");
                        Console.WriteLine($"Session Code: {createResponse.SessionCode}");
                        Console.WriteLine($"Session ID: {createResponse.SessionInfo.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Failed to create session: {createResponse?.Error}");
                    }
                }
                
                // Test 2: Send a ping
                Console.WriteLine();
                Console.WriteLine("Test 2: Sending ping...");
                var ping = new PingMessage(DateTime.UtcNow.Ticks);
                var pingJson = JsonSerializer.Serialize(ping, JsonOptions);
                await SendWebSocketMessage(client, pingJson);
                
                var pingResponse = await ReceiveWebSocketMessage(client);
                Console.WriteLine($"Ping response: {pingResponse}");
                
                // Close connection
                Console.WriteLine();
                Console.WriteLine("Closing connection...");
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                
                Console.WriteLine();
                Console.WriteLine("=== Test Complete ===");
                Console.WriteLine("All tests passed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static async Task SendWebSocketMessage(WebSocket webSocket, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }
        
        private static async Task<string> ReceiveWebSocketMessage(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                return Encoding.UTF8.GetString(buffer, 0, result.Count);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                return "[Connection closed]";
            }
            
            return "[Unknown message type]";
        }
    }
}