using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using me.vsix.irc;

namespace me.vsix.mete.plugin.Weather
{
    public class GetWX : MarshalByRefObject,IRCPlugin
    {
        Queue<pReply> pendingSends;
        public GetWX()
        {
            pendingSends = new Queue<pReply>();
            //http://api.wunderground.com/auto/wui/geo/WXCurrentObXML/index.xml?query=KSFO

        }

        public string GetWeather(string station)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load("http://api.wunderground.com/auto/wui/geo/WXCurrentObXML/index.xml?query=" + station);

                //doc.GetElementById("temp_f");
                XmlNodeList temp_f = doc.GetElementsByTagName("temp_f");
                XmlNodeList obsTime = doc.GetElementsByTagName("observation_time_rfc822");
                XmlNodeList humid = doc.GetElementsByTagName("relative_humidity");
                XmlNodeList dewpoint = doc.GetElementsByTagName("dewpoint_f");
                XmlNodeList weather = doc.GetElementsByTagName("weather");
                XmlNodeList pressure = doc.GetElementsByTagName("pressure_in");
                XmlNodeList windchill = doc.GetElementsByTagName("windchill_f");
                XmlNodeList heatindex = doc.GetElementsByTagName("heatindex_f");
                XmlNodeList windspeed = doc.GetElementsByTagName("wind_mph");
                XmlNodeList windDir = doc.GetElementsByTagName("wind_dir");
                XmlNodeList location = doc.GetElementsByTagName("display_location");
                XmlNode locationSet = location[0];

                return "Conditions @ " + locationSet["full"].InnerText + " as of " + obsTime[0].InnerText + " -- Temp: " + temp_f[0].InnerText + "F "
                    + "Hum: " + humid[0].InnerText + " "
                    + "Dewpoint: " + dewpoint[0].InnerText + "F "
                    + "Wind: " + windDir[0].InnerText + " at " + windspeed[0].InnerText + "mph "
                    + "Baro: " + pressure[0].InnerText + " "
                    + "Current Weather: " + weather[0].InnerText;
            }
            catch (Exception ex)
            {
                return "Something bad happened while looking up that weather.";
            }
        }



        #region IRCPlugin Members

        public bool pEntryPoint(ModuleImplements eventType, string sender, string hostmask, string dest, string data)
        {
            string[] tmp = new string[2];            
            string replyto;
            if (dest.Substring(0, 1) == "#")
                replyto = dest;
            else
                replyto = sender.ToUpper();

            string[] args = data.Split(" ".ToCharArray(),2);
            pReply foo = new pReply();
            foo.type = ReplyTypes.PrivMsg;
            string[] retval = new string[2];
            retval[0] = replyto;
            retval[1]=GetWeather(args[1]);
            foo.args = retval;
            pendingSends.Enqueue(foo);
            return false;     
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
            return ".*?\\.wx (.*?)";
        }

        #endregion
    }
}
