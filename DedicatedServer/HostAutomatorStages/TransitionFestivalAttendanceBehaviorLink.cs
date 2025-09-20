using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DedicatedServer.HostAutomatorStages
{
    internal class TransitionFestivalAttendanceBehaviorLink : BehaviorLink
    {
        private static MethodInfo info = typeof(Game1).GetMethod("performWarpFarmer", BindingFlags.Static | BindingFlags.NonPublic);
        public TransitionFestivalAttendanceBehaviorLink(BehaviorLink next = null) : base(next)
        {
        }

        private static string getLocationOfFestival()
        {
            if (Game1.weatherIcon == 1)
            {
                return Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + Game1.currentSeason + Game1.dayOfMonth)["conditions"].Split('/')[0];
            }

            return null;
        }

        public override void Process(BehaviorState state)
        {
            if (Utils.Festivals.ShouldAttend(state.GetNumOtherPlayers()) && !Utils.Festivals.IsWaitingToAttend())
            {
                state.LogDebug($"节日流程: 房主准备参加节日 - 玩家数: {state.GetNumOtherPlayers()}");
                if (state.HasBetweenTransitionFestivalAttendanceWaitTicks())
                {
                    state.DecrementBetweenTransitionFestivalAttendanceWaitTicks();
                } else
                {
                    var location = Game1.getLocationFromName(getLocationOfFestival());
                    var warp = new Warp(0, 0, location.NameOrUniqueName, 0, 0, false);
                    ReadyCheckHelper.SetLocalReady("festivalStart", true);
                    Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", allowCancel: true, delegate (Farmer who)
                    {
                        state.LogDebug($"节日流程: 房主确认参加节日，传送到 {warp.TargetName}");
                        Game1.exitActiveMenu();
                        info.Invoke(null, new object[] { Game1.getLocationRequest(warp.TargetName), 0, 0, Game1.player.facingDirection.Value });
                        if ((Game1.currentSeason != "fall" || Game1.dayOfMonth != 27) && (Game1.currentSeason != "winter" || Game1.dayOfMonth != 25)) // Don't enable chat box on spirit's eve nor feast of the winter star
                        {
                            state.LogDebug("节日流程: 启用节日投票聊天框");
                            state.EnableFestivalChatBox();
                        }
                        else
                        {
                            state.LogDebug("节日流程: 跳过投票（万圣节或冬日盛宴）");
                        }
                    });
                    state.WaitForFestivalAttendance();
                }
            } else if (!Utils.Festivals.ShouldAttend(state.GetNumOtherPlayers()) && Utils.Festivals.IsWaitingToAttend())
            {
                if (state.HasBetweenTransitionFestivalAttendanceWaitTicks())
                {
                    state.DecrementBetweenTransitionFestivalAttendanceWaitTicks();
                } else
                {
                    if (Game1.activeClickableMenu != null && Game1.activeClickableMenu is ReadyCheckDialog rcd)
                    {
                        rcd.closeDialog(Game1.player);
                    }
                    ReadyCheckHelper.SetLocalReady("festivalStart", false);
                    state.StopWaitingForFestivalAttendance();
                }
            }
            else
            {
                state.ClearBetweenTransitionFestivalAttendanceWaitTicks();
                processNext(state);
            }
        }
    }
}
