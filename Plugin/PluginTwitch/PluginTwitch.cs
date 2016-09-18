﻿using System;
using System.Runtime.InteropServices;
using Rainmeter;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Automation;


namespace PluginTwitchChat
{

    internal class Measure
    {
        static readonly string MissingImage = "_empty";

        static TwitchClient twitchClient = null;
        static MessageHandler messageHandler = null;
        static WebBrowserURLLocator urlLocator = null;
        static StringMeasurer stringMeasurer = null;

        string StringValue;
        string tpe = "";
        string channelString = "";
        Func<double> update;

        internal void Reload(API api, ref double maxValue)
        {
            tpe = api.ReadString("Type", "");
            switch (tpe)
            {
                case "Main":
                    StringValue = "";
                    ReloadMain(api);
                    break;
                case "AutoConnector":
                    ReloadAutoConnector(api);
                    break;
            }

            update = GetUpdateFunction();
        }

        internal void ReloadAutoConnector(API api)
        {
            if (api.ReadDouble("ConnectAutomatically", 0.0) != 1.0)
                return;

            if (urlLocator == null)
            {
                string webBrowser = api.ReadString("Browser", "").ToLower();
                switch (webBrowser)
                {
                    case "chrome":
                        urlLocator = new ChromeURLLocator();
                        break;
                    default: return; // TODO: Support other browsers
                }
            }
            var channel = urlLocator.TwitchChannel;
            if (channel != null)
                twitchClient.JoinChannel(channel);
        }

        internal void ReloadMain(API api)
        {
            if (twitchClient != null)
                return;

            string user = api.ReadString("Username", "").ToLower();
            string ouath = api.ReadString("Ouath", "");
            string fontFace = api.ReadString("FontFace", "");
            string imageDir = api.ReadString("ImageDir", "");
            bool useSeperator = api.ReadInt("UseSeperator", 1) == 1;
            bool useBetterTTV = api.ReadInt("UseBetterTTVEmotes", 1) == 1;
            int width = api.ReadInt("Width", 500);
            int height = api.ReadInt("Height", 500);
            int fontSize = api.ReadInt("FontSize", 16);
            int imageQuality = Clamp(api.ReadInt("ImageQuality", 1), 1, 3);

            if(user == "")
            {
                StringValue = "User name is missing in settings files Variables.inc.";
                return;
            }

            if (ouath == "")
            {
                StringValue = "Ouath is missing in settings files Variables.inc.";
                return;
            }

            if (fontFace == "" || imageDir == "")
            {
                StringValue = "Missing settings FontFace, ImageDir or FontSize.";
                return;
            }

            var size = new Size(width, height);
            var font = new Font(fontFace, fontSize);
            var imgDownloader = new ImageDownloader(imageDir, imageQuality);
            stringMeasurer = new StringMeasurer(font);

            var settings = new MessageHandlerSettings { UseBetterTTVEmotes = useBetterTTV, MaxSize = size, UseSeperator = useSeperator };
            messageHandler = new MessageHandler(settings, stringMeasurer, imgDownloader);
            twitchClient = new TwitchClient(user, ouath, messageHandler, imgDownloader);
        }

        internal int Clamp(int v, int min, int max)
        {
            return v < min ? min : v > max ? max : v;
        }

        internal void Cleanup()
        {
            twitchClient?.Disconnect();
            twitchClient = null;
            stringMeasurer?.Dispose();
        }

        internal double Update()
        {
            if (twitchClient == null)
                return 0.0;

            return update();
        }

        internal Func<double> GetUpdateFunction()
        {
            switch (tpe)
            {
                case "ChannelName":
                    StringValue = "";
                    return () =>
                    {
                        StringValue = twitchClient.IsInChannel ? twitchClient.Channel : "";
                        return 0.0;
                    };
                case "Main":
                    return () =>
                    {
                        messageHandler.Update();
                        StringValue = messageHandler.String;
                        return 0.0;
                    };
                case "IsInChannel": return () => { return twitchClient.IsInChannel ? 1.0 : 0.0; };
            }

            var info = GetInfo(MeasureInfo.Image);
            if (info != null) return GetImageUpdateFunction(info);

            info = GetInfo(MeasureInfo.Gif);
            if (info != null) return GetGifUpdateFunction(info);

            info = GetInfo(MeasureInfo.Link);
            if (info != null) return GetLinkUpdateFunction(info);

            return () => { return 0.0; };
        }

        internal Func<double> GetImageUpdateFunction(MeasureInfo info)
        {
            switch (info.Type)
            {
                case "Width": return () => { return messageHandler.ImageSize.Width; };
                case "Height": return () => { return messageHandler.ImageSize.Height; };
            }

            var i = info.Index;
            switch (info.Type)
            {
                case "X": return () => { return messageHandler.GetImage(i)?.X ?? 0.0; };
                case "Y": return () => { return messageHandler.GetImage(i)?.Y ?? 0.0; };
                case "Name":
                    return () =>
                    {
                        StringValue = messageHandler.GetImage(i)?.Name ?? MissingImage;
                        return 0.0;
                    };
                case "ToolTip":
                    return () =>
                    {
                        StringValue = messageHandler.GetImage(i)?.DisplayName ?? MissingImage;
                        return 0.0;
                    };
                default: return () => { return 0.0; };
            }
        }

        internal Func<double> GetGifUpdateFunction(MeasureInfo info)
        {
            var i = info.Index;
            switch (info.Type)
            {
                case "X": return () => { return messageHandler.GetGif(i)?.X ?? 0.0; };
                case "Y": return () => { return messageHandler.GetGif(i)?.Y ?? 0.0; };
                case "Name":
                    return () =>
                    {
                        StringValue = messageHandler.GetGif(i)?.Name ?? MissingImage;
                        return 0.0;
                    };
                case "ToolTip":
                    return () =>
                    {
                        StringValue = messageHandler.GetGif(i)?.DisplayName ?? MissingImage;
                        return 0.0;
                    };
                default: return () => { return 0.0; };
            }
        }

        internal Func<double> GetLinkUpdateFunction(MeasureInfo info)
        {
            var i = info.Index;
            switch (info.Type)
            {
                case "X":      return () => { return messageHandler.GetLink(i)?.X ?? 0.0; };
                case "Y":      return () => { return messageHandler.GetLink(i)?.Y ?? 0.0; };
                case "Width":  return () => { return messageHandler.GetLink(i)?.Width ?? 0.0; };
                case "Height": return () => { return messageHandler.GetLink(i)?.Height ?? 0.0; };
                case "Url":
                    StringValue = "";
                    return () =>
                    {
                        StringValue = messageHandler.GetLink(i)?.Url ?? "";
                        return 0.0;
                    };
                case "Name":
                    StringValue = "";
                    return () =>
                    {
                        StringValue = messageHandler.GetLink(i) ?? "";
                        return 0.0;
                    };
                default: return () => { return 0.0; };
            }
        }

        internal string GetString()
        {
            return StringValue;
        }

        internal void ExecuteBang(string args)
        {
            if (twitchClient == null || tpe != "Main")
                return;

            if (args.StartsWith("SendMessage"))
            {
                twitchClient.SendMessage(args.Replace("SendMessage ", ""));
                return;
            }

            if (args.StartsWith("JoinChannel"))
            {
                string channel = args.Replace("JoinChannel ", "").ToLower();

                if (channel == string.Empty)
                {
                    twitchClient.LeaveChannel();
                    return;
                }

                if (channel.IndexOfAny(new[] { ' ', ',', ':' }) != -1)
                    return;

                if (!channel.StartsWith("#"))
                    channel = "#" + channel;

                twitchClient.JoinChannel(channel);
            }
        }


        internal class MeasureInfo
        {
            private static string regex = @"([^\d]*)(\d*)?";
            public static readonly Regex Image = new Regex("Image" + regex);
            public static readonly Regex Gif = new Regex("Gif" + regex);
            public static readonly Regex Link = new Regex("Link" + regex);

            public string Type;
            public int Index;
        }

        internal MeasureInfo GetInfo(Regex regex)
        {
            var match = regex.Match(tpe).Groups;

            if (match.Count < 2)
                return null;

            var type = match[1].Value;
            var index = -1;
            if (match.Count >= 3 && match[2].Value != string.Empty)
                index = int.Parse(match[2].Value);

            return new MeasureInfo() { Type = type, Index = index };
        }

    }

    public static class Plugin
    {
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Cleanup();
            GCHandle.FromIntPtr(data).Free();
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }

        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
    }
}
