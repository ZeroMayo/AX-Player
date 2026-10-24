[English](README.md) | **한국어**

<h1 align="center">AX Player</h1>
<p align="center"><img width="250" alt="AX Player Logo" src="assets/AXPlayer-Icon.png"></p>
<p align="center">디자인과 창의성에 중점을 둔 새로운 시대의 YouTube 동영상 플레이어</p>
<p align="center">이 앱의 일부 리소스(samples)는 <a href="https://github.com/ppy/osu-resources">ppy/osu-resources</a>를 사용하고 있습니다.</p>

<p align="center">
<a href="https://github.com/ZeroMayo/ZeroMayo/blob/main/docs/project-status.md"><img src="https://img.shields.io/badge/status-active-47c219.svg" alt="상태"></a>
<a href="https://github.com/ZeroMayo/AX-Player/actions/workflows/ci.yml"><img src="https://github.com/ZeroMayo/AX-Player/actions/workflows/ci.yml/badge.svg?branch=master&event=push" alt="빌드 상태"></a>
<a href="https://github.com/ZeroMayo/AX-Player/releases/latest"><img src="https://img.shields.io/github/release/ZeroMayo/AX-Player.svg" alt="GitHub 릴리스"></a>
<img src="https://img.shields.io/badge/made_in-korea-0F64CD.svg?labelColor=CD2E3A" alt="Made In Korea">
<a href="https://github.com/ZeroMayo/AX-Player/blob/master/LICENSE.md"><img src="https://img.shields.io/github/license/ZeroMayo/AX-Player.svg" alt="라이선스"></a>
<a href="https://discord.gg/UZWDqQ29ch"><img src="https://discordapp.com/api/guilds/1474931183854026812/widget.png?style=shield" alt="dev chat"></a>
<a href="https://www.codefactor.io/repository/github/ZeroMayo/AX-Player"><img src="https://www.codefactor.io/repository/github/ZeroMayo/AX-Player/badge" alt="CodeFactor"></a>
</p>

## 앱 다운로드 및 설치

### 최신 릴리스:

| [Windows 10+ (x64)](https://github.com/ZeroMayo/AX-Player/releases/latest/download/AX Player-win-Setup.exe) |
|--------------------------------------------------------------------------------------|

YouTube API Daily Quota는 매우 빠르게 차버립니다(구글은 10,000단위의 엄격한 제한을 두고 있습니다). 이에 대해 묻지 마세요.

## AX Player 개발

### 사전 조건

다음 사전 조건을 반드시 확인해 주세요:

- 데스크톱 플랫폼과 [.NET 10.0 SDK](https://dotnet.microsoft.com/download)가 설치되있어야 합니다.
- 리눅스에서 실행할 때는 비디오 디코딩을 지원할 수 있도록 시스템 전체에 FFmpeg가 설치되어 있어야 합니다.

코드베이스를 다룰 때는 최신 버전의 [Visual Studio](https://visualstudio.microsoft.com/vs/), [EditorConfig](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig)와 [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) 플러그인이 설치된 [Visual Studio Code](https://code.visualstudio.com/) 같은 지능형 코드 완성 및 문법 강조 기능이 있는 IDE 사용을 권장합니다.

### 소스 코드 다운로드

서브모듈을 포함해 저장소를 복제합니다:

```shell
git clone https://github.com/ZeroMayo/AX-Player
cd AXPlayer
```

`AX Player` 디렉터리 안에서 다음 명령어를 실행해 소스 코드를 최신 커밋으로 업데이트하세요:

```shell
git pull
```

### 빌드하기

#### IDE에서

주요 `.sln` 파일 대신 플랫폼별 `.slnf` 파일 중 하나를 통해 솔루션을 로드해야 합니다. 이렇게 하면 의존성을 줄이고 신경 쓰지 않는 플랫폼을 숨길 수 있습니다. 유효한 `.slnf` 파일은 다음과 같습니다:

- `AXPlayer.Desktop.Windows.slnf` (WinRT 확장 기능이 포함된 Windows 플랫폼, 가장 일반적)
- `AXPlayer.Desktop.slnf` (Linux 및 기타 플랫폼)

위에 나열된 권장 IDE들의 실행 설정도 포함되어 있습니다. IDE에서 제공하는 Build/Run 기능을 사용해서 작업을 시작해야 합니다. 새 컴포넌트를 테스트하거나 빌드할 때는 `AX Player (Tests)` 프로젝트나 설정을 사용하는 것을 강력히 권장합니다. 이에 대한 자세한 정보는 [아래](#contributing)에 제공되어 있습니다.

#### CLI에서

또한 명령어 한 줄로 커맨드라인에서 *AX Player*를 빌드하고 실행할 수 있습니다:

```shell
dotnet run --project AXPlayer.Desktop.Windows (for Windows)
dotnet run --project AXPlayer.Desktop (for Linux and other platform)
```

로컬에서 성능 테스트를 할 때는 빌드 명령어에 `-c Release` 옵션을 꼭 추가하세요. 기본 `Debug` 설정으로 실행하면 오버헤드가 클 수 있기 때문입니다(특히 로컬 프레임워크 수정 테스트 시).

빌드가 실패하면 `dotnet restore` 명령어로 NuGet 패키지를 복원해 보세요.

### 프레임워크 수정으로 테스트하기

가끔은 [osu-framework](https://github.com/ZeroMayo/osu-framework) 의 변경 사항을 교차 테스트해야 할 때가 있습니다. 포함된 명령어를 사용하면 이를 빠르게 수행할 수 있습니다:

Windows:

```ps
UseLocalFramework.ps1
```

macOS / Linux:

```ps
UseLocalFramework.sh
```

이 명령어들은 인접한 디렉터리에 관련 프로젝트가 체크아웃되어 있다고 가정합니다:

```
|- AX-Player            // 이 저장소
|- osu-framework
```

## 번역

*AX Player* 번역을 지원하기 위해, 더 이상 Crowdin을 통한 번역 기여를 받지 않습니다. 모든 번역은 이제 LLM을 사용해 처리됩니다.

새로운 언어 지원을 추가하고 싶으시면 언제든지 PR을 열어주세요. 기존 영어 및 한국어 번역본의 수정본은 허용되지 않음을 알려드립니다.

## 기여하기

프로젝트에 기여하는 방법으로는 문제를 보고하거나 풀 리퀘스트를 제출하는 두 가지가 있습니다. 가장 효과적으로 도움을 줄 수 있는 방법을 이해하려면 [기여 지침](CONTRIBUTING.md)을 참고해 주세요.

우리 팀은 인간의 기여를 믿습니다. AI/LLM을 사용해 작성되었거나 문서화되었거나 지원받은 이슈 리포트나 풀 리퀘스트 등 모든 기여물은 추가 논의 없이 보통 종료 및 잠금 처리됩니다.

## 라이선스

*AX Player*의 코드는 [MIT 라이선스](https://opensource.org/licenses/MIT) 하에 배포됩니다. 자세한 내용은 [라이선스 파일](LICENCE)을 참고해 주세요. [요약](https://tldrlegal.com/license/mit-license)하자면, 소프트웨어나 소스의 어떤 복사본에든 원본 저작권과 라이선스 고지를 포함하는 한 원하는 대로 자유롭게 사용할 수 있습니다.

**참고:** FFmpeg 바이너리는 원본 라이선스(GPL/LGPL) 하에 소스에서 배포됩니다.
자세한 내용은 [FFmpeg 라이선스](https://www.ffmpeg.org/legal.html)를 참고해 주세요.

또한 앱 리소스는 별도의 라이선스가 적용된다는 점도 참고해 주세요. 자세한 내용은 [라이선스 파일](https://github.com/ZeroMayo/AX-Player/blob/master/AX-Player.App.Resources/LICENSE.md)을 참고해 주세요.

## 즐겨찾기 기록

<a href="https://www.star-history.com/?repos=ZeroMayo%2FAX-Player&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/image?repos=ZeroMayo/AX-Player&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/image?repos=ZeroMayo/AX-Player&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/image?repos=ZeroMayo/AX-Player&type=date&legend=top-left" />
 </picture>
</a>

## 어원

**AX Player**에서 "AX"는 '**A**dvanced e**X**perience(고급 경험)'라는 구상에서 비롯되었습니다.