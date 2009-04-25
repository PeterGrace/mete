using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using me.vsix.irc;


namespace me.vsix
{
    class Mete
    {
        static void Main(string[] args)
        {
            //IRCBot irc = new IRCBot("irc.ipv6.he.net", 6667, null);
            IRCBot irc = new IRCBot("C:\\kmb\\kmb.ini");
            while (true)
            {
                System.Threading.Thread.Sleep(10);
            }
        }
    }
}
