# CAF Converter

Windows 本地音频转换器：批量将音视频转换为 CAF，适用于 iPhone / Bark 通知音。

本仓库提供源码、测试及构建脚本，不包含 FFmpeg 二进制、个人媒体、设置、日志或构建缓存。首次使用请按下文安装 FFmpeg / FFprobe。

CAF Converter 是一款面向 Windows 10/11 x64 的本地桌面工具，可把 MP3、MP4、M4A、WAV、FLAC、AAC、MOV、MKV、WEBM、OGG、OPUS、WMA、AIFF 和 CAF 等媒体转换为 CAF 音频。重点用于 iPhone、Bark 和自定义通知铃声。文件只在本机处理；应用不联网、不上传媒体、不包含遥测、广告或用户追踪。

## 功能

- 中文 WPF 图形界面，支持深色/浅色主题、高 DPI、拖放与批量导入。
- 使用 FFprobe 识别真实媒体流，不依赖扩展名判断内容；无音轨文件不能转换。
- 每文件进度、总进度、1–4 并发、单任务取消和全部取消。
- 输出先写入 `.partial.caf`，FFprobe 验证成功后才原子式进入最终文件名。
- 自动重命名 `名称 (1).caf`，也可明确选择覆盖模式。
- 每日中文日志、14 天清理、损坏设置自动备份恢复。

## 预设与转换参数

“Bark / iPhone 通知音”输出 `pcm_s16le`、44100 Hz、单声道 CAF，最长 29 秒，默认从 00:00 开始。“通用高质量 CAF”输出 44100 Hz、立体声并保留完整时长。“自定义”可选择 22050/32000/44100/48000 Hz、单/双声道、裁剪、单遍 loudnorm、淡入和淡出。

核心参数等价于：输入后准确放置 `-ss`，显式 `-map 0:a:0 -vn -ac -ar -c:a pcm_s16le -f caf -progress pipe:1 -nostats`。所有参数逐项加入 `ProcessStartInfo.ArgumentList`，不使用 cmd.exe 或 shell 拼接。

## 开发环境与运行

- Windows 10/11 x64
- .NET 10 SDK
- Visual Studio 2026（含 .NET 桌面开发）或 `dotnet` CLI

开发运行前，请将同一发行版的 `ffmpeg.exe` 和 `ffprobe.exe` 放到 `src/CafConverter/Resources/Tools/win-x64/`，或将工具所在目录加入 PATH。完整测试和发布脚本需要前一种方式，以验证内嵌资源。

开发运行：

```powershell
dotnet restore tests/CafConverter.Tests/CafConverter.Tests.csproj
dotnet run --project src/CafConverter/CafConverter.csproj
```

## FFmpeg 放置与查找顺序

本源码仓库不附带 FFmpeg / FFprobe。请从 [FFmpeg 官方下载页](https://ffmpeg.org/download.html) 选择 Windows 构建来源，自行获取同一发行版的两个工具。程序依次查找：

1. 内嵌工具，首次运行释放到 `%LOCALAPPDATA%\CafConverter\Tools\ffmpeg-9.0.1-essentials\`；有效版本不会重复释放。
2. 程序目录的 `tools\ffmpeg.exe` 和 `tools\ffprobe.exe`。
3. 系统 PATH。
4. 设置中用户手动选择的目录（下次启动自动使用；内嵌工具仍优先）。

需要生成内嵌工具的单文件应用时，请把两个文件放到 `src/CafConverter/Resources/Tools/win-x64/`。发布脚本会拒绝缺失或过小的文件。工具二进制已加入 `.gitignore`；构建和分发时应保留对应版本的许可证及来源说明。

## 编译、测试与发布

```powershell
dotnet build CafConverter.sln -c Release
dotnet test tests/CafConverter.Tests/CafConverter.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File build/publish.ps1
```

发布配置为 `net10.0-windows`、`win-x64`、SelfContained、PublishSingleFile、非裁剪、单文件压缩，并包含自提取原生/内嵌工具。输出位于 `artifacts/CAFConverter-win-x64/`，主程序为 `CAFConverter.exe`。运行 `build/clean.ps1` 可清理构建输出。

## 设置与日志

- 设置：`%APPDATA%\CafConverter\settings.json`
- 损坏设置备份：`%APPDATA%\CafConverter\settings.corrupt.json`
- 日志：`%LOCALAPPDATA%\CafConverter\Logs\yyyy-MM-dd.log`

设置与工具释放都先写临时文件，再执行替换；日志最多保留 14 天。

## 常见问题

- “未找到 FFmpeg”：点击右侧“选择 FFmpeg”，选择 `ffmpeg.exe`；同目录必须有 `ffprobe.exe`。
- “没有音频轨道”：视频不含可提取音轨，不能转换。
- “输出目录无权限”：改用用户有写权限的文件夹，关闭占用目标文件的程序。
- “磁盘空间不足”：清理磁盘或更换输出目录。
- “输入损坏或格式不支持”：用其他播放器确认文件有效，并查看界面底部日志。
- 杀毒软件拦截：核对 FFmpeg 来源和 SHA-256，按组织安全策略允许；本程序不请求管理员权限。

## 目录结构

```text
CafConverter.sln
src/CafConverter/       WPF 应用、MVVM、服务、主题和内嵌工具
tests/CafConverter.Tests/ 单元测试与可选 FFmpeg 集成测试
build/                  发布和清理脚本
artifacts/              Release 发布产物
```

## 第三方组件

转换核心为 FFmpeg/FFprobe；测试使用 xUnit。具体版本、许可证、来源及 FFmpeg 源码获取说明见 `THIRD_PARTY_NOTICES.md` 和 `FFMPEG_LICENSE.txt`。
