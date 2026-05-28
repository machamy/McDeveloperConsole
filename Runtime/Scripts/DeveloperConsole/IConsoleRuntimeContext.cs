#if MCDEVCONSOLE_USE_NGO
using Machamy.DeveloperConsole.Commands;

namespace Machamy.DeveloperConsole
{
    public interface IConsoleRuntimeContext
    {
        bool IsClient { get; }
        bool IsServer { get; }
    }

    public interface IConsoleRemoteCommandExecutor
    {
        bool CanRequestServerCommand(IConsoleCommand command);
        bool RequestServerCommand(string input);
    }
}
#endif
