# Mc Developer Console

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%20LTS-blue.svg)](https://unity3d.com/get-unity/download)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)
[![Package Version](https://img.shields.io/badge/Version-1.0.2-orange.svg)](package.json)

Unity UI Toolkit 기반의 런타임 개발자 콘솔 패키지입니다. 게임 실행 중 로그를 확인하고, 콘솔 명령을 실행하며, 커스텀 명령어와 자동완성을 추가할 수 있습니다.

## 주요 기능

- 런타임에서 열고 닫을 수 있는 인게임 개발자 콘솔
- Unity `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, `Debug.LogException` 출력 연동
- `[ConsoleCommand]` 기반 정적 메서드 자동 등록
- `IConsoleCommand`, `SimpleCommand`, `RawCommand` 기반 수동 명령 등록
- 명령어와 첫 번째 인자 자동완성
- 입력 히스토리 탐색
- UI Toolkit 기반 스타일 커스터마이징
- 드래그 이동, 동/남/남동 방향 리사이즈, 해상도 변경 시 크기 제한 자동 보정

## 설치

### Unity Package Manager

1. Unity에서 `Window > Package Manager`를 엽니다.
2. `+` 버튼을 누르고 `Add package from git URL...`을 선택합니다.
3. 아래 URL을 입력합니다.

```text
https://github.com/machamy/McDeveloperConsole.git
```

### 수동 설치

1. 저장소를 클론하거나 다운로드합니다.
2. `Assets/McDeveloperConsole` 폴더를 Unity 프로젝트의 `Assets` 아래에 복사합니다.
3. Unity가 패키지와 `.asmdef`를 임포트할 때까지 기다립니다.

## 빠른 시작

1. `Runtime/Prefabs/ConsoleUI.prefab`을 씬에 배치합니다.
2. 기본 프리팹은 `Input System`의 `<Keyboard>/backquote` 바인딩으로 콘솔을 토글합니다.
3. 필요하면 `ConsoleUI` 컴포넌트의 `Toggle Console Action`에 다른 `InputAction`을 연결합니다.
4. 플레이 모드에서 backquote 키를 눌러 콘솔을 열고 명령어를 입력합니다.

```csharp
using Machamy.DeveloperConsole;
using UnityEngine;

public class ConsoleExample : MonoBehaviour
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

명령을 코드에서 직접 실행할 수도 있습니다.

```csharp
McConsole.Instance.ExecuteCommand("ping");
McConsole.Instance.ExecuteCommand("echo hello world");
```

## 커맨드 작성

### 속성 기반 등록

정적 메서드에 `[ConsoleCommand]`를 붙이면 런타임 초기화 시 자동 등록됩니다. 지원되는 파라미터 타입은 `int`, `float`, `bool`, `string`, `enum`입니다.

```csharp
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Attributes;

public static class GameCommands
{
    [ConsoleCommand("setHealth", "플레이어 체력을 설정합니다.", "setHealth <value>", new[] { "50", "100", "150" })]
    private static void SetHealth(float value)
    {
        McConsole.MessageInfo($"Health set to {value}");
    }

    [ConsoleCommand("spawn", "오브젝트를 생성합니다.", "spawn <name> <count>")]
    private static void Spawn(string name, int count)
    {
        McConsole.MessageSuccess($"Spawned {count} {name}");
    }
}
```

`string[]` 하나만 받는 메서드는 직접 인자를 파싱하는 raw command로 등록됩니다.

```csharp
[ConsoleCommand("echoRaw", "입력 인자를 그대로 출력합니다.", "echoRaw <message>")]
private static void EchoRaw(string[] args)
{
    McConsole.MessageDefault(string.Join(" ", args));
}
```

### 수동 등록

`IConsoleCommand`를 구현하거나 제공되는 명령 클래스를 만들어 등록할 수 있습니다.

```csharp
using System;
using System.Collections.Generic;
using Machamy.DeveloperConsole;
using Machamy.DeveloperConsole.Commands;

public sealed class DifficultyCommand : IConsoleCommand
{
    public string Command => "difficulty";
    public string Description => "난이도를 변경합니다.";
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
        var current = args.Length > 0 ? args[^1] : "";
        foreach (var option in new[] { "easy", "normal", "hard" })
        {
            if (option.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                suggestions.Add(option);
        }
    }
}

CommandLibrary.RegisterCommand(new DifficultyCommand());
```

## 조작법

| 입력 | 동작 |
| --- | --- |
| `backquote` | 콘솔 열기/닫기 |
| `Enter` | 현재 입력 실행 |
| `Tab` | 다음 자동완성 후보 적용 |
| `Up Arrow` | 이전 입력 히스토리 불러오기 |
| `Down Arrow` | 다음 입력 히스토리 불러오기 |

## 내장 명령

| 명령 | 설명 |
| --- | --- |
| `help` | 등록된 명령 목록을 출력하거나 특정 명령의 설명을 출력합니다. |
| `help2` | `RawCommand` 기반 help 명령입니다. |
| `ping` | 콘솔 응답을 확인하고 `Pong!`을 출력합니다. |
| `echo <message>` | 입력 메시지를 콘솔에 출력합니다. |
| `clear` | 콘솔 히스토리를 지웁니다. |
| `autoScroll` | 새 메시지/로그 발생 시 자동 스크롤을 토글합니다. |
| `setOpacity <value>` | 콘솔 투명도를 `0.0`부터 `1.0` 사이로 설정합니다. |
| `setLogLevel <level>` | Unity 로그 출력 레벨을 설정합니다. `0: None`, `1: Exception`, `2: Error`, `3: Warning`, `4: Info` |
| `printAllLogTypes` | 로그 타입과 메시지 타입 출력 테스트를 실행합니다. |

## 주요 API

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

bool isOpen = ConsoleUI.IsConsoleOpen;
```

콘솔이 열리는 동안 게임 입력을 비활성화하려면 static 이벤트를 구독합니다.

```csharp
using Machamy.DeveloperConsole;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameplayInputGate : MonoBehaviour
{
    [SerializeField] private InputActionMap gameplayActionMap;

    private void OnEnable()
    {
        ConsoleUI.Opened += DisableGameplayInput;
        ConsoleUI.Closed += EnableGameplayInput;
    }

    private void OnDisable()
    {
        ConsoleUI.Opened -= DisableGameplayInput;
        ConsoleUI.Closed -= EnableGameplayInput;
    }

    private void DisableGameplayInput()
    {
        gameplayActionMap?.Disable();
    }

    private void EnableGameplayInput()
    {
        gameplayActionMap?.Enable();
    }
}
```

`static event`는 구독자가 남아 있으면 참조가 유지될 수 있으므로 `OnDisable`에서 반드시 구독을 해제하세요.

열림 상태 하나만 처리하고 싶다면 `OpenStateChanged`를 사용할 수 있습니다.

```csharp
ConsoleUI.OpenStateChanged += isOpen =>
{
    if (isOpen)
        gameplayActionMap.Disable();
    else
        gameplayActionMap.Enable();
};
```

### MessageType

메시지는 USS 클래스와 연결되는 타입으로 스타일링됩니다.

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

## 설정과 커스터마이징

`ConsoleUI` 컴포넌트에서 주요 옵션을 조정할 수 있습니다.

| 옵션 | 기본 동작 |
| --- | --- |
| `_useAutoComplete` | 자동완성 사용 여부입니다. 기본 프리팹은 활성화되어 있습니다. |
| `autoScrollToBottomOnNewMessage` | 새 콘솔 메시지 발생 시 하단으로 스크롤합니다. |
| `autoScrollToBottomOnNewPrint` | 새 Unity 로그 출력 시 하단으로 스크롤합니다. |
| `useResolutionWatcher` | 화면 해상도 변경 시 콘솔 크기 제한을 현재 화면에 맞게 보정합니다. |
| `minSize` | 리사이즈 가능한 최소 크기입니다. 기본 프리팹은 `360 x 200`입니다. |
| `maxSize` | 리사이즈 가능한 최대 크기입니다. 기본 프리팹은 `1920 x 1080`입니다. |

스타일은 `Runtime/UI Toolkit/DebugConsole/console.uss`에서 수정합니다. 메시지 색상은 `.message.info`, `.message.warn`, `.message.error`, `.message.success`, `.message.gray`, `.message.white`, `.message.cyan`, `.message.debug` 클래스에 연결되어 있습니다.

## 비활성화 심볼

빌드나 특정 환경에서 콘솔을 제외하려면 `DO_NOT_USE_DEBUG_CONSOLE` 심볼을 정의합니다. 이 심볼이 정의되면 `ConsoleUI`는 생성 시 제거되고, 명령 자동 등록과 실행 로직이 비활성화됩니다.

`LogEx` 로그를 빌드에서 제외하려면 `DONT_USE_LOGEX_IN_BUILD` 심볼을 사용할 수 있습니다.

## 요구사항

- Unity `2022.3` 이상
- `com.unity.inputsystem` `1.6.0`
- `com.unity.ui.builder` `1.0.0`
- `com.unity.ugui` `1.0.0`

## 문제 해결

**콘솔이 열리지 않습니다.**
씬에 `ConsoleUI.prefab`이 배치되어 있는지 확인합니다. 기본 토글 키는 backquote입니다. 커스텀 `InputAction`을 연결했다면 액션 바인딩도 확인합니다.

**자동완성이 보이지 않습니다.**
`ConsoleUI`의 `_useAutoComplete` 옵션이 켜져 있는지 확인합니다. 명령어 인자 자동완성은 명령 구현의 `AutoComplete` 또는 `[ConsoleCommand]`의 `arg0AutoComplete` 값에 따라 동작합니다.

**명령어가 등록되지 않습니다.**
속성 기반 명령은 `static` 메서드여야 합니다. 파라미터는 `int`, `float`, `bool`, `string`, `enum` 또는 raw command용 `string[]` 하나여야 합니다.

**Unity 로그가 콘솔에 출력되지 않습니다.**
`McConsole.SetLogLevel` 또는 `setLogLevel` 명령으로 현재 로그 출력 레벨을 확인합니다. 기본값은 `Warning`입니다.

## 라이선스

이 프로젝트는 MIT License로 배포됩니다. 자세한 내용은 [LICENSE](LICENSE)를 확인하세요.
