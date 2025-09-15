// <copyright file="ShareData2_Simple.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Coroutine;
using GameHelper;
using GameHelper.CoroutineEvents;
using GameHelper.Plugin;
using GameHelper.RemoteEnums;
using GameHelper.RemoteEnums.Entity;
using GameHelper.RemoteObjects.Components;
using GameHelper.RemoteObjects.States.InGameStateObjects;
using GameHelper.Utils;
using ImGuiNET;
using Newtonsoft.Json;
using GameOffsets.Natives;

namespace ShareData2
{
    /// <summary>
    ///     ShareData2 plugin for GameHelper2 framework.
    /// </summary>
    public sealed class ShareData2 : PCore<ShareData2Settings>
    {
        private HttpListener? _httpListener;
        private bool _serverIsRunning;
        private Task? _serverTask;

        private string SettingPathname => Path.Join(this.DllDirectory, "config", "settings.txt");
        
        private static void LogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] {message}";
                File.AppendAllText("log_sharedata2.txt", logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public override void OnEnable(bool isGameOpened)
        {
            // Load settings
            if (File.Exists(this.SettingPathname))
            {
                try
                {
                    var content = File.ReadAllText(this.SettingPathname);
                    this.Settings = JsonConvert.DeserializeObject<ShareData2Settings>(content) ?? new ShareData2Settings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading settings: {ex.Message}");
                }
            }

            // Start HTTP server
            this.StartHttpServer();
        }

        /// <inheritdoc />
        public override void OnDisable()
        {
            this.StopHttpServer();
        }

        /// <inheritdoc />
        public override void DrawSettings()
        {
            ImGui.Text("ShareData2 - Game Data Sharing Plugin");
            ImGui.Separator();

            var enable = this.Settings.Enable;
            ImGui.Checkbox("Enable Plugin", ref enable);
            this.Settings.Enable = enable;
            if (this.Settings.Enable)
            {
                ImGui.SameLine();
                var status = this._serverIsRunning ? "Running" : "Stopped";
                var color = this._serverIsRunning ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(1, 0, 0, 1);
                ImGui.TextColored(color, $"Server Status: {status}");
            }

            var port = this.Settings.Port;
            ImGui.InputInt("Port", ref port, 1, 100);
            this.Settings.Port = port;
            if (this.Settings.Port < 1024 || this.Settings.Port > 65535)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "Port must be between 1024 and 65535");
                this.Settings.Port = 53868;
            }

            var showDebug = this.Settings.ShowDebugInfo;
            ImGui.Checkbox("Show Debug Info", ref showDebug);
            this.Settings.ShowDebugInfo = showDebug;

            ImGui.Separator();
            ImGui.Text("API Endpoints:");
            ImGui.BulletText("/getData?type=partial - Get basic game data");
            ImGui.BulletText("/getData?type=full - Get detailed game data including terrain");
            ImGui.BulletText("/getScreenPos?x=X&y=Y - Convert grid coordinates to screen position");

            if (this.Settings.ShowDebugInfo)
            {
                ImGui.Separator();
                ImGui.Text("Debug Information:");
                ImGui.Text($"Game State: {Core.States.GameCurrentState}");
                try
                {
                    ImGui.Text($"Area: {Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails.Id}");
                }
                catch
                {
                    ImGui.Text("Area: Not available");
                }
            }
        }

        /// <inheritdoc />
        public override void DrawUI()
        {
            // No UI needed - plugin runs in background
        }

        /// <inheritdoc />
        public override void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this.SettingPathname));
                var settingsData = JsonConvert.SerializeObject(this.Settings, Formatting.Indented);
                File.WriteAllText(this.SettingPathname, settingsData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void StartHttpServer()
        {
            if (this._serverIsRunning || !this.Settings.Enable)
            {
                return;
            }

            try
            {
                this._httpListener = new HttpListener();
                this._httpListener.Prefixes.Add($"http://*:{this.Settings.Port}/");
                this._httpListener.Start();
                this._serverIsRunning = true;

                this._serverTask = Task.Run(this.ListenAsync);
                Console.WriteLine($"ShareData2 HTTP server started on port {this.Settings.Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start HTTP server: {ex.Message}");
                this._serverIsRunning = false;
            }
        }

        private void StopHttpServer()
        {
            if (!this._serverIsRunning)
            {
                return;
            }

            try
            {
                this._httpListener?.Stop();
                this._httpListener?.Close();
                this._httpListener = null;
                this._serverIsRunning = false;
                Console.WriteLine("ShareData2 HTTP server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping HTTP server: {ex.Message}");
            }
        }

        private async Task ListenAsync()
        {
            while (this._serverIsRunning && this._httpListener?.IsListening == true)
            {
                try
                {
                    var context = await this._httpListener.GetContextAsync();
                    _ = Task.Run(() => this.HandleRequestAsync(context));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HTTP Server error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                var responseString = this.HandleHttpRequest(request);
                var buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                    // Ignore errors when closing response
                }
            }
        }

        private string HandleHttpRequest(HttpListenerRequest request)
        {
            try
            {
                var path = request.Url?.AbsolutePath?.ToLower() ?? string.Empty;

                return path switch
                {
                    "/getdata" => this.HandleGetDataRequest(request),
                    "/getscreenpos" => this.HandleGetScreenPosRequest(request),
                    "/getlocationonscreen" => this.HandleGetLocationOnScreenRequest(request),
                    "/" => this.HandleRootRequest(),
                    _ => this.HandleNotFoundRequest()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling HTTP request: {ex.Message}");
                return JsonConvert.SerializeObject(new { error = "Internal server error" });
            }
        }

        private string HandleGetDataRequest(HttpListenerRequest request)
        {
            var query = request.Url?.Query ?? string.Empty;
            var type = "partial";

            if (query.Contains("type="))
            {
                var typeParam = query.Split('&')[0].Split('=')[1];
                if (typeParam == "full")
                {
                    type = "full";
                }
            }

            return this.GetGameData(type);
        }

        private string HandleGetScreenPosRequest(HttpListenerRequest request)
        {
            var query = request.Url?.Query ?? string.Empty;
            var x = 0;
            var y = 0;

            try
            {
                if (query.Contains("x=") && query.Contains("y="))
                {
                    var xParam = query.Split('&')[0].Split('=')[1];
                    var yParam = query.Split('&')[1].Split('=')[1];
                    x = int.Parse(xParam);
                    y = int.Parse(yParam);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing coordinates: {ex.Message}");
            }

            var screenPos = this.GetScreenPosition(x, y);
            return JsonConvert.SerializeObject(screenPos);
        }

        private string HandleGetLocationOnScreenRequest(HttpListenerRequest request)
        {
            var query = request.Url?.Query ?? string.Empty;
            var x = 0;
            var y = 0;
            var z = 0;

            try
            {
                if (query.Contains("x=") && query.Contains("y=") && query.Contains("z="))
                {
                    var parts = query.Split('&');
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("x="))
                            x = int.Parse(part.Split('=')[1]);
                        else if (part.StartsWith("y="))
                            y = int.Parse(part.Split('=')[1]);
                        else if (part.StartsWith("z="))
                            z = int.Parse(part.Split('=')[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing coordinates: {ex.Message}");
            }

            // Convert world coordinates to screen coordinates
            var screenPos = this.GetLocationOnScreen(x, y, z);
            return JsonConvert.SerializeObject(screenPos);
        }

        private string HandleRootRequest()
        {
            return JsonConvert.SerializeObject(new
            {
                name = "ShareData2",
                version = "2.0.0",
                endpoints = new[]
                {
                    "/getData?type=partial",
                    "/getData?type=full",
                    "/getScreenPos?x=X&y=Y",
                    "/getLocationOnScreen?x=X&y=Y&z=Z"
                }
            });
        }

        private string HandleNotFoundRequest()
        {
            return JsonConvert.SerializeObject(new { error = "Endpoint not found" });
        }

        private string GetGameData(string type = "partial")
        {
            try
            {
                var gameState = Core.States.GameCurrentState switch
                {
                    GameStateTypes.InGameState => 20,
                    GameStateTypes.LoginState => 1,
                    _ => 0
                };

                // Initialize with default values
                var windowBounds = new[] { 0, 1920, 0, 1080 };
                var mousePosition = new[] { 0, 0 };
                var areaHash = 0u;
                var areaName = "Unknown";
                var isLoading = Core.States.GameCurrentState != GameStateTypes.InGameState;
                var awakeEntities = new List<object>();
                var player = new
                {
                    gp = new[] { 0, 0 }, // grid_position
                    l = new[] { 100, 100, 0, 100, 100, 0, 0, 0, 0 }, // life_data
                    b = new string[0], // buffs
                    db = new string[0], // debuffs
                    isMoving = 0,
                    level = 1
                };

                // Try to get real game data if in game
                if (Core.States.GameCurrentState == GameStateTypes.InGameState)
                {
                    try
                    {
                        // Get actual window bounds from game
                        try
                        {
                            var gameWindow = Core.Process.WindowArea;
                            windowBounds = new[] { 
                                gameWindow.X, 
                                gameWindow.Right, 
                                gameWindow.Y, 
                                gameWindow.Bottom 
                            };
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error getting window bounds: {ex.Message}");
                            // Keep default values
                        }

                        // Get mouse position - using default for now
                        mousePosition = new[] { 0, 0 };

                        // Get area info
                        try
                        {
                            var area = Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails;
                            areaName = area.Id;
                            // areaHash = area.Hash; // This might not exist in GameHelper2
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error getting area info: {ex.Message}");
                        }

                        // Get entities
                        awakeEntities = this.GetAwakeEntities();

                        // Get player info
                        player = (dynamic)this.GetPlayerInfo();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting game data: {ex.Message}");
                    }
                }

                var terrainString = type == "full" ? this.GenerateMinimap() : "";
                
                var gameData = new
                {
                    gameState = gameState,
                    windowBounds = windowBounds,
                    mousePosition = mousePosition,
                    terrain_string = terrainString, // Change to snake_case for bot compatibility
                    areaHash = areaHash,
                    areaName = areaName,
                    area_raw_name = areaName, // Add this field for bot compatibility
                    IsLoading = isLoading, // Change to PascalCase for bot compatibility
                    awake_entities = awakeEntities, // Change to snake_case for bot compatibility
                    pi = player, // Change from 'player' to 'pi' for bot compatibility
                    w = windowBounds, // Add window bounds as 'w' for bot compatibility [x, x2, y, y2]
                    f = new { n = new string[0], i = new int[0], cu = new bool[0] }, // Add flasks data for bot compatibility
                    ipv = false, // Add invites panel visible for bot compatibility
                    s = new { c_b_u = new int[0], cs = new int[0], i_n = new string[0], d = new string[0], tu = new int[0] }, // Add skills data for bot compatibility
                    vl = new object[0], // Add visible labels for bot compatibility
                    g_s = 20, // Add game state for bot compatibility
                    ah = areaHash, // Add area hash for bot compatibility
                    // Terrain arrays for bot compatibility
                    terrain_passable = GenerateTerrainPassableArray(terrainString),
                    terrain_currently_passable_area = GenerateTerrainPassableArray(terrainString), // Same as passable for now
                    terrain_visited_area = GenerateTerrainVisitedArray(terrainString)
                };

                return JsonConvert.SerializeObject(gameData, Formatting.Indented);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetGameData: {ex.Message}");
                return JsonConvert.SerializeObject(new { error = "Failed to get game data" });
            }
        }

        private string GenerateMinimap()
        {
            try
            {
                // Get real terrain data from GameHelper2 using Radar logic
                if (Core.States.GameCurrentState == GameStateTypes.InGameState)
                {
                    var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
                    if (areaInstance != null && areaInstance.GridWalkableData != null && areaInstance.GridWalkableData.Length > 0)
                    {
                        var terrainMetadata = areaInstance.TerrainMetadata;
                        var walkableData = areaInstance.GridWalkableData;
                        var bytesPerRow = terrainMetadata.BytesPerRow;
                        
                        if (bytesPerRow <= 0)
                        {
                            LogMessage("Invalid bytesPerRow, using fallback terrain");
                            return GenerateFallbackTerrain();
                        }

                        // Use MapEdgeDetector to get accurate dimensions like Radar
                        var mapEdgeDetector = new MapEdgeDetector(walkableData, bytesPerRow);
                        var actualWidth = bytesPerRow * 2;  // Like Radar uses
                        var actualHeight = mapEdgeDetector.TotalRows;
                        
                        LogMessage($"Terrain: {actualWidth}x{actualHeight}, BytesPerRow: {bytesPerRow}, DataLength: {walkableData.Length}");
                        
                        var sb = new StringBuilder();
                        
                        // Generate terrain string using actual dimensions with boundary checking
                        for (var r = actualHeight - 1; r >= 0; --r)
                        {
                            for (var c = 0; c < actualWidth; c++)
                            {
                                // Use boundary checking like Radar
                                if (!mapEdgeDetector.IsInsideMapBoundary(c, r))
                                {
                                    sb.Append("0");  // Outside boundary = not walkable
                                    continue;
                                }
                                
                                // Use WalkableValue with boundary checking
                                try
                                {
                                    var b = WalkableValue(walkableData, bytesPerRow, c, r);
                                    var ch = b.ToString()[0];
                                    if (b == 0)
                                        ch = '0';
                                    sb.AppendFormat("{0}", ch);
                                }
                                catch (Exception ex)
                                {
                                    LogMessage($"WalkableValue error at ({c}, {r}): {ex.Message}");
                                    sb.Append("0");  // Error = not walkable
                                }
                            }
                            sb.AppendLine();
                        }
                        
                        var terrainString = sb.ToString();
                        
                        // Save terrain as PNG image for debugging
                        try
                        {
                            SaveTerrainAsImage(terrainString, actualWidth, actualHeight);
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error saving terrain image: {ex.Message}");
                        }
                        
                        return terrainString;
                    }
                }
                
                // Fallback to simple all-passable terrain if no real data
                LogMessage("Using fallback terrain data");
                return GenerateFallbackTerrain();
            }
            catch (Exception ex)
            {
                LogMessage($"Error generating minimap: {ex.Message}");
                return GenerateFallbackTerrain();
            }
        }

        private string GenerateFallbackTerrain()
        {
            var sb = new StringBuilder();
            for (var r = 0; r < 100; r++)
            {
                for (var c = 0; c < 100; c++)
                {
                    sb.Append("1");
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        private int[] GenerateTerrainPassableArray(string terrainString)
        {
            try
            {
                if (string.IsNullOrEmpty(terrainString))
                {
                    // Return empty array if no terrain data
                    return new int[0];
                }

                var lines = terrainString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var result = new List<int>();
                
                foreach (var line in lines)
                {
                    foreach (var ch in line)
                    {
                        // Convert terrain character to passable value (0 = not passable, 1 = passable)
                        // Based on bot logic: threshold 49, so values >= 49 are passable
                        var value = (int)ch;
                        result.Add(value >= 49 ? 1 : 0);
                    }
                }
                
                return result.ToArray();
            }
            catch (Exception ex)
            {
                LogMessage($"Error generating terrain passable array: {ex.Message}");
                return new int[0];
            }
        }

        private int[] GenerateTerrainVisitedArray(string terrainString)
        {
            try
            {
                if (string.IsNullOrEmpty(terrainString))
                {
                    // Return empty array if no terrain data
                    return new int[0];
                }

                var lines = terrainString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var result = new List<int>();
                
                foreach (var line in lines)
                {
                    foreach (var ch in line)
                    {
                        // Initialize visited area as all unvisited (0)
                        result.Add(0);
                    }
                }
                
                return result.ToArray();
            }
            catch (Exception ex)
            {
                LogMessage($"Error generating terrain visited array: {ex.Message}");
                return new int[0];
            }
        }


        private void SaveTerrainAsImage(string terrainString, int width, int height)
        {
            try
            {
                // Create bitmap
                using (var bitmap = new System.Drawing.Bitmap(width, height))
                {
                    var lines = terrainString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    for (int y = 0; y < height && y < lines.Length; y++)
                    {
                        var line = lines[y];
                        for (int x = 0; x < width && x < line.Length; x++)
                        {
                            var ch = line[x];
                            System.Drawing.Color color;
                            
                            // Color mapping based on terrain value
                            switch (ch)
                            {
                                case '0': // Not walkable
                                    color = System.Drawing.Color.Black;
                                    break;
                                case '1': // Walkable
                                    color = System.Drawing.Color.White;
                                    break;
                                case '2': // Walkable (different level)
                                    color = System.Drawing.Color.LightGray;
                                    break;
                                case '3': // Walkable (different level)
                                    color = System.Drawing.Color.Gray;
                                    break;
                                case '4': // Walkable (different level)
                                    color = System.Drawing.Color.DarkGray;
                                    break;
                                case '5': // Walkable (different level)
                                    color = System.Drawing.Color.Silver;
                                    break;
                                case '6': // Walkable (different level)
                                    color = System.Drawing.Color.LightBlue;
                                    break;
                                case '7': // Walkable (different level)
                                    color = System.Drawing.Color.Blue;
                                    break;
                                case '8': // Walkable (different level)
                                    color = System.Drawing.Color.DarkBlue;
                                    break;
                                case '9': // Walkable (different level)
                                    color = System.Drawing.Color.LightGreen;
                                    break;
                                case 'A': // Walkable (different level)
                                    color = System.Drawing.Color.Green;
                                    break;
                                case 'B': // Walkable (different level)
                                    color = System.Drawing.Color.DarkGreen;
                                    break;
                                case 'C': // Walkable (different level)
                                    color = System.Drawing.Color.Yellow;
                                    break;
                                case 'D': // Walkable (different level)
                                    color = System.Drawing.Color.Orange;
                                    break;
                                case 'E': // Walkable (different level)
                                    color = System.Drawing.Color.Red;
                                    break;
                                case 'F': // Walkable (different level)
                                    color = System.Drawing.Color.DarkRed;
                                    break;
                                default: // Unknown
                                    color = System.Drawing.Color.Magenta;
                                    break;
                            }
                            
                            bitmap.SetPixel(x, y, color);
                        }
                    }
                    
                    
                    // Save to file with timestamp
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var filename = $"terrain_debug_{timestamp}.png";
                    var filepath = Path.Combine(Environment.CurrentDirectory, filename);
                    
                    bitmap.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
                    LogMessage($"Terrain image saved: {filepath} ({width}x{height})");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error creating terrain image: {ex.Message}");
            }
        }


        public static byte WalkableValue(byte[] data, int bytesPerRow, long c, long r)
        {
            var offset = r * bytesPerRow + c / 2;
            if (offset < 0 || offset >= data.Length)
            {
                throw new Exception(string.Format($"WalkableValue failed: ({c}, {r}) [{bytesPerRow}] => {offset}"));
            }

            byte b;
            if ((c & 1) == 0)
            {
                b = (byte)(data[offset] & 0xF);
            }
            else
            {
                b = (byte)(data[offset] >> 4);
            }
            return b;
        }

        private int[] GetScreenPosition(int gridX, int gridY)
        {
            // Simple screen position calculation
            return new[] { gridX * 10, gridY * 10 };
        }

        private int[] GetLocationOnScreen(int worldX, int worldY, int worldZ)
        {
            try
            {
                if (Core.States.GameCurrentState == GameStateTypes.InGameState)
                {
                    // Convert world coordinates to screen coordinates - simplified for now
                    // var worldPos = new System.Numerics.Vector3(worldX, worldY, worldZ);
                    // var screenPos = Core.States.InGameStateObject.Camera.WorldToScreen(worldPos);
                    // return new[] { (int)screenPos.X, (int)screenPos.Y };
                    return new[] { worldX / 10, worldY / 10 };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting world to screen coordinates: {ex.Message}");
            }

            // Fallback to simple calculation
            return new[] { worldX / 10, worldY / 10 };
        }

        private List<object> GetAwakeEntities()
        {
            var entities = new List<object>();
            
            try
            {
                if (Core.States.GameCurrentState != GameStateTypes.InGameState)
                {
                    return entities;
                }

                // Get all entities from the game
                var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
                var awakeEntities = areaInstance.AwakeEntities;
                
                foreach (var kvp in awakeEntities)
                {
                    try
                    {
                        var entity = kvp.Value;
                        
                        // Skip invalid entities
                        if (!entity.IsValid)
                        {
                            continue;
                        }

                        // Skip effect objects
                        if (entity.EntityType == EntityTypes.Renderable)
                        {
                            continue;
                        }

                        var entityData = new
                        {
                            i = (int)entity.Id,
                            p = entity.Path ?? "",
                            et = this.SerializeEntityType(entity.EntityType.ToString()),
                            gp = this.GetEntityGridPosition(entity),
                            wp = this.GetEntityWorldPosition(entity),
                            ls = this.GetEntityScreenPosition(entity),
                            rn = this.GetEntityRenderName(entity),
                            r = this.GetEntityRarity(entity),
                            h = this.IsEntityHostile(entity),
                            ia = this.IsAttackable(entity),
                            t = this.IsTargetable(entity),
                            it = this.IsTargeted(entity),
                            o = this.IsOpened(entity),
                            b = this.HasEntityBounds(entity),
                            l = this.GetEntityLifeData(entity)
                        };

                        entities.Add(entityData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing entity {kvp.Value.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting entities: {ex.Message}");
            }

            return entities;
        }

        private object GetPlayerInfo()
        {
            try
            {
                if (Core.States.GameCurrentState != GameStateTypes.InGameState)
                {
                    return new
                    {
                        gp = new[] { 0, 0 }, // grid_position
                        l = new[] { 100, 100, 0, 100, 100, 0, 0, 0, 0 }, // life_data
                        b = new string[0], // buffs
                        db = new string[0], // debuffs
                        isMoving = 0,
                        level = 1
                    };
                }

                // Get real player data
                var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
                var player = areaInstance.Player;
                var life = player.TryGetComponent<Life>(out var lifeComp) ? lifeComp : null;
                var actor = player.TryGetComponent<Actor>(out var actorComp) ? actorComp : null;
                var playerComp = player.TryGetComponent<Player>(out var playerComp2) ? playerComp2 : null;

                var lifeData = new[] { 100, 100, 0, 100, 100, 0, 0, 0, 0 };
                if (life != null)
                {
                    lifeData = new[]
                    {
                        life.Health.Total,
                        life.Health.Current,
                        life.Health.ReservedTotal,
                        life.Mana.Total,
                        life.Mana.Current,
                        life.Mana.ReservedTotal,
                        life.EnergyShield.Total,
                        life.EnergyShield.Current,
                        life.EnergyShield.ReservedTotal
                    };
                }

                var buffs = new List<string>();
                var buffsComp = player.TryGetComponent<Buffs>(out var buffsComp2) ? buffsComp2 : null;
                if (buffsComp != null)
                {
                    foreach (var buff in buffsComp.StatusEffects.Keys)
                    {
                        buffs.Add(buff);
                    }
                }

                var isMoving = 0;
                if (actor != null)
                {
                    isMoving = 0; // Simplified for now
                }

                var level = 1;
                if (playerComp != null)
                {
                    level = playerComp.Level;
                }

                var render = player.TryGetComponent<Render>(out var renderComp) ? renderComp : null;
                var gridPos = new[] { 0, 0 };
                if (render != null)
                {
                    // Get actual terrain dimensions for proper clamping
                    var terrainMetadata = areaInstance.TerrainMetadata;
                    var walkableData = areaInstance.GridWalkableData;
                    var bytesPerRow = terrainMetadata.BytesPerRow;
                    
                    if (bytesPerRow > 0 && walkableData != null && walkableData.Length > 0)
                    {
                        var mapEdgeDetector = new MapEdgeDetector(walkableData, bytesPerRow);
                        var terrainWidth = bytesPerRow * 2;  // Like Radar uses
                        var terrainHeight = mapEdgeDetector.TotalRows;
                        
                        // Clamp player coordinates to terrain bounds
                        var x = Math.Max(0, Math.Min(terrainWidth - 1, (int)render.GridPosition.X));
                        var y = Math.Max(0, Math.Min(terrainHeight - 1, (int)render.GridPosition.Y));
                        
                        // Use (x, y) format as expected by bot
                        gridPos = new[] { x, y };
                        
                        LogMessage($"Player position clamped: original=({render.GridPosition.X}, {render.GridPosition.Y}) -> clamped=({x}, {y}) terrain=({terrainWidth}x{terrainHeight})");
                    }
                    else
                    {
                        // Fallback to original coordinates if terrain data not available
                        gridPos = new[] { (int)render.GridPosition.X, (int)render.GridPosition.Y };
                    }
                }

                return new
                {
                    gp = gridPos, // grid_position
                    l = lifeData, // life_data
                    b = buffs.ToArray(), // buffs
                    db = new string[0], // debuffs (empty for now)
                    isMoving = isMoving,
                    level = level
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting player info: {ex.Message}");
                return new
                {
                    gp = new[] { 0, 0 }, // grid_position
                    l = new[] { 100, 100, 0, 100, 100, 0, 0, 0, 0 }, // life_data
                    b = new string[0], // buffs
                    db = new string[0], // debuffs
                    isMoving = 0,
                    level = 1
                };
            }
        }

        private string SerializeEntityType(string entityType)
        {
            return entityType switch
            {
                "Monster" => "m",
                "AreaTransition" => "at",
                "Chest" => "c",
                "WorldItem" => "wi",
                _ => entityType.ToLower()
            };
        }

        private int[] GetEntityScreenPosition(Entity entity)
        {
            try
            {
                var render = entity.TryGetComponent<Render>(out var renderComp) ? renderComp : null;
                if (render == null)
                {
                    return new[] { 0, 0 };
                }

                if (render.WorldPosition.X == 0 && render.WorldPosition.Y == 0 && render.WorldPosition.Z == 0)
                {
                    return new[] { 0, 0 };
                }

                // Convert world position to screen position
                var screenPos = Core.States.InGameStateObject.CurrentWorldInstance.WorldToScreen(render.WorldPosition);
                return new[] { (int)screenPos.X, (int)screenPos.Y };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting entity screen position: {ex.Message}");
                return new[] { 0, 0 };
            }
        }

        private int IsAttackable(Entity entity)
        {
            try
            {
                // Check if entity is attackable (simplified for now)
                try
                {
                    if (!entity.TryGetComponent<Life>(out var lifeComp) || !lifeComp.IsAlive)
                    {
                        return 0;
                    }
                    return 1;
                }
                catch
                {
                    return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private int IsTargetable(Entity entity)
        {
            try
            {
                var targetable = entity.TryGetComponent<Targetable>(out var targetableComp) ? targetableComp : null;
                if (targetable == null) return 0;
                
                // Access raw IsTargetable value using reflection to get the cache field
                var cacheField = typeof(Targetable).GetField("cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cacheField != null)
                {
                    var cache = cacheField.GetValue(targetable);
                    var isTargetableField = cache.GetType().GetField("IsTargetable");
                    if (isTargetableField != null)
                    {
                        var rawValue = (bool)isTargetableField.GetValue(cache);
                        return rawValue ? 1 : 0;
                    }
                }
                
                // Fallback to processed property if reflection fails
                return targetable.IsTargetable ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int IsTargeted(Entity entity)
        {
            try
            {
                var targetable = entity.TryGetComponent<Targetable>(out var targetableComp) ? targetableComp : null;
                if (targetable == null) return 0;
                
                // Access the cache field through reflection or use a different approach
                // For now, let's use IsTargetable as a fallback since we can't easily access the cache
                return targetable.IsTargetable ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int IsOpened(Entity entity)
        {
            try
            {
                var triggerableBlockage = entity.TryGetComponent<TriggerableBlockage>(out var triggerableComp) ? triggerableComp : null;
                return triggerableBlockage?.IsBlocked == false ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int[] GetEntityLifeData(Entity entity)
        {
            try
            {
                var life = entity.TryGetComponent<Life>(out var lifeComp) ? lifeComp : null;
                if (life == null)
                {
                    return new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                }

                return new[]
                {
                    life.Health.Total,
                    life.Health.Current,
                    life.Health.ReservedTotal,
                    life.Mana.Total,
                    life.Mana.Current,
                    life.Mana.ReservedTotal,
                    life.EnergyShield.Total,
                    life.EnergyShield.Current,
                    life.EnergyShield.ReservedTotal
                };
            }
            catch
            {
                return new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            }
        }

        private int[] GetEntityGridPosition(Entity entity)
        {
            try
            {
                var render = entity.TryGetComponent<Render>(out var renderComp) ? renderComp : null;
                if (render != null)
                {
                    return new[] { (int)render.GridPosition.X, (int)render.GridPosition.Y };
                }
            }
            catch { }
            return new[] { 0, 0 };
        }

        private int[] GetEntityWorldPosition(Entity entity)
        {
            try
            {
                var render = entity.TryGetComponent<Render>(out var renderComp) ? renderComp : null;
                if (render != null)
                {
                    return new[] { (int)render.WorldPosition.X, (int)render.WorldPosition.Y, (int)render.WorldPosition.Z };
                }
            }
            catch { }
            return new[] { 0, 0, 0 };
        }

        private string GetEntityRenderName(Entity entity)
        {
            try
            {
                // Simplified - return entity path as name
                return entity.Path ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetEntityRarity(Entity entity)
        {
            try
            {
                var omp = entity.TryGetComponent<ObjectMagicProperties>(out var ompComp) ? ompComp : null;
                return omp?.Rarity.ToString() ?? "Normal";
            }
            catch
            {
                return "Normal";
            }
        }

        private int IsEntityHostile(Entity entity)
        {
            try
            {
                var positioned = entity.TryGetComponent<Positioned>(out var posComp) ? posComp : null;
                return positioned?.IsFriendly == false ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int HasEntityBounds(Entity entity)
        {
            try
            {
                var render = entity.TryGetComponent<Render>(out var renderComp) ? renderComp : null;
                return render?.ModelBounds.X > 0 ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
