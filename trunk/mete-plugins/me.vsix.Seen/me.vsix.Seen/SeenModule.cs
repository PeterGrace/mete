using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using me.vsix.irc;

namespace me.vsix.Seen
{
    public class SeenModule:MarshalByRefObject,IRCPlugin 
    {
        Queue<pReply> pendingSends;
        Dictionary<string, Dictionary<string, Seen>> seenMap;

        public SeenModule()
        {
            pendingSends = new Queue<pReply>();
            seenMap = new Dictionary<string, Dictionary<string,Seen>>();
        }
        #region IRCPlugin Members

        public bool pEntryPoint(ModuleImplements whatType, string sender, string hostmask, string dest, string data)
        {
            string[] tmp = new string[2];            
            string replyto;
            if (dest.Substring(0, 1) == "#")
                replyto = dest;
            else
                replyto = sender.ToUpper();

            pReply foo;
            string[] args = data.Split(' ');
            switch (args[0])
            {
                case "seen":
                    {
                        if (args.Length < 2)
                            return false;
                        if (seenMap.ContainsKey(args[1].ToUpper()))
                        {
                            if (seenMap[args[1].ToUpper()].ContainsKey(dest))
                            {
                                TimeSpan datediff = DateTime.Now.Subtract(seenMap[args[1].ToUpper()][dest].whenSeen);
                                tmp[1] = args[1] + " was last seen saying: \"" + seenMap[args[1].ToUpper()][dest].lastMessage + "\" in channel " + dest + " " + datediff.ToString() + " ago.";
                            }
                        }
                        else
                            tmp[1] = args[1] + " has no history in " + dest;

                        foo.type = ReplyTypes.PrivMsg;
                        tmp[0] = replyto;
                        foo.args = tmp;
                        pendingSends.Enqueue(foo);

                        break;

                    }
                default:
                    {
                        Seen whenseen;
                        whenseen.lastMessage = data;
                        whenseen.whenSeen = DateTime.Now;
                        if (seenMap.ContainsKey(sender.ToUpper()))
                            seenMap[sender.ToUpper()][dest] = whenseen;
                        else
                        {
                            seenMap[sender.ToUpper()] = new Dictionary<string, Seen>();
                            seenMap[sender.ToUpper()][dest] = whenseen;
                        }
                        break;
                    }
            }

            return true;

        }

        public ModuleImplements pGetModuleJobs()
        {
            return (ModuleImplements.Chat);
        }

        public pReply pGetPendingSends()
        {
            pReply dummy = new pReply();
            dummy.type = 0;

            if (pendingSends != null)
                if (pendingSends.Count > 0)
                    return pendingSends.Dequeue();
                else
                    return dummy;
            else
                return dummy;
        }

        public string pGetTriggerRegexp(int whichType)
        {
            return "(.*)";
        }


        #endregion
    
} 
    struct Seen
    {
        public DateTime whenSeen;
        public string lastMessage;
    }
}
