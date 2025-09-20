using DedicatedServer.HostAutomatorStages;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DedicatedServer.Utils
{
    internal class Festivals
    {
        private static int getFestivalEndTime()
        {
            if (Game1.weatherIcon == 1)
            {
                return Convert.ToInt32(Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + Game1.currentSeason + Game1.dayOfMonth)["conditions"].Split('/')[1].Split(' ')[1]);
            }

            return -1;
        }
        public static bool IsWaitingToAttend()
        {
            return ReadyCheckHelper.IsReady("festivalStart", Game1.player);
        }
        public static bool OthersWaitingToAttend(int numOtherPlayers)
        {
            return HostAutomatorStages.ReadyCheckHelper.GetNumberReady("festivalStart") == (numOtherPlayers + (IsWaitingToAttend() ? 1 : 0));
        }
        private static bool isTodayBeachNightMarket()
        {
            return Game1.currentSeason.Equals("winter") && Game1.dayOfMonth >= 15 && Game1.dayOfMonth <= 17;
        }
        public static bool ShouldAttend(int numOtherPlayers)
        {
            bool isFestivalDay = Utility.isFestivalDay(Game1.dayOfMonth, Game1.season);
            bool hasOtherPlayers = numOtherPlayers > 0;
            bool othersWaiting = OthersWaitingToAttend(numOtherPlayers);
            bool isBeachNightMarket = isTodayBeachNightMarket();
            bool isTimeCorrect = Game1.timeOfDay >= Utility.getStartTimeOfFestival() && Game1.timeOfDay <= getFestivalEndTime();
            
            bool shouldAttend = hasOtherPlayers && othersWaiting && isFestivalDay && !isBeachNightMarket && isTimeCorrect;
            
            // 暂时注释掉详细日志，避免编译错误
            // if (isFestivalDay && hasOtherPlayers)
            // {
            //     // 只在节日当天且有其他玩家时记录详细信息
            //     Monitor?.Log($"节日判断: 节日日期={isFestivalDay}, 有其他玩家={hasOtherPlayers}, 其他玩家等待={othersWaiting}, 海滩夜市={isBeachNightMarket}, 时间正确={isTimeCorrect}, 应参加={shouldAttend}", StardewModdingAPI.LogLevel.Debug);
            // }
            
            return shouldAttend;
        }

        public static bool IsWaitingToLeave()
        {
            return ReadyCheckHelper.IsReady("festivalEnd", Game1.player);
        }
        public static bool OthersWaitingToLeave(int numOtherPlayers)
        {
            return HostAutomatorStages.ReadyCheckHelper.GetNumberReady("festivalEnd") == (numOtherPlayers + (IsWaitingToLeave() ? 1 : 0));
        }
        public static bool ShouldLeave(int numOtherPlayers)
        {
            return Game1.isFestival() && OthersWaitingToLeave(numOtherPlayers);
        }
    }
}
