using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using me.vsix.irc;

namespace me.vsix.OpMe
{
    
    public class JoinWatch:MarshalByRefObject,IRCPlugin
    {
        Queue<pReply> pendingSends;
        Dictionary<string, ArrayList> OpsList;

        public JoinWatch()
        {
            pendingSends = new Queue<pReply>();
            refreshData();
        }

        void refreshData()
        {
            string line;
            OpsList = new Dictionary<string, ArrayList>();
            TextReader tr = new StreamReader("C:\\kmb\\data\\ops\\ops.txt");
            while ((line = tr.ReadLine()) != null)
            {
                string[] keyval = line.Split(',');
                string chan = keyval[0];
                if (OpsList.ContainsKey(chan))
                    OpsList[chan].Add(keyval[1]);
                else
                {
                    ArrayList foo = new ArrayList(10);
                    foo.Add(keyval[1]);
                    OpsList[chan] = foo;
                }
            }
            tr.Close();

        }



        #region IRCPlugin Members

        public ModuleImplements pGetModuleJobs()
        {
            return ModuleImplements.Join;
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
            throw new NotImplementedException();
        }

        public bool pEntryPoint(ModuleImplements whatType, string sender, string hostmask, string dest, string data)
        {
            if (whatType != ModuleImplements.Join)
                return false;

            Regex r;
            Match m;
            string[] tmp = new string[2];
            
            if (OpsList.ContainsKey(dest))
            {
                ArrayList CurrentOps = OpsList[dest];
            }
            r = new Regex("^([^!@]+)!([^@]+)@(.*)$");

            foreach (string op in OpsList[dest])
            {
                m = r.Match(op);
                if (m.Success)
                {
                    //Console.WriteLine("{0},{1},{2}", m.Groups[1].ToString(), m.Groups[2].ToString(), m.Groups[3].ToString());

                    string[] fullmask = hostmask.Split('@');

                    Regex hostReg = new Regex(m.Groups[3].ToString());
                    Match hostMatch = hostReg.Match(fullmask[1]);
                    if (hostMatch.Success)
                    {
                        Regex identReg = new Regex(m.Groups[1].ToString());
                        Match identMatch = identReg.Match(fullmask[0]);
                        if (identMatch.Success)
                        {
                            Regex nickReg = new Regex(m.Groups[1].ToString());
                            Match nickMatch = nickReg.Match(sender);
                            if (nickMatch.Success)
                            {
                                tmp[0] = dest;
                                tmp[1] = "+o " + sender;
                                pReply foo;
                                foo.type = ReplyTypes.Mode;
                                foo.args = tmp;
                                pendingSends.Enqueue(foo);
                            }
                        }                        
                    }
                }
            }
            



            return true;
        }

        #endregion
    }
}
