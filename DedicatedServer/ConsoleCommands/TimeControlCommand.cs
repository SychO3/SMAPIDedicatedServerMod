using StardewModdingAPI;
using StardewValley;
using System;
using System.Globalization;

namespace DedicatedServer.ConsoleCommands
{
    /// <summary>控制游戏时间的控制台命令</summary>
    internal class TimeControlCommand
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;

        public TimeControlCommand(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
        }

        /// <summary>注册时间控制命令</summary>
        public void RegisterCommands()
        {
            helper.ConsoleCommands.Add("settime", 
                "设置当前时间.\n\n用法: settime <时:分>\n- 时间格式: HH:mm (24小时制)\n例如: settime 14:30", 
                this.SetTime);

            helper.ConsoleCommands.Add("addtime", 
                "增加/减少时间.\n\n用法: addtime <分钟数>\n- 分钟数: 正数增加时间，负数减少时间\n例如: addtime 30 (增加30分钟)\n      addtime -15 (减少15分钟)", 
                this.AddTime);

            helper.ConsoleCommands.Add("pausetime", 
                "暂停/恢复时间流逝.\n\n用法: pausetime", 
                this.PauseTime);

            helper.ConsoleCommands.Add("gettime", 
                "获取当前游戏时间.\n\n用法: gettime", 
                this.GetTime);

            helper.ConsoleCommands.Add("setday", 
                "设置当前日期.\n\n用法: setday <天数>\n- 天数: 从游戏开始的第几天\n例如: setday 15", 
                this.SetDay);

            helper.ConsoleCommands.Add("setseason", 
                "设置当前季节.\n\n用法: setseason <季节>\n- 季节: spring, summer, fall, winter\n例如: setseason summer", 
                this.SetSeason);

            helper.ConsoleCommands.Add("setyear", 
                "设置当前年份.\n\n用法: setyear <年份>\n- 年份: 游戏年份数字\n例如: setyear 3", 
                this.SetYear);
        }

        /// <summary>设置时间</summary>
        private void SetTime(string command, string[] args)
        {
            if (args.Length != 1)
            {
                monitor.Log("用法: settime <时:分>", LogLevel.Error);
                return;
            }

            if (!TimeSpan.TryParseExact(args[0], @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
            {
                monitor.Log("时间格式错误，请使用 HH:mm 格式 (例如: 14:30)", LogLevel.Error);
                return;
            }

            // 直接使用小时和分钟构造HHMM格式
            int gameTime = time.Hours * 100 + time.Minutes;

            // 星露谷时间范围：6:00 AM (600) 到次日 2:00 AM (2600)
            // 但如果输入的是0-5点，视为次日凌晨
            if (gameTime < 600 && gameTime >= 0 && gameTime <= 200)
            {
                gameTime += 2400; // 转换为次日时间格式 (如: 100 -> 2500)
            }
            
            if (gameTime < 600 || gameTime > 2600)
            {
                monitor.Log("时间必须在 6:00 到 26:00 (次日2:00) 之间", LogLevel.Error);
                return;
            }

            Game1.timeOfDay = gameTime;
            monitor.Log($"时间已设置为 {time:hh\\:mm}", LogLevel.Info);
        }

        /// <summary>增加/减少时间</summary>
        private void AddTime(string command, string[] args)
        {
            if (args.Length != 1)
            {
                monitor.Log("用法: addtime <分钟数>\n- 分钟数: 正数增加时间，负数减少时间\n例如: addtime 30 或 addtime -15", LogLevel.Error);
                return;
            }

            if (!int.TryParse(args[0], out int minutes))
            {
                monitor.Log("请输入有效的分钟数", LogLevel.Error);
                return;
            }

            // 解析当前时间
            int currentHours = Game1.timeOfDay / 100;
            int currentMinutes = Game1.timeOfDay % 100;
            
            // 计算新时间
            int newMinutes = currentMinutes + minutes;
            int newHours = currentHours;
            
            // 处理分钟进位（正数）
            while (newMinutes >= 60)
            {
                newMinutes -= 60;
                newHours++;
            }
            
            // 处理分钟借位（负数）
            while (newMinutes < 0)
            {
                newMinutes += 60;
                newHours--;
            }
            
            // 构造新的时间值 (HHMM格式)
            int newTimeOfDay = newHours * 100 + newMinutes;
            
            // 限制在有效范围内 (6:00 AM 到次日 2:00 AM)
            if (newTimeOfDay < 600)
            {
                newTimeOfDay = 600;
            }
            else if (newTimeOfDay > 2600)
            {
                newTimeOfDay = 2600;
            }
            
            Game1.timeOfDay = newTimeOfDay;
            string action = minutes >= 0 ? "增加" : "减少";
            monitor.Log($"时间{action}了 {Math.Abs(minutes)} 分钟", LogLevel.Info);
        }

        /// <summary>暂停/恢复时间</summary>
        private void PauseTime(string command, string[] args)
        {
            Game1.netWorldState.Value.IsPaused = !Game1.netWorldState.Value.IsPaused;
            string status = Game1.netWorldState.Value.IsPaused ? "暂停" : "恢复";
            monitor.Log($"时间流逝已{status}", LogLevel.Info);
        }

        /// <summary>获取当前时间</summary>
        private void GetTime(string command, string[] args)
        {
            int hours = Game1.timeOfDay / 100;
            int minutes = Game1.timeOfDay % 100;
            string timeString = $"{hours:00}:{minutes:00}";

            monitor.Log($"当前游戏时间: {timeString}", LogLevel.Info);
            monitor.Log($"第 {Game1.dayOfMonth} 天, {Game1.currentSeason}, 第 {Game1.year} 年", LogLevel.Info);
            monitor.Log($"时间流逝: {(!Game1.netWorldState.Value.IsPaused ? "开启" : "暂停")}", LogLevel.Info);
        }

        /// <summary>设置日期</summary>
        private void SetDay(string command, string[] args)
        {
            if (args.Length != 1)
            {
                monitor.Log("用法: setday <天数>", LogLevel.Error);
                return;
            }

            if (!int.TryParse(args[0], out int day) || day < 1 || day > 28)
            {
                monitor.Log("天数必须在 1-28 之间", LogLevel.Error);
                return;
            }

            Game1.dayOfMonth = day;
            monitor.Log($"日期已设置为第 {day} 天", LogLevel.Info);
        }

        /// <summary>设置季节</summary>
        private void SetSeason(string command, string[] args)
        {
            if (args.Length != 1)
            {
                monitor.Log("用法: setseason <季节>", LogLevel.Error);
                return;
            }

            string season = args[0].ToLower();
            string[] validSeasons = { "spring", "summer", "fall", "winter" };

            if (Array.IndexOf(validSeasons, season) == -1)
            {
                monitor.Log("有效季节: spring, summer, fall, winter", LogLevel.Error);
                return;
            }

            Game1.currentSeason = season;
            monitor.Log($"季节已设置为 {season}", LogLevel.Info);
        }

        /// <summary>设置年份</summary>
        private void SetYear(string command, string[] args)
        {
            if (args.Length != 1)
            {
                monitor.Log("用法: setyear <年份>", LogLevel.Error);
                return;
            }

            if (!int.TryParse(args[0], out int year) || year < 1)
            {
                monitor.Log("年份必须是大于0的数字", LogLevel.Error);
                return;
            }

            Game1.year = year;
            monitor.Log($"年份已设置为第 {year} 年", LogLevel.Info);
        }
    }
}
