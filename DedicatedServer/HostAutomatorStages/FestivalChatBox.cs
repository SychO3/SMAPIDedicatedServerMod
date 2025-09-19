using DedicatedServer.Chat;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DedicatedServer.HostAutomatorStages
{
    internal class FestivalChatBox
    {
        private const string entryMessage = "当您希望开始节日活动时，请在聊天中输入\"开始\"。如果您想取消投票，请输入\"取消\"。";

        private EventDrivenChatBox chatBox;
        private IDictionary<long, Farmer> otherPlayers;
        private bool enabled = false;
        private HashSet<long> votes = new HashSet<long>();

        public FestivalChatBox(EventDrivenChatBox chatBox, IDictionary<long, Farmer> otherPlayers)
        {
            this.chatBox = chatBox;
            this.otherPlayers = otherPlayers;
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
                if (votes.Contains(id))
                {
                    count++;
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
