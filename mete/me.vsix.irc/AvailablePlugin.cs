using System.Reflection;
using me.vsix.irc;
using System;

[Serializable]
public struct AvailablePlugin
{
    public string AssemblyPath;
    public string ClassName;
    public IRCPlugin instance;
    public AppDomain domain;
    public bool naughty;
    public ModuleImplements implements;

}