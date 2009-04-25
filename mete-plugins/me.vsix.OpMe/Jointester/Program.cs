using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using me.vsix.irc;

namespace Jointester
{
    class Program
    {
        static void Main(string[] args)
        {
            me.vsix.OpMe.JoinWatch foo = new me.vsix.OpMe.JoinWatch();

            foo.pEntryPoint(ModuleImplements.Join,"km", "~kmb-@2001:470:8a93:3:a4e3:a289:900c:379b", "#kmb", "");
            Console.ReadLine();

        }
    }
}
