﻿// Uncomment these only if you want to export GetString() or ExecuteBang().
#define DLLEXPORT_GETSTRING
#define DLLEXPORT_EXECUTEBANG

using System;
using System.Runtime.InteropServices;
using Rainmeter;
using System.Text.RegularExpressions;
using System.Drawing;

// Overview: This is a blank canvas on which to build your plugin.

// Note: Measure.GetString, Plugin.GetString, Measure.ExecuteBang, and
// Plugin.ExecuteBang have been commented out. If you need GetString
// and/or ExecuteBang and you have read what they are used for from the
// SDK docs, uncomment the function(s). Otherwise leave them commented out
// (or get rid of them)!

namespace PluginTwitch
{

    internal class Measure
    {
        static TwitchClient twitch = null;

        string tpe = "";

        internal Measure()
        {
        }

        internal void Reload(API api, ref double maxValue)
        {
            tpe = api.ReadString("Type", "");
            if (tpe != "Main")
                return;

            if (twitch == null)
            {
                string user = api.ReadString("Username", "");
                string ouath = api.ReadString("Ouath", "");
                string fontFace = api.ReadString("FontFace", "");
                string emoteDir = api.ReadString("EmoteDir", "");
                int width = api.ReadInt("Width", 500);
                int height = api.ReadInt("Height", 500);
                int fontSize = api.ReadInt("FontSize", 0);
                if (user == "" || ouath == "" || fontFace == "" || emoteDir == "" || fontSize == 0)
                    return;

                var font = new Font(fontFace, fontSize);
                var messageParser = new MessageParser(width, height, emoteDir, font);
                twitch = new TwitchClient(user, ouath, messageParser);
            }

            string newChannel = api.ReadString("Channel", "").ToLower();
            if (newChannel == "" || newChannel[0] != '#')
                return;

            if (newChannel == "#")
            {
                twitch.LeaveChannel();
                return;
            }

            if(newChannel != twitch.Channel)
            {
                twitch.Connect();
                twitch.JoinChannel(newChannel);
            }
        }

        internal void Cleanup()
        {
            twitch?.Disconnect();
            twitch = null;
        }

        internal double Update()    
        {
            if (twitch == null)
                return 0.0;

            if (tpe == "InChannel")
                 return twitch.IsInChannel() ? 1.0 : 0.0;

            if (tpe.StartsWith("TwitchEmoteSize"))
                return twitch.EmoteSize;

            if (tpe.StartsWith("TwitchEmote"))
            {
                var pattern = @"TwitchEmote([^\d]*)(\d*)";
                var m = Regex.Matches(tpe, pattern)[0].Groups;
                var variable = m[1].Value;
                var index = int.Parse(m[2].Value);
                var emote = twitch.GetEmote(index);
                if (emote == null)
                    return 0.0;

                switch (variable)
                {
                    case "ID": return emote.ID;
                    case "X": return emote.X;
                    case "Y": return emote.Y;
                    default: return 0.0;
                }
            }

            return 0.0;
        }
        
#if DLLEXPORT_GETSTRING
        internal string GetString()
        {
            if (tpe == "Main")
                return twitch?.String ?? "";

            return null;
        }
#endif
        
#if DLLEXPORT_EXECUTEBANG
        internal void ExecuteBang(string args)
        {
            twitch.SendMessage(args);
        }
#endif
    }

    public static class Plugin
    {
#if DLLEXPORT_GETSTRING
        static IntPtr StringBuffer = IntPtr.Zero;
#endif

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
#if DLLEXPORT_GETSTRING
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
#endif
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
        
#if DLLEXPORT_GETSTRING
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
#endif

#if DLLEXPORT_EXECUTEBANG
        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
#endif
    }
}
