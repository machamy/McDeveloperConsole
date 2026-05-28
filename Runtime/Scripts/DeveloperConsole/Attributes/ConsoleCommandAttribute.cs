using System;
#if MCDEVCONSOLE_USE_NGO
using Machamy.DeveloperConsole;
#endif
using UnityEngine.Scripting;

namespace Machamy.DeveloperConsole.Attributes
{
    /// <summary>
    /// (eng) Attribute to mark methods as console commands.<br/>
    /// (kor) 메서드를 콘솔 명령어로 표시하는 특성입니다
    /// </summary>
    [Preserve]
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class ConsoleCommandAttribute : Attribute
    {
        public string Command { get; }
        public string Description { get; }

        public string Signature { get;}

        public string[] Arg0AutoComplete { get; }
#if MCDEVCONSOLE_USE_NGO
        public ConsoleCommandScope Scope { get; }
#endif

#if MCDEVCONSOLE_USE_NGO
        public ConsoleCommandAttribute(string command, string description = "", string signature = null, string[] arg0AutoComplete = null, ConsoleCommandScope scope = ConsoleCommandScope.Local)
#else
        public ConsoleCommandAttribute(string command, string description = "", string signature = null, string[] arg0AutoComplete = null)
#endif
        {
            Command = command;
            Description = description;
            Signature = signature;
            Arg0AutoComplete = arg0AutoComplete;
#if MCDEVCONSOLE_USE_NGO
            Scope = scope;
#endif
        }
    }

    [Preserve]
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ConsoleCommandClassAttribute : Attribute
    {
        public ConsoleCommandClassAttribute()
        {
        }
    }
}
