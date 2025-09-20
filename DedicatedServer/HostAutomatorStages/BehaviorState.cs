using DedicatedServer.Chat;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DedicatedServer.HostAutomatorStages
{
    internal class BehaviorState
    {
        private const int startOfDayWaitTicks = 60;
        private static FieldInfo multiplayerFieldInfo = typeof(Game1).GetField("multiplayer", BindingFlags.NonPublic | BindingFlags.Static);
        private static Multiplayer multiplayer = null;

        private int betweenEventsWaitTicks = 0;
        private int betweenDialoguesWaitTicks = 0;
        private int betweenShippingMenusWaitTicks = 0;
        private bool checkedForParsnipSeeds = false;
        private bool exitedFarmhouse = false;
        private int betweenTransitionSleepWaitTicks = 0;
        private int betweenTransitionFestivalAttendanceWaitTicks = 0;
        private int betweenTransitionFestivalEndWaitTicks = 0;
        private int waitTicks = startOfDayWaitTicks;
        private int numFestivalStartVotes = 0;
        private int numFestivalStartVotesRequired = 0;
        private IDictionary<long, Farmer> otherPlayers = new Dictionary<long, Farmer>();
        private IMonitor monitor;
        private FestivalChatBox festivalChatBox;
        private bool hasLoggedFestivalChatBoxCheck = false; // 防止无限刷屏的标志
        
        // 节日主活动状态管理（模仿dsa文件的模式）
        private bool festivalMainEventAvailable = false;
        private int festivalMainEventCountDown = 0;

        public BehaviorState(IMonitor monitor, EventDrivenChatBox chatBox, IModHelper helper)
        {
            this.monitor = monitor;
            festivalChatBox = new FestivalChatBox(chatBox, otherPlayers, helper);
        }

        public bool HasBetweenEventsWaitTicks()
        {
            return betweenEventsWaitTicks > 0;
        }
        public void DecrementBetweenEventsWaitTicks()
        {
            betweenEventsWaitTicks--;
        }
        public void SkipEvent()
        {
            betweenEventsWaitTicks = (int)(600 * 0.2);
        }
        public void ClearBetweenEventsWaitTicks()
        {
            betweenEventsWaitTicks = 0;
        }

        public bool HasBetweenDialoguesWaitTicks()
        {
            return betweenDialoguesWaitTicks > 0;
        }
        public void DecrementBetweenDialoguesWaitTicks()
        {
            betweenDialoguesWaitTicks--;
        }
        public void SkipDialogue()
        {
            betweenDialoguesWaitTicks = (int)(60 * 0.2);
        }
        public void ClearBetweenDialoguesWaitTicks()
        {
            betweenDialoguesWaitTicks = 0;
        }

        public bool HasBetweenShippingMenusWaitTicks()
        {
            return betweenShippingMenusWaitTicks > 0;
        }
        public void DecrementBetweenShippingMenusWaitTicks()
        {
            betweenShippingMenusWaitTicks--;
        }
        public void SkipShippingMenu()
        {
            betweenShippingMenusWaitTicks = 60;
        }
        public void ClearBetweenShippingMenusWaitTicks()
        {
            betweenShippingMenusWaitTicks = 0;
        }

        public bool HasCheckedForParsnipSeeds()
        {
            return checkedForParsnipSeeds;
        }
        public void CheckForParsnipSeeds()
        {
            checkedForParsnipSeeds = true;
        }

        public bool ExitedFarmhouse()
        {
            return exitedFarmhouse;
        }
        public void ExitFarmhouse()
        {
            exitedFarmhouse = true;
        }

        public bool HasBetweenTransitionSleepWaitTicks()
        {
            return betweenTransitionSleepWaitTicks > 0;
        }
        public void DecrementBetweenTransitionSleepWaitTicks()
        {
            betweenTransitionSleepWaitTicks--;
        }
        public void Sleep()
        {
            betweenTransitionSleepWaitTicks = (int)(60 * 0.2);
        }
        public void WarpToSleep()
        {
            betweenTransitionSleepWaitTicks = 60;
        }
        public void CancelSleep()
        {
            betweenTransitionSleepWaitTicks = (int)(60 * 0.2);
        }
        public void ClearBetweenTransitionSleepWaitTicks()
        {
            betweenTransitionSleepWaitTicks = 0;
        }

        public bool HasBetweenTransitionFestivalAttendanceWaitTicks()
        {
            return betweenTransitionFestivalAttendanceWaitTicks > 0;
        }
        public void DecrementBetweenTransitionFestivalAttendanceWaitTicks()
        {
            betweenTransitionFestivalAttendanceWaitTicks--;
        }
        public void WaitForFestivalAttendance()
        {
            betweenTransitionFestivalAttendanceWaitTicks = (int)(60 * 0.2);
        }
        public void StopWaitingForFestivalAttendance()
        {
            betweenTransitionFestivalAttendanceWaitTicks = (int)(60 * 0.2);
        }
        public void ClearBetweenTransitionFestivalAttendanceWaitTicks()
        {
            betweenTransitionFestivalAttendanceWaitTicks = 0;
        }

        public bool HasBetweenTransitionFestivalEndWaitTicks()
        {
            return betweenTransitionFestivalEndWaitTicks > 0;
        }
        public void DecrementBetweenTransitionFestivalEndWaitTicks()
        {
            betweenTransitionFestivalEndWaitTicks--;
        }
        public void WaitForFestivalEnd()
        {
            betweenTransitionFestivalEndWaitTicks = (int)(60 * 0.2);
        }
        public void StopWaitingForFestivalEnd()
        {
            betweenTransitionFestivalEndWaitTicks = (int)(60 * 0.2);
        }
        public void ClearBetweenTransitionFestivalEndWaitTicks()
        {
            betweenTransitionFestivalEndWaitTicks = 0;
        }

        public bool HasWaitTicks()
        {
            return waitTicks > 0;
        }
        public void SetWaitTicks(int waitTicks)
        {
            this.waitTicks = waitTicks;
        }
        public void DecrementWaitTicks()
        {
            waitTicks--;
        }
        public void ClearWaitTicks()
        {
            waitTicks = 0;
        }
        
        public Tuple<int, int> UpdateFestivalStartVotes()
        {
            if (festivalChatBox != null && festivalChatBox.IsEnabled())
            {
                // 只在第一次检查时打印日志，避免无限刷屏
                if (!hasLoggedFestivalChatBoxCheck)
                {
                    monitor?.Log("节日流程: 检查节日投票状态...", StardewModdingAPI.LogLevel.Info);
                    hasLoggedFestivalChatBoxCheck = true;
                }
                
                int numFestivalStartVotes = festivalChatBox.NumVoted();
                if (numFestivalStartVotes != this.numFestivalStartVotes || otherPlayers.Count != numFestivalStartVotesRequired)
                {
                    this.numFestivalStartVotes = numFestivalStartVotes;
                    numFestivalStartVotesRequired = otherPlayers.Count;
                    monitor?.Log($"节日流程: 投票数变化 - {numFestivalStartVotes}/{numFestivalStartVotesRequired}", StardewModdingAPI.LogLevel.Info);
                    return Tuple.Create(numFestivalStartVotes, numFestivalStartVotesRequired);
                }
                
            }
            return null;
        }

        public void EnableFestivalChatBox()
        {
            if (festivalChatBox != null)
            {
                festivalChatBox.Enable();
                // 重置投票数为0，让第一次检查时能检测到变化
                numFestivalStartVotes = 0;
                numFestivalStartVotesRequired = otherPlayers.Count;
                hasLoggedFestivalChatBoxCheck = false; // 重置日志标志
                monitor?.Log($"节日流程: 投票聊天框已启用 - 玩家数: {numFestivalStartVotesRequired}", StardewModdingAPI.LogLevel.Info);
            }
        }
        public void DisableFestivalChatBox()
        {
            if (festivalChatBox != null)
            {
                festivalChatBox.Disable();
                monitor?.Log("节日流程: 投票聊天框已禁用", StardewModdingAPI.LogLevel.Info);
            }
        }
        public void SendChatMessage(string message)
        {
            if (festivalChatBox != null)
            {
                festivalChatBox.SendChatMessage(message);
            }
        }

        public bool IsFestivalChatBoxEnabled()
        {
            return festivalChatBox != null && festivalChatBox.IsEnabled();
        }
        
        // 节日主活动状态管理方法（模仿dsa文件）
        public void EnableFestivalMainEvent()
        {
            festivalMainEventAvailable = true;
            festivalMainEventCountDown = 0;
            monitor?.Log("节日流程: 启用节日主活动等待状态", StardewModdingAPI.LogLevel.Info);
        }
        
        public bool ShouldProcessFestivalMainEvent()
        {
            // 检查是否应该处理节日主活动（模仿dsa的条件检查）
            return festivalMainEventAvailable && Game1.CurrentEvent != null && Game1.CurrentEvent.isFestival;
        }
        
        public bool ShouldTriggerFestivalMainEvent()
        {
            // 增加倒计时，在倒计时结束后触发主活动
            if (ShouldProcessFestivalMainEvent())
            {
                festivalMainEventCountDown += 1;
                
                // 等待3秒后触发主活动（模仿dsa的延迟）
                if (festivalMainEventCountDown >= 180) // 60fps * 3秒 = 180帧
                {
                    return true;
                }
            }
            return false;
        }
        
        public void DisableFestivalMainEvent()
        {
            festivalMainEventAvailable = false;
            festivalMainEventCountDown = 0;
            monitor?.Log("节日流程: 禁用节日主活动状态", StardewModdingAPI.LogLevel.Info);
        }

        public int GetNumOtherPlayers()
        {
            return otherPlayers.Count;
        }
        public IDictionary<long, Farmer> GetOtherPlayers()
        {
            return otherPlayers;
        }
        public void UpdateOtherPlayers()
        {
            if (multiplayer == null)
            {
                multiplayer = (Multiplayer)multiplayerFieldInfo.GetValue(null);
            }
            otherPlayers.Clear();
            foreach (var farmer in Game1.otherFarmers.Values)
            {
                if (!multiplayer.isDisconnecting(farmer))
                {
                    otherPlayers.Add(farmer.UniqueMultiplayerID, farmer);
                }
            }
        }

        public void LogDebug(string s)
        {
            monitor.Log(s, LogLevel.Debug);
        }

        public void NewDay()
        {
            betweenEventsWaitTicks = 0;
            betweenDialoguesWaitTicks = 0;
            betweenShippingMenusWaitTicks = 0;
            checkedForParsnipSeeds = false;
            exitedFarmhouse = false;
            betweenTransitionSleepWaitTicks = 0;
            betweenTransitionFestivalAttendanceWaitTicks = 0;
            betweenTransitionFestivalEndWaitTicks = 0;
            waitTicks = startOfDayWaitTicks;
            numFestivalStartVotes = 0;
            numFestivalStartVotesRequired = otherPlayers.Count;
            hasLoggedFestivalChatBoxCheck = false; // 重置日志标志
            
            // 重置节日主活动状态
            festivalMainEventAvailable = false;
            festivalMainEventCountDown = 0;
        }
    }
}
