using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Network.NetReady;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DedicatedServer.HostAutomatorStages
{
    internal class ReadyCheckHelper
    {
        // 使用 Stardew Valley 1.6 的新 API
        private static readonly MethodInfo getIfExistsMethod;
        private static readonly FieldInfo readyStatesField;
        
        // 用于兼容性的缓存
        private static Dictionary<string, HashSet<long>> cachedReadyPlayers = new Dictionary<string, HashSet<long>>();
        
        static ReadyCheckHelper()
        {
            try
            {
                // 获取 ReadySynchronizer 的内部方法
                getIfExistsMethod = typeof(ReadySynchronizer).GetMethod("GetIfExists", BindingFlags.Instance | BindingFlags.NonPublic);
                
                // 获取 ServerReadyCheck 的 ReadyStates 字段
                readyStatesField = Assembly
                    .LoadFrom("Stardew Valley.dll")
                    .GetType("StardewValley.Network.NetReady.Internal.ServerReadyCheck")
                    ?.GetField("ReadyStates", BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (getIfExistsMethod == null || readyStatesField == null)
                {
                    throw new InvalidOperationException("无法找到 ReadySynchronizer 的内部方法或字段");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"初始化 ReadyCheckHelper 失败: {ex.Message}", ex);
            }
        }

        public static void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            // 清理前一天的缓存数据
            cachedReadyPlayers.Clear();

            //Checking mailbox sometimes gives some gold, but it's compulsory to unlock some events
            for (int i = 0; i < 10; ++i) {
                Game1.getFarm().mailbox();
            }

            //Unlocks the sewer
            if (!Game1.player.eventsSeen.Contains("295672") && Game1.netWorldState.Value.MuseumPieces.Count() >= 60) {
                Game1.player.eventsSeen.Add("295672");
            }

            //Upgrade farmhouse to match highest level cabin
            var targetLevel = Game1.getFarm().buildings.Where(o => o.isCabin).Select(o => ((Cabin)o.indoors.Value).upgradeLevel).DefaultIfEmpty(0).Max();
            if (targetLevel > Game1.player.HouseUpgradeLevel) {
                Game1.player.HouseUpgradeLevel = targetLevel;
                Game1.player.performRenovation("FarmHouse");
            }
        }

        public static void WatchReadyCheck(string checkName)
        {
            if (!cachedReadyPlayers.ContainsKey(checkName))
            {
                cachedReadyPlayers[checkName] = new HashSet<long>();
            }
        }

        // Prerequisite: OnDayStarted() must have been called at least once prior to this method being called.
        public static bool IsReady(string checkName, Farmer player)
        {
            try
            {
                // 使用 Game1.netReady 获取准备状态
                if (Game1.netReady != null)
                {
                    var serverReadyCheck = getIfExistsMethod?.Invoke(Game1.netReady, new object[] { checkName });
                    if (serverReadyCheck != null)
                    {
                        var readyStates = (IDictionary)readyStatesField.GetValue(serverReadyCheck);
                        if (readyStates.Contains(player.UniqueMultiplayerID))
                        {
                            var state = readyStates[player.UniqueMultiplayerID].ToString();
                            return state == "Ready";
                        }
                    }
                }
                
                // 后备方案：使用缓存
                WatchReadyCheck(checkName);
                if (cachedReadyPlayers.TryGetValue(checkName, out var readyIds))
                {
                    return readyIds.Contains(player.UniqueMultiplayerID);
                }
            }
            catch (Exception)
            {
                // 如果出错，使用缓存作为后备
                WatchReadyCheck(checkName);
                if (cachedReadyPlayers.TryGetValue(checkName, out var readyIds))
                {
                    return readyIds.Contains(player.UniqueMultiplayerID);
                }
            }

            return false;
        }

        // Replacement for FarmerTeam.GetNumberReady()
        public static int GetNumberReady(string checkName)
        {
            try
            {
                // 优先使用新 API
                if (Game1.netReady != null)
                {
                    return Game1.netReady.GetNumberReady(checkName);
                }
            }
            catch (Exception)
            {
                // 如果新 API 失败，使用后备方案
            }

            // 后备方案：手动计算
            WatchReadyCheck(checkName);
            if (cachedReadyPlayers.TryGetValue(checkName, out var readyIds))
            {
                return readyIds.Count;
            }

            return 0;
        }

        // Replacement for FarmerTeam.SetLocalReady()
        public static void SetLocalReady(string checkName, bool ready)
        {
            try
            {
                // 优先使用新 API
                if (Game1.netReady != null)
                {
                    Game1.netReady.SetLocalReady(checkName, ready);
                    
                    // 更新缓存以保持一致性
                    WatchReadyCheck(checkName);
                    if (cachedReadyPlayers.TryGetValue(checkName, out var cachedIds))
                    {
                        if (ready)
                            cachedIds.Add(Game1.player.UniqueMultiplayerID);
                        else
                            cachedIds.Remove(Game1.player.UniqueMultiplayerID);
                    }
                    return;
                }
            }
            catch (Exception)
            {
                // 如果新 API 失败，使用后备方案
            }

            // 后备方案：只更新本地缓存
            WatchReadyCheck(checkName);
            long playerId = Game1.player.UniqueMultiplayerID;
            
            if (cachedReadyPlayers.TryGetValue(checkName, out var readyIds))
            {
                if (ready)
                    readyIds.Add(playerId);
                else
                    readyIds.Remove(playerId);
            }
        }
    }
}
