using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace me.vsix.irc
{
    //public delegate void myDelegate(int actionType, ref object obj);
    [Serializable]
    public struct pReply
    {
        public ReplyTypes type;
        public string[] args;
    };

    public enum ReplyTypes
    {
        Nothing = 0,
        PrivMsg = 1,
        Mode = 2
    }
public enum ModuleImplements
{
    Nothing =0,
    Chat = 1,
    Mode = 2,
    Join = 4,
    Kick = 8
}
    public interface IRCPlugin
    {

        //event myDelegate ActionRequired;
        string pGetTriggerRegexp(int whichType);
        bool pEntryPoint(ModuleImplements eventType,string sender, string hostmask, string dest, string data);
        pReply pGetPendingSends();
        ModuleImplements pGetModuleJobs();
    }
}
