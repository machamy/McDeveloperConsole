# Mc Developer Console

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%20LTS-blue.svg)](https://unity3d.com/get-unity/download)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)
[![Package Version](https://img.shields.io/badge/Version-1.0.2-orange.svg)](package.json)

Unity UI Toolkit 기반 런타임 개발자 콘솔 패키지입니다. 플레이 중 Unity 로그를 확인하고, 콘솔 명령을 실행하며, 프로젝트 전용 디버그 명령을 자동 등록할 수 있습니다.

Netcode for GameObjects가 설치된 프로젝트에서는 `MCDEVCONSOLE_USE_NGO` define이 자동으로 켜지고, 클라이언트-서버 콘솔 명령 scope와 원격 실행 기능을 사용할 수 있습니다.

## Features

- UI Toolkit 기반 인게임 콘솔 창
- Unity `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, `Debug.LogException` 출력 연동
- `[ConsoleCommand]` 기반 static method 자동 등록
- `[ConsoleCommandClass]` 기반 `IConsoleCommand` 구현체 자동 등록
- `IConsoleCommand`, `SimpleCommand`, `RawCommand` 기반 수동 명령 등록
- 명령어와 첫 번째 인자 자동완성
- 입력 히스토리 탐색
- 드래그 이동, 동/남/남동 방향 리사이즈
- 해상도 변경 시 콘솔 크기 제한 자동 보정
- Netcode for GameObjects 사용 시 `Local`, `ClientOnly`, `ServerOnly`, `ClientToServer` command scope 지원

## Installation

### Unity Package Manager

Unity Package Manager의 `Add package from git URL...`에 아래 주소를 넣습니다.

```text
https://github.com/machamy/McDeveloperConsole.git
```

### Manual Copy

직접 포함할 때는 이 저장소의 아래 항목을 Unity 프로젝트의 `Assets/McDeveloperConsole` 아래에 복사합니다.

```text
Editor
Runtime
Editor.meta
Runtime.meta
LICENSE
LICENSE.meta
package.json
package.json.meta
README.md
README.md.meta
```

Unity `.meta` 파일을 같이 유지해야 prefab, USS, UXML, asmdef 참조가 깨지지 않습니다.

## Folder Layout

| Path | Purpose |
| --- | --- |
| `Runtime/Prefabs/ConsoleUI.prefab` | 씬에 배치하는 콘솔 UI prefab |
| `Runtime/Scripts/DeveloperConsole` | 콘솔 core, command system, message type |
| `Runtime/Scripts/UIToolkit` | drag/resize manipulator |
| `Runtime/UI Toolkit/DebugConsole` | UXML/USS/console panel setting |
| `Runtime/Netcode` | Netcode for GameObjects integration |
| `Editor` | inspector/editor support |

## Quick Start

1. `Runtime/Prefabs/ConsoleUI.prefab`을 씬에 배치합니다.
2. 기본 토글 키는 `` ` `` 입니다.
3. 필요하면 `ConsoleUI` 컴포넌트의 `Toggle Console Action`에 프로젝트의 `InputAction`을 연결합니다.
4. 플레이 모드에서 콘솔을 열고 `help`, `ping`, `echo hello` 같은 명령을 실행합니다.

```csharp
using Machamy.DeveloperConsole;
using UnityEngine;

public sealed class ConsoleExample : MonoBehaviour
{
    private void Start()
    {
        McConsole.MessageInfo("Hello, Console!");
        McConsole.MessageWarning("Warning message");
        McConsole.MessageError("Error message");
        McConsole.MessageSuccess("Success message");

        McConsole.Print("Unity log message");
        McConsole.Print(LogType.Warning, "Unity warning message");
    }
}
```

코드에서 명령을 직접 실행할 수도 있습니다.

```csharp
McConsole.Instance.ExecuteCommand("ping");
McConsole.Instance.ExecuteCommand("echo hello world");
```

## Defining Commands

### Attribute Commands

`[ConsoleCommand]`를 static method에 붙이면 런타임 초기화 시 자동 등록됩니다. 지원되는 파라미터 타입은 `int`, `float`, `bool`, `string`, `enum`입니다.

```csharp
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;

public static class PlayerConsoleCommands
{
    [ConsoleCommand("setHealth", "Sets player health.", "setHealth <value>", new[] { "50", "100", "150" })]
    private static void SetHealth(float value)
    {
        McConsole.MessageInfo($"Health set to {value}");
    }

    [ConsoleCommand("god", "Toggles god mode.", "god <true|false>", new[] { "true", "false" })]
    private static void SetGodMode(bool enabled)
    {
        McConsole.MessageSuccess($"God mode: {enabled}");
    }
}
```

`string[]` 하나만 받는 메서드는 직접 인자를 파싱하는 raw command로 등록됩니다.

```csharp
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;

public static class RawConsoleCommands
{
    [ConsoleCommand("say", "Prints the raw input.", "say <message>")]
    private static void Say(string[] args)
    {
        McConsole.MessageDefault(string.Join(" ", args));
    }
}
```

### ConsoleCommandClass

여러 명령을 클래스 단위로 관리하려면 `IConsoleCommand` 구현체에 `[ConsoleCommandClass]`를 붙입니다. 런타임 초기화 시 자동으로 인스턴스가 생성되어 등록되므로 매개변수 없는 생성자가 필요합니다.

```csharp
using System;
using System.Collections.Generic;
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;
using Machamy.DeveloperConsole.Commands;

[ConsoleCommandClass]
public sealed class DifficultyCommand : IConsoleCommand
{
    public string Command => "difficulty";
    public string Description => "Changes difficulty.";
    public string Signature => "difficulty <easy|normal|hard>";

    public void Execute(string[] args)
    {
        if (args.Length == 0)
        {
            McConsole.MessageWarning(Signature);
            return;
        }

        McConsole.MessageInfo($"Difficulty: {args[0]}");
    }

    public void AutoComplete(Span<string> args, ref List<string> suggestions)
    {
        var current = args.Length > 0 ? args[^1] : string.Empty;
        foreach (var option in new[] { "easy", "normal", "hard" })
        {
            if (option.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                suggestions.Add(option);
        }
    }
}
```

### Manual Registration

명령을 원하는 시점에 직접 등록하거나 해제할 수도 있습니다.

```csharp
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Commands;

CommandLibrary.RegisterCommand(new DifficultyCommand());
CommandLibrary.UnregisterCommand("difficulty");
```

## Auto-completion

`[ConsoleCommand]`의 네 번째 인자에 첫 번째 argument 후보를 넣을 수 있습니다.

```csharp
[ConsoleCommand("give", "Gives an item.", "give <itemId>", new[] { "potion", "coin", "key" })]
private static void Give(string itemId)
{
    McConsole.MessageSuccess($"Give {itemId}");
}
```

`IConsoleCommand`를 직접 구현하면 `AutoComplete`에서 현재 입력 상태에 맞는 후보를 더 정교하게 제어할 수 있습니다.

```csharp
public void AutoComplete(Span<string> args, ref List<string> suggestions)
{
    if (args.Length != 1)
        return;

    var current = args[0];
    foreach (var itemId in ItemDatabase.AllIds)
    {
        if (itemId.StartsWith(current, StringComparison.OrdinalIgnoreCase))
            suggestions.Add(itemId);
    }
}
```

## Controls

| Input | Action |
| --- | --- |
| `` ` `` | Open or close the console |
| `Enter` | Execute current input |
| `Tab` | Apply next autocomplete suggestion |
| `Up Arrow` | Recall previous submitted command |
| `Down Arrow` | Recall next submitted command |

## Built-in Commands

| Command | Description |
| --- | --- |
| `help [command]` | Lists available commands or prints help for one command |
| `help2 [command]` | RawCommand-based help command |
| `ping` | Prints `Pong!` |
| `echo <message>` | Prints the input message |
| `clear` | Clears console history |
| `autoScroll` | Toggles automatic scroll-to-bottom |
| `setOpacity <value>` | Sets console opacity from `0.0` to `1.0` |
| `setLogLevel <level>` | Sets Unity log capture level: `0: None`, `1: Exception`, `2: Error`, `3: Warning`, `4: Info` |
| `printAllLogTypes` | Prints sample Unity log and console message types |

## Main API

### McConsole

```csharp
McConsole.Message("message");
McConsole.Message(MessageType.Info, "message");
McConsole.MessageDefault("default");
McConsole.MessageInfo("info");
McConsole.MessageWarning("warning");
McConsole.MessageError("error");
McConsole.MessageDebug("debug");
McConsole.MessageSuccess("success");

McConsole.Print("log");
McConsole.Print(LogType.Warning, "warning log");

McConsole.SetLogLevel(LogLevel.Warning);
McConsole.Instance.ExecuteCommand("help");

bool isOpen = McConsole.Instance.IsWindowOpen;
LogLevel currentLevel = McConsole.Instance.LogPrintLevel;
```

### ConsoleUI

```csharp
ConsoleUI.Instance.Open();
ConsoleUI.Instance.Close();
ConsoleUI.Instance.Toggle();
ConsoleUI.Instance.ClearHistory();
ConsoleUI.Instance.ScrollToBottom();
ConsoleUI.Instance.SetOpacity(0.8f);
ConsoleUI.Instance.RequestAutoComplete("setOpacity ");

bool isOpen = ConsoleUI.Instance.IsOpen;
string currentInput = ConsoleUI.Instance.CurrentInput;
```

콘솔 창의 active 상태 변화를 감지해야 하면 `McConsole.OnConsoleWindowToggled`를 사용할 수 있습니다. 이 hook은 `ConsoleUI.OnEnable`/`OnDisable`에서 호출됩니다.

```csharp
using Machamy.DeveloperConsole;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameplayInputGate : MonoBehaviour
{
    [SerializeField] private InputActionMap gameplayActionMap;

    private void OnEnable()
    {
        McConsole.OnConsoleWindowToggled += SetGameplayInput;
    }

    private void OnDisable()
    {
        McConsole.OnConsoleWindowToggled -= SetGameplayInput;
    }

    private void SetGameplayInput(bool consoleObjectEnabled)
    {
        if (consoleObjectEnabled)
            gameplayActionMap?.Disable();
        else
            gameplayActionMap?.Enable();
    }
}
```

## ConsoleUI Settings

`ConsoleUI` 컴포넌트에서 주요 동작을 설정합니다.

| Field | Description |
| --- | --- |
| `_toggleConsoleAction` | 콘솔 토글용 Input System action. 비어 있으면 `` ` `` 텍스트 입력으로 토글합니다 |
| `_useAutoComplete` | 자동완성 사용 여부 |
| `autoScrollToBottomOnNewMessage` | 콘솔 메시지 추가 시 자동으로 아래로 스크롤 |
| `autoScrollToBottomOnNewPrint` | Unity 로그 출력 추가 시 자동으로 아래로 스크롤 |
| `useResolutionWatcher` | 해상도 변경 시 콘솔 리사이즈 한계를 화면 크기에 맞게 보정 |
| `dontDestroyOnLoad` | scene 전환 후에도 콘솔 UI 유지 |
| `minSize` | 리사이즈 가능한 최소 크기 |
| `maxSize` | 리사이즈 가능한 최대 크기 |

스타일은 아래 USS에서 수정합니다.

```text
Runtime/UI Toolkit/DebugConsole/console.uss
```

메시지 색상은 `MessageType.UssTag`와 USS class로 연결됩니다.

```csharp
MessageType.Default;
MessageType.Info;
MessageType.Warning;
MessageType.Error;
MessageType.Debug;
MessageType.Success;
MessageType.Gray;
MessageType.White;
MessageType.Cyan;
```

## Netcode Integration

`com.unity.netcode.gameobjects`가 설치되어 있으면 asmdef `versionDefines`가 `MCDEVCONSOLE_USE_NGO`를 자동 정의합니다. 이 define이 켜진 경우 `Runtime/Netcode` 코드가 함께 컴파일됩니다.

### Runtime Behavior

- `McConsoleNetcodeAdapter`가 scene load 이후 자동 생성됩니다.
- adapter는 `NetworkManager.Singleton`이 생길 때까지 기다렸다가 custom messaging handler를 등록합니다.
- 서버는 클라이언트에게 remote console 허용 여부를 전파합니다.
- 클라이언트가 `ClientToServer` scope 명령을 실행하면 서버로 command request를 보냅니다.
- 서버는 명령 실행 중 출력된 `McConsole.Message*` 결과를 캡처해 요청 클라이언트에게 돌려줍니다.

### Command Scopes

| Scope | Availability |
| --- | --- |
| `ConsoleCommandScope.Local` | 로컬 콘솔에서 실행 |
| `ConsoleCommandScope.ClientOnly` | 클라이언트에서만 노출 |
| `ConsoleCommandScope.ServerOnly` | 서버/호스트에서만 노출 |
| `ConsoleCommandScope.ClientToServer` | 서버에서 실행 가능하며, 허용된 클라이언트는 서버에 실행 요청 가능 |

Netcode scope는 `[ConsoleCommand]`의 다섯 번째 인자 또는 `IConsoleCommand.Scope`으로 지정합니다.

```csharp
#if MCDEVCONSOLE_USE_NGO
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;

public static class ServerConsoleCommands
{
    [ConsoleCommand(
        "server.giveGold",
        "Gives gold on the server.",
        "server.giveGold <amount>",
        null,
        ConsoleCommandScope.ClientToServer)]
    private static void GiveGold(int amount)
    {
        McConsole.MessageSuccess($"Server gave {amount} gold.");
    }
}
#endif
```

`IConsoleCommand` 예제:

```csharp
#if MCDEVCONSOLE_USE_NGO
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;
using Machamy.DeveloperConsole.Commands;

[ConsoleCommandClass]
public sealed class ServerPingCommand : IConsoleCommand
{
    public string Command => "server.ping";
    public string Description => "Runs ping on the server.";
    public string Signature => "server.ping";
    public ConsoleCommandScope Scope => ConsoleCommandScope.ClientToServer;

    public void Execute(string[] args)
    {
        McConsole.MessageInfo("Server pong.");
    }
}
#endif
```

### Netcode Commands

| Command | Scope | Description |
| --- | --- | --- |
| `net.status` | Local | Prints current `NetworkManager` client/server/host state |
| `net.remoteConsole <on|off>` | ServerOnly | Toggles client-to-server console command requests |
| `net.showRequests <on|off>` | ServerOnly | Toggles server-side logging for client command requests |
| `net.serverPing` | ClientToServer | Requests a server-side ping from a client |

### Remote Responses

서버 명령이 나중에 비동기 응답을 보내야 하면 현재 요청자의 target을 캡처해 보관할 수 있습니다.

```csharp
#if MCDEVCONSOLE_USE_NGO
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;

public static class AsyncServerCommands
{
    [ConsoleCommand(
        "server.longJob",
        "Starts a long server job.",
        "server.longJob",
        null,
        ConsoleCommandScope.ClientToServer)]
    private static void StartLongJob()
    {
        var target = McConsole.CaptureResponseTarget();
        McConsole.MessageInfo("Server job started.");

        // Later, on the server:
        McConsole.RespondSuccess(target, "Server job finished.");
    }
}
#endif
```

## Build Symbols

| Symbol | Effect |
| --- | --- |
| `DO_NOT_USE_DEBUG_CONSOLE` | 콘솔 초기화, 명령 자동 등록, 명령 실행 경로를 비활성화합니다 |
| `DONT_USE_LOGEX_IN_BUILD` | player build에서 `LogEx` 출력을 비활성화합니다 |
| `MCDEVCONSOLE_USE_NGO` | Netcode for GameObjects integration을 활성화합니다. NGO 설치 시 asmdef에서 자동 정의됩니다 |

## Requirements

- Unity 2022.3 or newer
- `com.unity.inputsystem` 1.6.0 or newer
- `com.unity.ui.builder` 1.0.0 or newer
- `com.unity.ugui` 1.0.0 or newer
- Optional: `com.unity.netcode.gameobjects` for Netcode integration

## Troubleshooting

### Console does not open

- 씬에 `ConsoleUI.prefab`이 배치되어 있는지 확인합니다.
- `UIDocument`가 비활성화되어 있지 않은지 확인합니다.
- 커스텀 `Toggle Console Action`을 연결했다면 action binding과 action enable 상태를 확인합니다.
- 별도 action을 연결하지 않았다면 플레이 중 `` ` `` 키 입력이 들어오는지 확인합니다.

### Commands are not registered

- `[ConsoleCommand]` 메서드는 반드시 `static`이어야 합니다.
- 지원 타입은 `int`, `float`, `bool`, `string`, `enum`, 또는 raw command용 단일 `string[]`입니다.
- `[ConsoleCommandClass]`는 `IConsoleCommand`를 구현해야 하며 매개변수 없는 생성자가 필요합니다.
- `DO_NOT_USE_DEBUG_CONSOLE` define이 켜져 있으면 자동 등록과 실행이 비활성화됩니다.

### Auto-completion does not show

- `ConsoleUI`의 `_useAutoComplete`가 켜져 있는지 확인합니다.
- 첫 번째 인자 후보는 `[ConsoleCommand]`의 `arg0AutoComplete` 또는 `IConsoleCommand.AutoComplete`에서 제공합니다.

### Unity logs do not appear

- `McConsole.SetLogLevel` 또는 `setLogLevel` 명령으로 현재 log level을 확인합니다.
- 기본 log capture level은 `Warning`입니다.
- `DO_NOT_USE_DEBUG_CONSOLE` define이 켜져 있으면 Unity log forwarding도 비활성화됩니다.

### Netcode commands do not appear

- `com.unity.netcode.gameobjects`가 설치되어 있는지 확인합니다.
- `MCDEVCONSOLE_USE_NGO` define이 asmdef version define으로 켜졌는지 확인합니다.
- `ServerOnly`, `ClientOnly`, `ClientToServer` 명령은 현재 client/server 상태에 따라 `help` 목록에서 필터링됩니다.
- release build에서는 기본적으로 client-to-server remote console이 꺼져 있습니다. 서버에서 `net.remoteConsole on`으로 켤 수 있습니다.

## License

MIT License. See [LICENSE](LICENSE).
