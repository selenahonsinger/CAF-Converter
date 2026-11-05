将同一可靠发行版的 64 位 Windows 静态构建 ffmpeg.exe 和 ffprobe.exe 放在此目录。
构建后它们会作为 EmbeddedResource 内嵌，并在首次运行时释放到：
%LOCALAPPDATA%\CafConverter\Tools\<工具版本>\

请从 FFmpeg 官方下载页列出的 Windows 构建来源获取，并遵守相应许可证：
https://ffmpeg.org/download.html
