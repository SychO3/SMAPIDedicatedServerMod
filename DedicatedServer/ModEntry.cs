using DedicatedServer.Config;
using DedicatedServer.ConsoleCommands;
using DedicatedServer.HostAutomatorStages;
using DedicatedServer.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace DedicatedServer
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        // TODO ModConfig value checking. But perhaps this actually should be done in the SelectFarmStage; if the
        // farm with the name given by the config exists, then none of the rest of the config values really matter,
        // except for the bat / mushroom decision and the pet name (the parts accessed mid-game rather than just at
        // farm creation).

        // TODO Add more config options, like the ability to disable the crop saver (perhaps still keep track of crops
        // in case it's enabled later, but don't alter them).

        // TODO Remove player limit (if the existing attempts haven't already succeeded in doing that).

        // TODO Make the host invisible to everyone else

        // TODO Consider what the automated host should do when another player proposes to them.

        private WaitCondition titleMenuWaitCondition;
        private ModConfig config;
        private IModHelper helper;
        private TimeControlCommand timeControlCommand;
        private InactivePlayerKicker inactivePlayerKicker;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.helper = helper;
            this.config = helper.ReadConfig<ModConfig>();

            // 初始化并注册时间控制命令
            this.timeControlCommand = new TimeControlCommand(helper, Monitor);
            this.timeControlCommand.RegisterCommands();

            // 初始化不活跃玩家踢出功能
            this.inactivePlayerKicker = new InactivePlayerKicker(helper, Monitor, config);
            
            // 注册控制台命令
            helper.ConsoleCommands.Add("kick_status", "显示玩家活动状态", (command, args) =>
            {
                inactivePlayerKicker.ShowPlayerStatus();
            });

            helper.ConsoleCommands.Add("kick_enable", "启用不活跃玩家踢出功能", (command, args) =>
            {
                config.EnableInactivePlayerKick = true;
                helper.WriteConfig(config);
                inactivePlayerKicker.Disable();
                inactivePlayerKicker = new InactivePlayerKicker(helper, Monitor, config);
                inactivePlayerKicker.Enable();
                Monitor.Log("不活跃玩家踢出功能已启用", LogLevel.Info);
            });

            helper.ConsoleCommands.Add("kick_disable", "禁用不活跃玩家踢出功能", (command, args) =>
            {
                config.EnableInactivePlayerKick = false;
                helper.WriteConfig(config);
                inactivePlayerKicker.Disable();
                Monitor.Log("不活跃玩家踢出功能已禁用", LogLevel.Info);
            });

            helper.ConsoleCommands.Add("kick_timeout", "设置不活跃踢出时间(分钟) - 用法: kick_timeout <分钟>", (command, args) =>
            {
                if (args.Length != 1 || !int.TryParse(args[0], out int minutes) || minutes <= 0)
                {
                    Monitor.Log("用法: kick_timeout <分钟> (必须是正整数)", LogLevel.Info);
                    return;
                }

                config.InactiveKickTimeMinutes = minutes;
                helper.WriteConfig(config);
                
                // 重新初始化以应用新设置
                inactivePlayerKicker.Disable();
                inactivePlayerKicker = new InactivePlayerKicker(helper, Monitor, config);
                if (config.EnableInactivePlayerKick)
                {
                    inactivePlayerKicker.Enable();
                }
                
                Monitor.Log($"不活跃踢出时间已设置为 {minutes} 分钟", LogLevel.Info);
            });

            // Ensure that the game environment is in a stable state before the mod starts executing
            // Without a waiting time, an invitation code is almost never generated; with a waiting
            // time of 1 second, it is very rare that no more codes are generated
            this.titleMenuWaitCondition = new WaitCondition(
                () => Game1.activeClickableMenu is StardewValley.Menus.TitleMenu,
                60);

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        /// <summary>
        /// Event handler to wait until a specific condition is met before executing.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (this.titleMenuWaitCondition.IsMet())
            {
                helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                new StartFarmStage(this.Helper, Monitor, config).Enable();
                
                // 启用不活跃玩家踢出功能
                inactivePlayerKicker.Enable();
            }
        }

        /// <summary>
        ///         Represents wait condition.
        /// <br/>   
        /// <br/>   First waits until the condition is met and then waits a certain number of update cycles
        /// </summary>
        private class WaitCondition
        {
            private readonly System.Func<bool> condition;
            private int waitCounter;

            public WaitCondition(System.Func<bool> condition, int initialWait)
            {
                this.condition = condition;
                this.waitCounter = initialWait;
            }

            public bool IsMet()
            {
                if (this.condition())
                {
                    this.waitCounter--;
                }

                if (0 >= this.waitCounter)
                {
                    return true;
                }

                return false;

            }
        }
    }
}
