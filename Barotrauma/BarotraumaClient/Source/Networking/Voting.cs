﻿using Barotrauma.Networking;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Barotrauma
{
    partial class Voting
    {
        public bool AllowSubVoting
        {
            get { return allowSubVoting; }
            set
            {
                if (value == allowSubVoting) return;
                allowSubVoting = value;
                GameMain.NetLobbyScreen.SubList.Enabled = value || GameMain.Server != null;
                GameMain.NetLobbyScreen.InfoFrame.FindChild("subvotes").Visible = value;

                if (GameMain.Server != null)
                {
                    UpdateVoteTexts(GameMain.Server.ConnectedClients, VoteType.Sub);
                    GameMain.Server.UpdateVoteStatus();
                }
                else
                {
                    GameMain.NetLobbyScreen.SubList.Deselect();
                }
            }
        }
        public bool AllowModeVoting
        {
            get { return allowModeVoting; }
            set
            {
                if (value == allowModeVoting) return;
                allowModeVoting = value;
                GameMain.NetLobbyScreen.ModeList.Enabled = value || GameMain.Server != null;
                GameMain.NetLobbyScreen.InfoFrame.FindChild("modevotes").Visible = value;
                if (GameMain.Server != null)
                {
                    UpdateVoteTexts(GameMain.Server.ConnectedClients, VoteType.Mode);
                    GameMain.Server.UpdateVoteStatus();
                }
                else
                {
                    GameMain.NetLobbyScreen.ModeList.Deselect();
                }
            }
        }

        public void UpdateVoteTexts(List<Client> clients, VoteType voteType)
        {
            GUIListBox listBox = (voteType == VoteType.Sub) ?
                GameMain.NetLobbyScreen.SubList : GameMain.NetLobbyScreen.ModeList;

            foreach (GUIComponent comp in listBox.children)
            {
                GUITextBlock voteText = comp.FindChild("votes") as GUITextBlock;
                if (voteText != null) comp.RemoveChild(voteText);
            }

            List<Pair<object, int>> voteList = GetVoteList(voteType, clients);
            foreach (Pair<object, int> votable in voteList)
            {
                SetVoteText(listBox, votable.First, votable.Second);
            }
        }

        private void SetVoteText(GUIListBox listBox, object userData, int votes)
        {
            if (userData == null) return;
            foreach (GUIComponent comp in listBox.children)
            {
                if (comp.UserData != userData) continue;
                GUITextBlock voteText = comp.FindChild("votes") as GUITextBlock;
                if (voteText == null)
                {
                    voteText = new GUITextBlock(new Rectangle(0, 0, 30, 0), "", "", Alignment.Right, Alignment.Right, comp);
                    voteText.UserData = "votes";
                }

                voteText.Text = votes == 0 ? "" : votes.ToString();
            }
        }

        public void ClientWrite(NetBuffer msg, VoteType voteType, object data)
        {
            if (GameMain.Server != null) return;

            msg.Write((byte)voteType);

            switch (voteType)
            {
                case VoteType.Sub:
                    Submarine sub = data as Submarine;
                    if (sub == null) return;

                    msg.Write(sub.Name);
                    break;
                case VoteType.Mode:
                    GameModePreset gameMode = data as GameModePreset;
                    if (gameMode == null) return;

                    msg.Write(gameMode.Name);
                    break;
                case VoteType.EndRound:
                    if (!(data is bool)) return;

                    msg.Write((bool)data);
                    break;
                case VoteType.Kick:
                    Client votedClient = data as Client;
                    if (votedClient == null) return;

                    msg.Write(votedClient.ID);
                    break;
            }

            msg.WritePadBits();
        }
        
        public void ClientRead(NetIncomingMessage inc)
        {
            if (GameMain.Server != null) return;

            AllowSubVoting = inc.ReadBoolean();
            if (allowSubVoting)
            {
                foreach (Submarine sub in Submarine.SavedSubmarines)
                {
                    SetVoteText(GameMain.NetLobbyScreen.SubList, sub, 0);
                }
                int votableCount = inc.ReadByte();
                for (int i = 0; i < votableCount; i++)
                {
                    int votes = inc.ReadByte();
                    string subName = inc.ReadString();
                    Submarine sub = Submarine.SavedSubmarines.Find(sm => sm.Name == subName);
                    SetVoteText(GameMain.NetLobbyScreen.SubList, sub, votes);
                }
            }
            AllowModeVoting = inc.ReadBoolean();
            if (allowModeVoting)
            {
                int votableCount = inc.ReadByte();
                for (int i = 0; i < votableCount; i++)
                {
                    int votes = inc.ReadByte();
                    string modeName = inc.ReadString();
                    GameModePreset mode = GameModePreset.list.Find(m => m.Name == modeName);
                    SetVoteText(GameMain.NetLobbyScreen.ModeList, mode, votes);
                }
            }
            AllowEndVoting = inc.ReadBoolean();
            if (AllowEndVoting)
            {
                GameMain.NetworkMember.EndVoteCount = inc.ReadByte();
                GameMain.NetworkMember.EndVoteMax = inc.ReadByte();
            }
            AllowVoteKick = inc.ReadBoolean();

            inc.ReadPadBits();
        }
    }
}
