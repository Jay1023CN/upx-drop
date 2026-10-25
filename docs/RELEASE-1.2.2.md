<div align="center">

<img src="https://raw.githubusercontent.com/Jay1023CN/upx-drop/v1.2.2/src/Assets/AppIcon.png" width="72" height="72" alt="UPX Drop 图标">

# UPX Drop 1.2.2

**Windows EXE / DLL 批量压缩与解压**

单 EXE 免安装 · 内置官方 UPX 5.2.1 · 本地处理

[**下载 Windows 版**](https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.2/UpxDrop.exe)　·　[使用说明](https://github.com/Jay1023CN/upx-drop#readme)　·　[反馈问题](https://github.com/Jay1023CN/upx-drop/issues)

</div>

UPX Drop 把 UPX 命令行做成了可直接操作的图形界面。拖入 EXE、DLL 或整个文件夹，选择压缩强度，就能批量给程序瘦身；也能解压 UPX 压缩过的文件。

![UPX Drop 1.2.2 主界面：批量压缩队列、体积统计与控制面板](https://raw.githubusercontent.com/Jay1023CN/upx-drop/v1.2.2/docs/images/workspace.png)

<p align="center"><sub>主界面 · 图中为原生测试程序的实际处理结果，压缩率取决于文件本身。</sub></p>

## 本次更新

修正下拉框误用 Windows 默认灰色按钮背景的问题，让筛选框和高级选项与整体界面保持一致。

- **默认状态**：白底、浅色边框。
- **悬停与展开**：悬停时浅色反馈，展开时淡蓝底、蓝色边框。
- **键盘聚焦**：蓝色焦点边框替代黑色虚线，保留清晰的操作提示。

## 主要功能

| 功能 | 能做什么 |
| :--- | :--- |
| **批量处理** | 递归扫描文件夹、自动去重，支持压缩和解压，一次最多加入 5000 个文件。 |
| **三档压缩** | 快速、均衡、极致，按速度和体积需求选择，默认使用均衡。 |
| **队列管理** | 自动或手动开始，处理中可追加文件、停止任务，失败或取消后可重试。 |
| **搜索筛选** | 按文件名、路径或状态查找；支持多选移出队列、清理已完成记录。 |
| **结果统计** | 查看前后体积、变化比例、节省空间与详细日志，支持导出 CSV。 |
| **原文件保护** | 先处理副本，压缩校验通过后再替换；失败或取消时保留原文件，可选独立备份。带数字签名、压缩后未变小的文件自动跳过。 |
| **高级设置** | 调整图标、附加数据、导出区段和重定位数据的处理方式，或尝试全部压缩方法。 |
| **自动更新** | 检查 GitHub 新版本，下载并校验后，在退出时完成更新。 |

<details>
<summary><b>查看高级设置截图</b></summary>

![UPX Drop 1.2.2 高级设置：压缩方法、附加数据与图标处理](https://raw.githubusercontent.com/Jay1023CN/upx-drop/v1.2.2/docs/images/workspace-advanced.png)

可根据文件需要调整压缩选项，日常使用选择默认的“均衡”即可。

</details>

## 下载与使用

适用于 **Windows 10 / 11 x64**，需要 **.NET Framework 4.8**。无需安装，也无需另行下载 UPX。

1. 下载 [**UpxDrop.exe**](https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.2/UpxDrop.exe)，直接运行。
2. 选择压缩或解压，按需开启“保留原文件备份”。
3. 拖入文件或文件夹。默认自动开始，也可关闭自动开始后手动处理。

| 下载文件 | 用途 |
| :--- | :--- |
| [**UpxDrop.exe**](https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.2/UpxDrop.exe) | 软件主程序，日常使用只需下载这个文件。 |
| [UpxDrop.exe.sha256](https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.2/UpxDrop.exe.sha256) | 主程序的 SHA-256 校验文件。 |
| [UpxDrop-1.2.2-source.zip](https://github.com/Jay1023CN/upx-drop/releases/download/v1.2.2/UpxDrop-1.2.2-source.zip) | 应用完整源码、构建脚本及对应 UPX 源码。 |

文件处理全在本地完成，不上传文件，支持离线使用。压缩校验通过后，发布前仍需要运行原程序确认功能正常。
