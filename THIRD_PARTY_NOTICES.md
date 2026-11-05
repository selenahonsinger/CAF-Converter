# Third-Party Notices

## FFmpeg / FFprobe

本源码仓库不分发 FFmpeg / FFprobe 二进制。使用者应自行获取工具并遵守所选构建的许可证。原构建记录使用 FFmpeg 9.0.1 essentials build（Windows x64 static），由 gyan.dev 构建；以下版本与哈希仅用于说明该构建记录，不代表本仓库包含该文件。

- FFmpeg 项目：https://ffmpeg.org/
- 官方下载页：https://ffmpeg.org/download.html
- 二进制构建页：https://www.gyan.dev/ffmpeg/builds/
- 对应源代码：https://github.com/FFmpeg/FFmpeg/tree/n9.0.1
- 下载包 SHA-256：`fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9`

该 essentials 构建页声明其静态构建采用 GNU General Public License version 3 (GPLv3)。随发行版提供的完整许可证文本见 `FFMPEG_LICENSE.txt`。再分发者必须继续遵守 GPLv3，包括保留许可证，并按许可证要求向接收者提供相应完整源代码或有效的书面源代码要约。项目本身不修改 FFmpeg 二进制；FFmpeg 以独立进程运行。

FFmpeg 是 FFmpeg 项目的商标；本项目与 FFmpeg 项目或构建提供者没有隶属关系。

## xUnit.net

xUnit.net 3.2.2 与 Visual Studio runner 3.1.5 仅用于测试，不包含在应用发布运行时中。xUnit.net 采用 Apache License 2.0。

- https://xunit.net/
- https://github.com/xunit/xunit

## Microsoft .NET

应用以 .NET 10 自包含方式发布。Microsoft .NET 运行时组件依据其各自许可证再分发；参见发布包内组件元数据及：https://github.com/dotnet/runtime
