using DedicatedServer.Chat;
using StardewValley;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DedicatedServer.HostAutomatorStages
{
    internal class FestivalChatBox
    {
        private const string entryMessage = "当您希望开始节日活动时，请在聊天中输入\"开始\"。如果您想取消投票，请输入\"取消\"。（手机玩家将自动同意）";

        private EventDrivenChatBox chatBox;
        private IDictionary<long, Farmer> otherPlayers;
        private IModHelper helper;
        private bool enabled = false;
        private HashSet<long> votes = new HashSet<long>();
        private HashSet<long> loggedMobilePlayers = new HashSet<long>(); // 已记录日志的手机玩家

        public FestivalChatBox(EventDrivenChatBox chatBox, IDictionary<long, Farmer> otherPlayers, IModHelper helper)
        {
            this.chatBox = chatBox;
            this.otherPlayers = otherPlayers;
            this.helper = helper;
        }

        public bool IsEnabled()
        {
            return enabled;
        }

        public void Enable()
        {
            if (!enabled)
            {
                enabled = true;
                votes.Clear();
                loggedMobilePlayers.Clear(); // 重置手机玩家日志标志
                chatBox.textBoxEnter(entryMessage);
                chatBox.ChatReceived += onChatReceived;
            }
        }

        public void Disable()
        {
            if (enabled)
            {
                enabled = false;
                votes.Clear();
                loggedMobilePlayers.Clear(); // 重置手机玩家日志标志
                chatBox.ChatReceived -= onChatReceived;
            }
        }

        private void onChatReceived(object sender, ChatEventArgs e)
        {
            if (!otherPlayers.ContainsKey(e.SourceFarmerId))
            {
                return;
            }

            if (e.Message.ToLower() == "开始")
            {
                votes.Add(e.SourceFarmerId);
            }
            else if (e.Message.ToLower() == "取消")
            {
                votes.Remove(e.SourceFarmerId);
            }
        }

        public int NumVoted()
        {
            int count = 0;
            foreach (var id in otherPlayers.Keys)
            {
                // 检查玩家是否通过聊天投票了"开始"
                if (votes.Contains(id))
                {
                    count++;
                    continue;
                }
                
                // 检查是否是手机玩家（没有SMAPI的玩家自动视为同意）
                var peer = helper.Multiplayer.GetConnectedPlayer(id);
                if (peer != null && !peer.HasSmapi)
                {
                    count++; // 手机玩家自动同意
                    
                    // 只为每个手机玩家记录一次日志，避免刷屏
                    if (!loggedMobilePlayers.Contains(id))
                    {
                        var farmer = otherPlayers[id];
                        chatBox.textBoxEnter($"检测到手机玩家 {farmer.Name}，自动同意节日活动");
                        loggedMobilePlayers.Add(id);
                    }
                }
            }
            return count;
        }

        public void SendChatMessage(string message)
        {
            chatBox.textBoxEnter(message);
        }
    }
}
