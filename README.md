# UPX Drop

Windows EXE / DLL 拖放压缩工具，使用 C#、WPF 和未修改的官方 UPX 5.2.1。

## 下载与使用

从 [GitHub Releases](https://github.com/Jay1023CN/upx-drop/releases) 下载 **UpxDrop.exe** 即可。分发和使用时只需要这一个 EXE，无需附带 DLL、配置文件或 UPX 工具文件。需要 Windows 10 / 11 和 .NET Framework 4.8；较旧的 Windows 10 若未安装 4.8，需先通过微软安装。

官方 UPX 压缩包、界面和许可文本均嵌入 EXE。首次运行会校验 SHA-256，将 `upx.exe` 自动释放到 `%LOCALAPPDATA%\UpxDrop\engine`，之后可以离线处理文件。这是单文件分发，运行时仍会产生缓存和用户设置，不是在内存中执行 UPX。

拖入 EXE、DLL 或文件夹，自动排队压缩；也可关闭“添加后自动开始”，手动开始。默认均衡压缩 `-7`，支持 `-1`、`--best` 和解压 `-d`。文件夹递归扫描，跳过目录链接，每批最多 5000 个文件。

可配置：

- 创建备份：默认关闭；开启后，成功替换时，在原文件旁生成独立命名的 `.upxdrop-<随机值>.bak`，不覆盖历史备份。需要还原时，关闭占用文件的程序，将备份复制回原文件名。
- 尝试全部压缩方法：`--all-methods`，耗时较长。
- 压缩导出区段；重定位数据默认保留，以兼容启用 ASLR 的现代 EXE 和 DLL。可选择为兼容的非 ASLR EXE 移除重定位；不会自动关闭 ASLR。
- 强制压缩可疑文件：`--force`，默认关闭，可能影响程序兼容性；签名文件仍然跳过。
- 附加数据：保留、丢弃或跳过含附加数据的文件。官方 UPX 没有单独压缩附加数据的选项；丢弃后不能靠解压恢复。
- 图标：不压缩、保留第一个图标、保留第一个图标目录或压缩全部。

不修改 UPX 标志，以保持官方格式、校验和解压能力；不提供旧版 UPX 切换。压缩流程为：同目录临时副本 → UPX → `upx -t` → 核对原文件未变化 → 原子替换。失败或取消保留原文件；已完成的文件不随取消回滚。UPX 校验不能代替原程序的功能测试。

## 自动更新

默认启动时通过 GitHub API 检查本仓库最新正式 Release。只接受更高的 `v主.次.修订` 版本、名为 `UpxDrop.exe` 的附件和对应 SHA-256；忽略草稿、预发布版和旧版本。优先使用 GitHub 提供的附件 digest，否则使用同一 Release 的 `UpxDrop.exe.sha256`。没有 Release、离线或校验失败均不会替换当前程序。

下载并校验完成后，底部显示版本，按钮变为“退出并更新”。正常退出后，由 Windows PowerShell 等待主进程结束，重新验证并原子替换 EXE，然后重启。安装目录必须对当前用户可写，不请求管理员权限。安装异常会尝试恢复旧版，并记录到 `%LOCALAPPDATA%\UpxDrop\updates\last-update.log`。GitHub 仓库和 Release 发布权限是更新的信任来源；SHA-256 不是独立的代码签名。

可关闭“自动更新”，或使用 `UpxDrop.exe --no-update-check` 本次离线启动。更新状态不会覆盖压缩队列日志。运行时联网只用于检查/下载版本；不会上传待处理文件。

## Windows 构建、测试和发布

无需 Node.js、Python、SDK 或 NuGet 包。使用 Windows 自带的 .NET Framework 4.8 编译器和 Windows PowerShell：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -ExecutionPolicy Bypass -File .\package.ps1
```

构建脚本在需要时从 UPX 官方 Release 下载固定版本，并验证仓库固定的 SHA-256。应用输出：`dist/UpxDrop/UpxDrop.exe`。打包输出：

- `outputs/UpxDrop.exe`：唯一需要分发给最终用户的程序。
- `outputs/UpxDrop.exe.sha256`：自动更新校验附件。
- `outputs/UpxDrop-1.1.0-source.zip`：应用完整源码、构建脚本和 UPX 对应完整源码归档；与 EXE 在同一 Release 发布，运行程序不依赖它。

现有核心测试使用本地 fixture 子进程，覆盖备份、取消、原文件保护、签名检测和选项传递，不等同于真实 UPX 对所有软件的兼容性验证。另有更新元数据/完整性测试、内嵌引擎释放与缓存修复测试、两种窗口大小的 XAML 渲染检查，以及隔离目录只放一个 EXE 的离线启动检查。Windows 更新测试使用独立的旧/新 EXE fixture，验证退出后替换与重启、含中文/空格/单引号的路径，以及新 EXE 无法启动时还原旧程序。压缩文件备份默认关闭；自更新回滚仅在更新安装期间临时保留旧程序，两者相互独立。

可选的 `test-native.ps1` 在 Visual C++ 开发者命令行中编译原生 PE fixture，使用内嵌官方 Windows UPX 验证实际压缩、解压、备份和压缩后程序行为；它不影响普通应用构建所需的依赖。

Windows CI 会运行上述真实引擎检查、构建、测试和上传 EXE、校验文件、源码包及界面截图。发布新版本时，同时修改 `src/Updater.cs` 的 `CurrentVersion` 和 `src/AssemblyInfo.cs` 的版本，然后推送匹配的版本标签（例如 `v1.1.0`）；CI 通过后自动创建正式 GitHub Release。普通分支推送仅生成构建产物，不发布版本。

开发云环境是 Linux。跨平台核心测试和交叉编译不代替 Windows CI；以当前提交的 Windows CI 结果确认界面启动、真实 UPX 处理及自更新替换和恢复是否通过。

## 开源与许可

Copyright (C) 2026 Jay1023CN and contributors. GPL-2.0-or-later，见 [LICENSE](LICENSE)。项目地址：https://github.com/Jay1023CN/upx-drop ，界面底部也可打开该地址及许可文本。

程序内嵌并运行未修改的官方 UPX。原始许可见 [tools/UPX-LICENSE](tools/UPX-LICENSE) 和 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。每次发布同时提供对应源码包；许可和源码归档无需放在用户 EXE 旁才能运行。
