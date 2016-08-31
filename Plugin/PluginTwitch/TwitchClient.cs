﻿using System;
using System.Linq;
using System.Text;
using IrcDotNet;
using System.Threading;
using System.Diagnostics;

using System.Collections.Generic;
using System.IO;
using System.Net;

namespace PluginTwitch
{
    public class TwitchClient
    {


        public String String { get; private set;  }
        public String Channel { get; private set; }
        public int ImageWidth { get { return messageParser.ImageWidth; } }
        public int ImageHeight { get { return messageParser.ImageHeight; } }

        private TwitchIrcClient client;
        private MessageParser messageParser;
        private ImageDownloader imgDownloader;
        private String username;
        private String ouath;
        private bool isConnected;

        public TwitchClient(string username, string ouath, MessageParser messageParser, ImageDownloader imgDownloader)
        {
            isConnected = false;
            client = new TwitchIrcClient();
            Channel = "";
            this.username = username;
            this.ouath = ouath;
            this.messageParser = messageParser;
            this.imgDownloader = imgDownloader;
        }

        public void Connect()
        {
            if (isConnected)
                return;

            String = string.Format("Starting to connect to twitch as {0}.", username);
            var server = "irc.twitch.tv";
            client.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
            client.Registered += IrcClient_Registered;
            // Wait until connection has succeeded or timed out.
            var waitTime = 2000;
            using (var registeredEvent = new ManualResetEventSlim(false))
            {
                using (var connectedEvent = new ManualResetEventSlim(false))
                {
                    client.Connected += (sender2, e2) => connectedEvent.Set();
                    client.Registered += (sender2, e2) => registeredEvent.Set();
                    client.Connect(server, false,
                        new IrcUserRegistrationInfo()
                        {
                            NickName = username,
                            Password = ouath,
                            UserName = username
                        });
                    if (!connectedEvent.Wait(waitTime))
                    {
                        String = string.Format("Connection to Twitch timed out.", server);
                        return;
                    }
                }
                if (!registeredEvent.Wait(waitTime))
                {
                    String = string.Format("Could not register to Twitch. Did provide a user name and Ouath?", server);
                    return;
                }
                isConnected = true;
                client.SendRawMessage("CAP REQ :twitch.tv/tags");
            }
        }

        public bool IsInChannel()
        {
            return Channel != "";
        }

        public void JoinChannel(string channel)
        {
            if (!isConnected)
                return;

            messageParser.Reset();
            String = "";
            Channel = channel;
            foreach (var c in client.Channels)
                client.Channels.Leave(c.Name);
            client.Channels.Join(Channel);
            imgDownloader.DownloadBadges(channel);
        }

        public void LeaveChannel()
        {
            if (!isConnected)
                return;

            messageParser.Reset();
            String = "";
            foreach(var c in client.Channels)
                client.Channels.Leave(c.Name);
            Channel = "";
        }

        public void Disconnect()
        {
            if (!isConnected)
                return;

            client.Disconnect();
            isConnected = false;
        }

        public Image GetImage(int index)
        {
            var emotes = messageParser.Images;
            if (index >= emotes.Count)
                return null;

            return emotes[index];
        }

        public void SendMessage(string msg)
        {
            client.SendPrivateMessage(new String[] { Channel }, msg);
        }

        private void IrcClient_Registered(object sender, EventArgs e)
        {
            var client = (IrcClient)sender;

            client.LocalUser.JoinedChannel += IrcClient_LocalUser_JoinedChannel;
        }

        private void IrcClient_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            var localUser = (IrcLocalUser)sender;

            e.Channel.MessageReceived += IrcClient_Channel_MessageReceived;
            String = string.Format("Joined the channel {0}.", e.Channel.Name);
        }

        private void IrcClient_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            var channel = (IrcChannel)sender;
            lock (String)
            {
               String = messageParser.AddMessage(e.Source.Name, e.Text, e.Tags);
            }
        }

    }
}
