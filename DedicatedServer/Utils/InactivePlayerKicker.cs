using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using DedicatedServer.Config;
using Microsoft.Xna.Framework;

namespace DedicatedServer.Utils
{
    /// <summary>
    /// 管理不活跃玩家的踢出功能
    /// </summary>
    public class InactivePlayerKicker
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly ModConfig config;
        private readonly ChatBox chatBox;
        private readonly Dictionary<long, DateTime> playerLastActivity;
        private readonly Dictionary<long, string> playerNames;
        private readonly Dictionary<long, Vector2> playerLastPosition;
        private DateTime lastCheckTime;
        
        public InactivePlayerKicker(IModHelper helper, IMonitor monitor, ModConfig config, ChatBox chatBox = null)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.config = config;
            this.chatBox = chatBox;
            this.playerLastActivity = new Dictionary<long, DateTime>();
            this.playerNames = new Dictionary<long, string>();
            this.playerLastPosition = new Dictionary<long, Vector2>();
            this.lastCheckTime = DateTime.Now;
        }

        /// <summary>
        /// 启用不活跃玩家检测
        /// </summary>
        public void Enable()
        {
            if (!config.EnableInactivePlayerKick)
            {
                monitor.Log("不活跃玩家踢出功能已禁用", LogLevel.Info);
                return;
            }

            monitor.Log($"启用不活跃玩家踢出功能 - 超时时间: {config.InactiveKickTimeMinutes}分钟", LogLevel.Info);

            // 订阅事件
            helper.Events.Multiplayer.PeerConnected += OnPlayerConnected;
            helper.Events.Multiplayer.PeerDisconnected += OnPlayerDisconnected;
            helper.Events.Input.ButtonPressed += OnPlayerActivity;
            helper.Events.Input.CursorMoved += OnPlayerActivity;
            helper.Events.Player.Warped += OnPlayerWarped;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdate;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            monitor.Log("不活跃玩家踢出功能已启用", LogLevel.Info);
        }

        /// <summary>
        /// 禁用不活跃玩家检测
        /// </summary>
        public void Disable()
        {
            // 取消订阅事件
            helper.Events.Multiplayer.PeerConnected -= OnPlayerConnected;
            helper.Events.Multiplayer.PeerDisconnected -= OnPlayerDisconnected;
            helper.Events.Input.ButtonPressed -= OnPlayerActivity;
            helper.Events.Input.CursorMoved -= OnPlayerActivity;
            helper.Events.Player.Warped -= OnPlayerWarped;
            helper.Events.GameLoop.OneSecondUpdateTicked -= OnOneSecondUpdate;
            helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;

            // 清空记录
            playerLastActivity.Clear();
            playerNames.Clear();
            playerLastPosition.Clear();

            monitor.Log("不活跃玩家踢出功能已禁用", LogLevel.Info);
        }

        /// <summary>
        /// 玩家连接时记录
        /// </summary>
        private void OnPlayerConnected(object sender, PeerConnectedEventArgs e)
        {
            var playerId = e.Peer.PlayerID;
            var playerName = Game1.getFarmer(playerId)?.Name ?? "未知玩家";
            
            playerLastActivity[playerId] = DateTime.Now;
            playerNames[playerId] = playerName;
            
            monitor.Log($"玩家连接: {playerName} (ID: {playerId})", LogLevel.Info);
            chatBox?.textBoxEnter($"玩家 {playerName} 已连接到服务器");
        }

        /// <summary>
        /// 玩家断开连接时清理记录
        /// </summary>
        private void OnPlayerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            var playerId = e.Peer.PlayerID;
            var playerName = playerNames.ContainsKey(playerId) ? playerNames[playerId] : "未知玩家";
            
            playerLastActivity.Remove(playerId);
            playerNames.Remove(playerId);
            playerLastPosition.Remove(playerId);
            
            monitor.Log($"玩家断开连接: {playerName} (ID: {playerId})", LogLevel.Info);
            chatBox?.textBoxEnter($"玩家 {playerName} 已离开服务器");
        }

        /// <summary>
        /// 检测玩家按键和鼠标活动
        /// </summary>
        private void OnPlayerActivity(object sender, EventArgs e)
        {
            if (!Game1.IsMultiplayer || Game1.player == null)
                return;

            // 更新当前玩家的活动（如果不是主机）
            var playerId = Game1.player.UniqueMultiplayerID;
            if (playerId != 0)
            {
                UpdatePlayerActivity(playerId);
            }

            // 由于输入事件只能检测到本地玩家的活动，
            // 我们需要通过其他方式来检测远程玩家的活动
        }

        /// <summary>
        /// 检测玩家传送活动
        /// </summary>
        private void OnPlayerWarped(object sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer)
                return;

            var playerId = e.Player.UniqueMultiplayerID;
            if (playerId != 0) // 不是主机
            {
                UpdatePlayerActivity(playerId);
            }
        }

        /// <summary>
        /// 更新玩家活动时间
        /// </summary>
        private void UpdatePlayerActivity(long playerId)
        {
            if (playerLastActivity.ContainsKey(playerId))
            {
                playerLastActivity[playerId] = DateTime.Now;
            }
        }

        /// <summary>
        /// 基于真实时间每分钟检查不活跃玩家
        /// </summary>
        private void OnOneSecondUpdate(object sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!config.EnableInactivePlayerKick || !Game1.IsMultiplayer || Game1.server == null)
                return;

            // 检查是否已经过了1分钟的真实时间
            var currentTime = DateTime.Now;
            var timeSinceLastCheck = currentTime - lastCheckTime;
            
            if (timeSinceLastCheck.TotalMinutes >= 1.0)
            {
                lastCheckTime = currentTime;
                CheckInactivePlayers();
            }
        }

        /// <summary>
        /// 检查并踢出不活跃玩家
        /// </summary>
        private void CheckInactivePlayers()
        {
            var currentTime = DateTime.Now;
            var kickTimeout = TimeSpan.FromMinutes(config.InactiveKickTimeMinutes);
            var playersToKick = new List<long>();

            foreach (var kvp in playerLastActivity.ToList())
            {
                var playerId = kvp.Key;
                var lastActivity = kvp.Value;
                var inactiveDuration = currentTime - lastActivity;

                if (inactiveDuration > kickTimeout)
                {
                    playersToKick.Add(playerId);
                }
            }

            // 踢出不活跃玩家
            foreach (var playerId in playersToKick)
            {
                KickInactivePlayer(playerId);
            }
        }

        /// <summary>
        /// 踢出不活跃玩家
        /// </summary>
        private void KickInactivePlayer(long playerId)
        {
            var playerName = playerNames.ContainsKey(playerId) ? playerNames[playerId] : "未知玩家";
            
            try
            {
                // 发送踢出警告消息
                var message = $"玩家 {playerName} 因为超过 {config.InactiveKickTimeMinutes} 分钟没有活动而被踢出服务器";
                monitor.Log(message, LogLevel.Info);
                chatBox?.textBoxEnter(message);

                // 执行踢出
                Game1.server.kick(playerId);
                
                // 清理记录
                playerLastActivity.Remove(playerId);
                playerNames.Remove(playerId);
                playerLastPosition.Remove(playerId);
            }
            catch (Exception ex)
            {
                monitor.Log($"踢出玩家 {playerName} (ID: {playerId}) 时发生错误: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// 每tick检查玩家位置变化来检测活动
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!config.EnableInactivePlayerKick || !Game1.IsMultiplayer)
                return;

            // 每60个tick检查一次位置变化（约1秒）
            if (e.Ticks % 60 != 0)
                return;

            CheckPlayerPositions();
        }

        /// <summary>
        /// 检查所有玩家的位置变化
        /// </summary>
        private void CheckPlayerPositions()
        {
            foreach (var farmer in Game1.otherFarmers.Values)
            {
                if (farmer == null || farmer.UniqueMultiplayerID == 0)
                    continue;

                var playerId = farmer.UniqueMultiplayerID;
                var currentPosition = farmer.Position;

                if (playerLastPosition.ContainsKey(playerId))
                {
                    var lastPosition = playerLastPosition[playerId];
                    // 如果位置有变化，更新活动时间
                    if (Vector2.Distance(currentPosition, lastPosition) > 5f) // 5像素的容差
                    {
                        UpdatePlayerActivity(playerId);
                    }
                }

                playerLastPosition[playerId] = currentPosition;
            }
        }

        /// <summary>
        /// 获取当前在线玩家的活动状态
        /// </summary>
        public void ShowPlayerStatus()
        {
            if (!config.EnableInactivePlayerKick)
            {
                monitor.Log("不活跃玩家踢出功能未启用", LogLevel.Info);
                return;
            }

            var currentTime = DateTime.Now;
            monitor.Log("=== 玩家活动状态 ===", LogLevel.Info);
            
            if (playerLastActivity.Count == 0)
            {
                monitor.Log("没有玩家连接", LogLevel.Info);
                return;
            }

            foreach (var kvp in playerLastActivity)
            {
                var playerId = kvp.Key;
                var lastActivity = kvp.Value;
                var playerName = playerNames.ContainsKey(playerId) ? playerNames[playerId] : "未知玩家";
                var inactiveDuration = currentTime - lastActivity;
                var remainingTime = TimeSpan.FromMinutes(config.InactiveKickTimeMinutes) - inactiveDuration;

                if (remainingTime.TotalSeconds > 0)
                {
                    monitor.Log($"{playerName}: 剩余时间 {remainingTime.Minutes}:{remainingTime.Seconds:D2}", LogLevel.Info);
                }
                else
                {
                    monitor.Log($"{playerName}: 即将被踢出", LogLevel.Info);
                }
            }
        }
    }
}
