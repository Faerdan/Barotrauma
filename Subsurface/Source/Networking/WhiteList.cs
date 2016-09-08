﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Barotrauma.Networking
{
    class WhiteListedPlayer
    {
        public string Name;
        public string IP;

        public WhiteListedPlayer(string name,string ip)
        {
            Name = name;
            IP = ip;
        }
    }

    class WhiteList
    {
        const string SavePath = "Data/whitelist.txt";

        private List<WhiteListedPlayer> whitelistedPlayers;
        public List<WhiteListedPlayer> WhiteListedPlayers
        {
            get { return whitelistedPlayers; }
        }

        private GUIComponent whitelistFrame;

        private GUITextBox nameBox;
        private GUITextBox ipBox;

        public bool Enabled;

        public WhiteList()
        {
            Enabled = false;
            whitelistedPlayers = new List<WhiteListedPlayer>();

            if (File.Exists(SavePath))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(SavePath);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Failed to open whitelist in " + SavePath, e);
                    return;
                }

                foreach (string line in lines)
                {
                    if (line[0] == '#')
                    {
                        string lineval = line.Substring(1, line.Length - 1);
                        int intVal = 0;
                        Int32.TryParse(lineval, out intVal);
                        if (lineval.ToLower() == "true" || intVal != 0)
                        {
                            Enabled = true;
                        }
                        else
                        {
                            Enabled = false;
                        }
                    }
                    else
                    {
                        string[] separatedLine = line.Split(',');
                        if (separatedLine.Length < 2) continue;

                        string name = String.Join(",", separatedLine.Take(separatedLine.Length - 1));
                        string ip = separatedLine.Last();

                        whitelistedPlayers.Add(new WhiteListedPlayer(name, ip));
                    }
                }
            }
        }

        public void Save()
        {
            GameServer.Log("Saving whitelist", null);

            List<string> lines = new List<string>();

            if (Enabled)
            {
                lines.Add("#true");
            }
            else
            {
                lines.Add("#false");
            }
            foreach (WhiteListedPlayer wlp in whitelistedPlayers)
            {
                lines.Add(wlp.Name + "," + wlp.IP);
            }

            try
            {
                File.WriteAllLines(SavePath, lines);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving the whitelist to " + SavePath + " failed", e);
            }
        }

        public bool IsWhiteListed(string name, string ip)
        {
            if (!Enabled) return true;
            WhiteListedPlayer wlp = whitelistedPlayers.Find(p => p.Name == name);
            if (wlp == null) return false;
            if (wlp.IP != ip && !string.IsNullOrWhiteSpace(wlp.IP)) return false;
            return true;
        }

        public GUIComponent CreateWhiteListFrame(GUIComponent parent)
        {
            if (whitelistFrame!=null)
            {
                whitelistFrame.Parent.ClearChildren();
                whitelistFrame = null;
            }

            parent.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);

            var enabledTick = new GUITickBox(new Rectangle(0, 0, 20, 20), "Enabled", Alignment.TopLeft, parent);
            enabledTick.Selected = Enabled;
            enabledTick.OnSelected = (GUITickBox box) =>
            {
                Enabled = !Enabled;

                if (Enabled)
                {
                    foreach (Client c in GameMain.Server.ConnectedClients)
                    {
                        if (!IsWhiteListed(c.name,c.Connection.RemoteEndPoint.Address.ToString()))
                        {
                            whitelistedPlayers.Add(new WhiteListedPlayer(c.name, c.Connection.RemoteEndPoint.Address.ToString()));
                            if (whitelistFrame != null) CreateWhiteListFrame(whitelistFrame.Parent);
                        }
                    }
                }
                
                Save();
                return true;
            };

            new GUITextBlock(new Rectangle(0, -25, 90, 20), "Name:", GUI.Style, Alignment.BottomLeft, Alignment.TopLeft, parent, false, GUI.Font);
            nameBox = new GUITextBox(new Rectangle(100, -25, 170, 20), Alignment.BottomLeft, GUI.Style, parent);
            nameBox.Font = GUI.Font;

            new GUITextBlock(new Rectangle(0, 5, 90, 20), "IP Address:", GUI.Style, Alignment.BottomLeft, Alignment.TopLeft, parent, false, GUI.Font);
            ipBox = new GUITextBox(new Rectangle(100, 5, 170, 20), Alignment.BottomLeft, GUI.Style, parent);
            ipBox.Font = GUI.Font;

            var addnewButton = new GUIButton(new Rectangle(0, 45, 150, 20), "Add to whitelist", Alignment.BottomLeft, GUI.Style, parent);
            addnewButton.OnClicked = AddToWhiteList;

            whitelistFrame = new GUIListBox(new Rectangle(0, 30, 0, parent.Rect.Height-100), GUI.Style, parent);

            foreach (WhiteListedPlayer wlp in whitelistedPlayers)
            {
                string blockText = wlp.Name;
                if (!string.IsNullOrWhiteSpace(wlp.IP)) blockText += " (" + wlp.IP + ")";
                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(0, 0, 0, 25),
                    blockText,
                    GUI.Style,
                    Alignment.Left, Alignment.Left, whitelistFrame);
                textBlock.Padding = new Vector4(10.0f, 10.0f, 0.0f, 0.0f);
                textBlock.UserData = wlp;

                var removeButton = new GUIButton(new Rectangle(0, 0, 100, 20), "Remove", Alignment.Right | Alignment.CenterY, GUI.Style, textBlock);
                removeButton.UserData = wlp;
                removeButton.OnClicked = RemoveFromWhiteList;
            }

            return parent;
        }

        private bool RemoveFromWhiteList(GUIButton button, object obj)
        {
            WhiteListedPlayer wlp = obj as WhiteListedPlayer;
            if (wlp == null) return false;

            DebugConsole.Log("Removing " + wlp.Name + " from whitelist");
            GameServer.Log("Removing " + wlp.Name + " from whitelist", null);

            whitelistedPlayers.Remove(wlp);
            Save();
            
            if (whitelistFrame != null)
            {
                whitelistFrame.Parent.ClearChildren();
                CreateWhiteListFrame(whitelistFrame.Parent);
            }

            return true;
        }

        private bool AddToWhiteList(GUIButton button, object obj)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return false;
            if (whitelistedPlayers.Any(x => x.Name.ToLower() == nameBox.Text.ToLower() && x.IP == ipBox.Text)) return false;
            whitelistedPlayers.Add(new WhiteListedPlayer(nameBox.Text,ipBox.Text));
            Save();

            if (whitelistFrame != null)
            {
                CreateWhiteListFrame(whitelistFrame.Parent);
            }
            return true;
        }
    }
}