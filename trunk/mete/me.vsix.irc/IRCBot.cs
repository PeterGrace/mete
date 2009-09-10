
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.IO;
using me.vsix.net;
using System.Reflection;
using System.Threading;


namespace me.vsix.irc
{
    [Serializable]
    public class IRCBot
    {
        Dictionary<string, string> cfg;
        AvailablePlugin[] pluginList;
        GNet cn;
        System.Timers.Timer pluginCheck;
        System.Timers.Timer pingCheck;
        string ctrla;

        public IRCBot(string filename)
        {
            ctrla = ((char)1).ToString();
            cfg = new Dictionary<string, string>();
            if (!readConfig(filename))
                throw new Exception("Couldn't read config.");

            pluginCheck = new System.Timers.Timer(100);
            pluginCheck.Elapsed += new System.Timers.ElapsedEventHandler(pluginCheck_Elapsed);
            reloadPlugins();

            cn = new GNet(cfg["server"], Convert.ToUInt16(cfg["port"]));
            cn.GenericCommEvent += new GNet.raiseEvent(EventMarshal);
            cn.Connect();
            pingCheck = new System.Timers.Timer(60000);
            pingCheck.Elapsed += new System.Timers.ElapsedEventHandler(pingCheck_Elapsed);
            pingCheck.Enabled = false;
        }

void  pingCheck_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
{
 	sendCTCP(cfg["nick"],"PING FOO BAR");
}

        public bool readConfig(string filename)
        {
            string line;
            try
            {
                TextReader fd = new StreamReader(filename);
                while ((line = fd.ReadLine()) != null)                
                {
                    if (line.Length > 0)
                    {
                        if (line.Substring(0, 1) != "#")
                        {
                            string[] keyval = line.Split("=".ToCharArray(), 2);
                            cfg[keyval[0]] = keyval[1];
                        }
                    }
                }
		return new[]{"server","port","user","nick","fullname"}.All(cfg.ContainsKey);
            
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading config file!");
                return false;
            }



        }

        public void Start()
        {
            //reloadPlugins();
            if (cfg.ContainsKey("serverpass"))
                sendToServer("PASS " + cfg["serverpass"]);


            sendToServer("NICK " + cfg["nick"]);
            sendToServer("USER " + cfg["user"] + " 8 * :" + cfg["fullname"]);
            pingCheck.Enabled = true;
        }

        public void processRecv(string input)
        {
            string[] commands;
            //commands = input.Split(Environment.NewLine.ToCharArray());
            commands = input.Split("\r\n".ToCharArray());
            foreach (string sInd in commands)
            {
                procCmd(sInd);
            }

        }

        public void sendToServer(string input)
        {
            Console.WriteLine(input);
            byte[] pb = Encoding.ASCII.GetBytes(input + Environment.NewLine);
            cn.sendBytes(pb);
        }

        private void procCmd(string cmd)
        {
            string serverStr, stdStr, rawStr;
            stdStr = ":([^!]+)!(\\S+)\\s+(\\S+)\\s+:?(\\S+)\\s*(?:[:+-]+(.*))?";
            //serverStr = ":(.*?)\\s(\\d{3})\\s\\w+\\s:(.*?)";
	    serverStr = ":(.*?)\\s(\\d{3})\\s(.*?)\\s:(.*?)$";
            rawStr = "(\\w+) (:.*)";

            Regex rStd = new Regex(stdStr);
            Regex rServer = new Regex(serverStr);
            Regex rRaw = new Regex(rawStr);

            Match m;

            m = rServer.Match(cmd);
            if (m.Success)
            {
                Console.WriteLine("Server Message: " + m.Groups[1].ToString() + "|" + m.Groups[2].ToString());
                procServerMessage(m.Groups[2].ToString(), m.Groups[3].ToString());
            }
            else
            {
                m = rStd.Match(cmd);
                if (m.Success)
                {
                    Console.WriteLine("Client Message: " + m.Groups[0].ToString());
                    procClientMessage(m.Groups[1].ToString(), m.Groups[2].ToString(), m.Groups[3].ToString(), m.Groups[4].ToString(), m.Groups[5].ToString());
                }
                else
                {
                    m = rRaw.Match(cmd);
                    if (m.Success)
                    {
                        Console.WriteLine("Raw Message: " + m.Groups[1].ToString() + "|" + m.Groups[2].ToString());
                        if (m.Groups[1].ToString() == "PING")
                        {
                            Console.WriteLine("Sent Pong.");
                            sendToServer("PONG " + m.Groups[2].ToString());
                        }
                    }
                }
            }

        }
        public void procServerMessage(string code, string args)
        {
            switch (code)
            {
                case "376":
                    {
                        // motd is over
                        joinChannel(cfg["defchan"]);
                        break;
                    }
            }
        }

        public void procClientMessage(string sender, string hostmask, string cmd, string dest, string data)
        {
            Console.WriteLine("Sender {0} ({1}) sent {2} command to destination {3} with data {4}.", sender, hostmask, cmd, dest, data);

            switch (cmd)
            {
                case "PRIVMSG":
                    {
                        pPRIVMSG(sender, hostmask, dest, data);
                        break;
                    }
                case "MODE":
                    {
                        //pMODE(sender,hostmask,dest,data);
                        break;
                    }
                case "KICK":
                    {
                        pKICK(sender, hostmask, dest, data);
                        break;
                    }
                case "INVITE":
                    {
                        pINVITE(sender, hostmask, dest, data);
                        break;
                    }
                case "JOIN":
                    {
                        pJOIN(sender, hostmask, dest, data);
                        break;
                    }
            }
        }

        private void pJOIN(string sender, string hostmask, string dest, string data)
        {
            string[] args=new string[3];

            if (pluginList == null)
                return;


            foreach (AvailablePlugin plugin in pluginList)
            {
                if (plugin.naughty == false)
                {
                    if ((plugin.implements & ModuleImplements.Join) == ModuleImplements.Join)
                    {
                        try
                        {
                            IRCPlugin p = plugin.instance;
			ThreadPool.QueueUserWorkItem(
                            (object state) => p.pEntryPoint(ModuleImplements.Join, sender, hostmask, dest, data));
                        }
                        catch (Exception ex)
                        {
                            makeNaughty(plugin.ClassName, ex);
                        }
                    }
                }
            }
        }
        private void pPRIVMSG(string sender, string hostmask, string dest, string data)
        {

            if (data.Split(' ')[0].StartsWith(((char)1).ToString()))
            {
                Regex ctcpReg = new Regex(".*?" + ctrla + "(.*?)" + ctrla);
                Match ctcpMatch = ctcpReg.Match(data);

                if (ctcpMatch.Success)
                {
                    string[] ctcpargs;
                    try
                    {
                        ctcpargs = ctcpMatch.Groups[1].ToString().Split(' ');
                    }
                    catch (Exception ex)
                    {
                        sendNotice(sender, ctrla + "PING NODATA" + ctrla);
                        return;
                    }
                    switch (ctcpargs[0])
                    {
                        case "PING":
                            {
                                if (ctcpargs.Length > 1)
                                    sendNotice(sender, ctrla + "PING " + ctcpargs[1] + " " + ctcpargs[2] + ctrla);
                                else
                                    sendNotice(sender, ctrla + "PING NODATA" + ctrla);
                                break;
                            }
                        case "VERSION":
                            {
                                sendNotice(sender, ctrla + "VERSION Mete alpha" + ctrla);
                                break;
                            }
                    }
                }

                
                return;
            }

            if (data.Split(' ')[0].ToUpper() == ".SYSTEM")
            {
                if (matchOwner(sender + "!" + hostmask))
                {
			Console.WriteLine("Go Commands!" + sender + ":" + data);
                    goSystemCommands(sender, data);
                    return;
                }
            }
            // don't need to do anything if no plugins are activated.
            if (pluginList == null)
                return;

            Match m;
            Regex r;

            foreach (AvailablePlugin plugin in pluginList)
            {
                if (plugin.naughty == false)
                {
                    if ((plugin.implements & ModuleImplements.Chat) == ModuleImplements.Chat)
                    {
                        IRCPlugin pl = plugin.instance;
                        try
                        {
                            r = new Regex(pl.pGetTriggerRegexp(0));
                            m = r.Match(data);
                            if (m.Success)
                            {
                                try
                                {
			ThreadPool.QueueUserWorkItem(
                            (object state) => pl.pEntryPoint(ModuleImplements.Chat, sender, hostmask, dest, data));
                                }
                                catch (Exception ex)
                                {
                                    makeNaughty(plugin.ClassName, ex);
                                    return;
                                }
                            }
                        }
                        catch (Exception ex1)
                        {
                            makeNaughty(plugin.ClassName, ex1);
                        }
                    }
                }
            }
        }

        private void goSystemCommands(string replyto, string data)
        {

		Console.WriteLine("reply: |"+ replyto+"|");
		Console.WriteLine("data: |"+ data+"|");
            string[] args = data.Split(' ');
            switch (args[1])
            {
                case "asdfunload":
                    {
                        int index = 0;
                        try
                        {
                            index = Convert.ToUInt16(args[2]);
                        }
                        catch (Exception ex)
                        {
                            return;
                        }
                        if (index + 1 > pluginList.Length)
                            sendPrivMessage(replyto, "Invalid plugin index.");

                        sendPrivMessage(replyto, "Trying to unload " + pluginList[index].ClassName);
                        pluginCheck.Enabled = false;
                        AppDomain.Unload(pluginList[index].domain);
                        sendPrivMessage(replyto, "Unload (probably) successful.  Warning!  Plugin cache is now wrong.  Better upgrade your module and reload asap.");
                        pluginCheck.Enabled = true;
                        break;
                    }
                case "reload":
                    {
                        sendPrivMessage(replyto, "Reloading Plugins.");
                        reloadPlugins();
                        break;
                    }
                case "pluginlist":
                    {
                        if (pluginList != null)
                        {
                            int i = 0;
                            foreach (AvailablePlugin plug in pluginList)
                            {
                                if (plug.naughty == true)
                                    sendPrivMessage(replyto, "NAUGHTY PLUGIN #" + i + ": " + plug.ClassName);
                                else
                                    sendPrivMessage(replyto, "Plugin #" + i + ": " + plug.ClassName);
                                i++;
                            }
                        }
                        break;
                    }



            }
        }




        private void pKICK(string sender, string hostmask, string dest, string data)
        {
            //now what?
        }
        private void pINVITE(string sender, string hostmask, string dest, string data)
        {
            joinChannel(data);
        }

        public void joinChannel(string channel)
        {
            sendToServer("JOIN " + channel);
        }

        private void sendPrivMessage(string dest, string message)
        {
            string tmp;
            tmp = ":" + cfg["nick"] + " PRIVMSG " + dest + " :" + message;
            sendToServer(tmp);
        }

        private void sendNotice(string dest, string message)
        {
            string tmp;
            tmp = "NOTICE " + dest + " :" + message;
            sendToServer(tmp);
        }

        private void sendCTCP(string dest, string message)
        {
            string ctrla = ((char)1).ToString();
            string msg = ctrla + message + ctrla;
            sendPrivMessage(dest,msg);
        }
            

        private void sendModeChange(string[] args)
        {
            string tmp;
            tmp = ":" + cfg["nick"] + " MODE " + args[0] + " " + args[1];
            sendToServer(tmp);
        }

        private void EventMarshal(NotifyType msg, object obj)
        {
            switch (msg)
            {
                case NotifyType.Connected:
                    {
                        goConnect();
                        break;
                    }
                case NotifyType.Disconnected:
                    {
                        goDisconnect();
                        break;
                    }
                case NotifyType.ReceivedData:
                    {
                        goReceive();
                        break;
                    }
                case NotifyType.SocketException:
                    {
                        Exception ex = (Exception)obj;
                        goErrorDisplay(ref ex);
                        break;
                    }
            }
        }

        private void goConnect()
        {
            Console.WriteLine("Connected!");
            Start();
        }

        private void goDisconnect()
        {
            Console.WriteLine("Disconnected!");
            cn.Close();
            cn.Connect();
        }

        private void goReceive()
        {
            string str;
            str = Encoding.ASCII.GetString(cn.recvBytes());
            processRecv(str);
        }

        private void goErrorDisplay(ref Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
        }


        public void reloadPlugins()
        {
            if (pluginList != null)
            {
                pluginCheck.Enabled = false;
                foreach (AvailablePlugin plugin in pluginList)
                    AppDomain.Unload(plugin.domain);
                pluginList = null;
            }

            pluginList = findPlugins("/home/pgrace/bot/plugins", "me.vsix.irc.IRCPlugin");
            if (pluginList != null)
            {
                for (int i = 0; i <= (pluginList.Length - 1); i++)
                {
                    AppDomain d = createInstance(pluginList[i]);
                    if (d != null)
                    {
                        pluginList[i].instance = (IRCPlugin)d.CreateInstanceFromAndUnwrap(pluginList[i].AssemblyPath, pluginList[i].ClassName);
                        pluginList[i].domain = d;
                        pluginList[i].naughty = false;
                        try
                        {
                            pluginList[i].implements = pluginList[i].instance.pGetModuleJobs();
                        }
                        catch (Exception ex)
                        {
                            pluginList[i].naughty = true;
                        }
                    }
                    else
                        pluginList[i].naughty = true;

                    //pluginList[i].domain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);
                    //pluginList[i].instance.ActionRequired += new myDelegate(handlePluginComm);
                }
            }
            pluginCheck.Enabled = true;
        }


        private AvailablePlugin[] findPlugins(string strPath, string strInterface)
        {
            ArrayList Plugins = new ArrayList();
            string[] strDLLs;
            int intIndex;
            Assembly objDLL;

            strDLLs = Directory.GetFileSystemEntries(strPath,"*.dll");
            for (intIndex=0;intIndex<=strDLLs.Length-1;intIndex++)
            {
                try
                {
                    //objDLL = Assembly.LoadFrom(strDLLs[intIndex]);
                    objDLL = LoadAss(strDLLs[intIndex]);
                    examineAssembly(objDLL,strInterface,Plugins,strDLLs[intIndex]);
                   
                }
                catch (Exception ex)
                {
                    //Error loading dll, we dont need to do anything
                }
            }
            if (Plugins.Count > 0)
            {
                AvailablePlugin[] results = new AvailablePlugin[Plugins.Count];
                if (Plugins.Count != 0)
                {
                    Plugins.CopyTo(results);
                    return results;
                }
            }
            return null;
        }
        
        private Assembly LoadAss(string filename)
        {
            FileStream fin = new FileStream(filename, FileMode.Open,
            FileAccess.Read);
            byte[] bin = new byte[16384];
            long rdlen = 0;
            long total = fin.Length;
            int len;
            MemoryStream memStream = new MemoryStream((int)total);
            rdlen = 0;
            while (rdlen < total)
            {
                len = fin.Read(bin, 0, 16384);
                memStream.Write(bin, 0, len);
                rdlen = rdlen + len;
            }
            // done with input file
            fin.Close();
            return Assembly.Load(memStream.ToArray());
        }
        private void examineAssembly(Assembly objDll, string strInterface, ArrayList plugins, string filename)
        {
            Type objInterface;
            AvailablePlugin plugin;

            foreach (Type objType in objDll.GetTypes())
            {
                if (objType.IsPublic)
                {
                    if (!((objType.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract))
                    {
                        // Not an abstract class, continue.
                        objInterface = objType.GetInterface(strInterface, true);
                        if (!(objInterface == null))
                        {
                            plugin = new AvailablePlugin();
                            plugin.AssemblyPath = filename;
                            plugin.ClassName = objType.FullName;
                            plugins.Add(plugin);
                        }
                    }
                }
            }
        }

        private AppDomain createInstance(AvailablePlugin plugin)
        {
            try
            {

                AppDomainSetup j = new AppDomainSetup();
                j.ShadowCopyFiles = "true";
                j.ShadowCopyDirectories = "C:\\kmb\\plugins";
                AppDomain d = AppDomain.CreateDomain(plugin.ClassName, AppDomain.CurrentDomain.Evidence, j);
                //objDLL = Assembly.LoadFrom(plugin.AssemblyPath);
                d.CreateInstanceFrom(plugin.AssemblyPath, plugin.ClassName);


                //objDLL = Assembly.LoadFrom(plugin.AssemblyPath);
                //objPlugin = objDLL.CreateInstance(plugin.ClassName);
                return d;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void handlePluginComm(ReplyTypes actionType, string[] obj)
        {
            switch (actionType)
            {
                case ReplyTypes.PrivMsg:
                    {
                        string[] args = (string[])obj;
                        sendPrivMessage(args[0], args[1]);
                        break;
                    }
                case ReplyTypes.Mode:
                    {
                        string[] args = (string[])obj;
                        sendModeChange(args);
                        break;
                    }
            }
        }

        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("MyHandler caught : " + e.Message);
        }



        void makeNaughty(string classname, Exception ex)
        {
            for (int i = 0; i < pluginList.Length; i++)
                if (pluginList[i].ClassName == classname)
                    pluginList[i].naughty = true;

            sendPrivMessage(cfg["defchan"], "plugin " + classname +" was naughty.  Saving myself by ignoring module.  Exception: " + ex.Message);
        }

        public bool matchOwner(string checkMask)
        {
		Console.WriteLine("CheckOwners");
            Regex r;
            Match m;
            string[] list = cfg["ownermask"].Split(",".ToCharArray());
            r = new Regex("^([^!@]+)!([^@]+)@(.*)$");

            Match checkMatch = r.Match(checkMask);

            foreach (string mask in list)
            {
		Console.WriteLine("Iterating for Matches");
                m = r.Match(mask);
                if (m.Success)
                {
                    Console.WriteLine("{0},{1},{2}", m.Groups[1].ToString(), m.Groups[2].ToString(), m.Groups[3].ToString());


                    Regex hostReg = new Regex(m.Groups[3].ToString());
                    Match hostMatch = hostReg.Match(checkMatch.Groups[3].ToString());
                    if (hostMatch.Success)
                    {
			Console.WriteLine("Host Matched!");
                        Regex identReg = new Regex(m.Groups[1].ToString());
                        Match identMatch = identReg.Match(checkMatch.Groups[2].ToString());
                        if (identMatch.Success)
                        {
			Console.WriteLine("Ident Matched!");
                            Regex nickReg = new Regex(m.Groups[1].ToString());
                            Match nickMatch = nickReg.Match(checkMatch.Groups[1].ToString());
                            if (nickMatch.Success)
				{
					Console.WriteLine("Totally matched.");
                                return true;
				}
                        }                        
                    }
                }
            }
            return false;
        }

        void pluginCheck_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (pluginList != null)
            {
                foreach (AvailablePlugin plugin in pluginList)
                {
                    if (plugin.naughty == false)
                    {

                        try
                        {
                            pReply prep;
                            bool leave = false;
                            do
                            {
                                try
                                {
                                    prep = plugin.instance.pGetPendingSends();
                                    if (prep.type > 0)
                                        handlePluginComm(prep.type, prep.args);
                                    else
                                        leave = true;
                                }
                                catch (Exception ex)
                                {
                                    makeNaughty(plugin.ClassName, ex);
                                    leave = true;
                                }

                            }
                            while (!leave);
                        }


                        catch (Exception ex)
                        {
                            return;
                        }
                    }
                }
            }
        }


    }
}
