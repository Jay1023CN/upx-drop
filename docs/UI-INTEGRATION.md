# 界面与功能接入约定

1.3.0 的界面与队列功能已接入。后续调整布局、配色、字体和动效时，可按下面的控件约定复用功能层。

## 文件分工

- `src/Queue.cs`：队列记录、后台扫描、搜索匹配、CSV 导出。
- `src/App.cs`：文件选择、运行队列、取消、重试、设置、更新。
- `src/Appearance.cs`：可替换的视觉逻辑，包括分段按键同步、刻度条、指示灯、拖放反馈和动效。
- `src/MainWindow.xaml`：界面资源。作为资源嵌入 EXE，由 `XamlReader.Load` 加载，不使用 XAML code-behind。

## 功能控件

保留下面的 `x:Name` 和控件类型即可沿用事件绑定。需要更改类型或名称时，同时修改 `App.cs` 的绑定。

| 类型 | 名称 |
| --- | --- |
| `ListView` | `Files`，使用 `GridView`，第一列为文件名；支持多选 |
| `TextBox` | `SearchBox`、`Details` |
| `ComboBox` | `Mode`、`Preset`、`StatusFilter`、`Overlay`、`Icons` |
| `CheckBox` | `AutoStart`、`Backup`、`AllMethods`、`CompressExports`、`StripRelocs`、`Force`、`AutoUpdate` |
| `Button` | `AddButton`、`FolderButton`、`StartButton`、`StopButton`、`RetryButton`、`RemoveButton`、`ClearButton`、`ExportButton`、`RevealButton` |
| `Button` | `EngineButton`、`DownloadButton`、`UpdateButton`、`ProjectButton`、`LicenseButton` |
| `TextBlock` | `Summary`、`EngineLabel`、`UpdateStatus`、`ProgressText`、`QueueCount`、`VisibleCount`、`TotalSize`、`SavedLabel`、`SavedSize`、`DoneCount`、`EmptyTitle`、`EmptyHint` |
| `ProgressBar` | `QueueProgress`，范围 0–100 |
| `FrameworkElement` | `AdvancedOptions`，包含备份及高级参数，处理期间整体禁用 |

`Files.ItemsSource` 由程序设置。每行可绑定 `Name`、`Folder`、`Path`、`Status`、`Before`、`After`、`Saving`。显示用的大小带单位；原始数据使用 `BeforeBytes`、`AfterBytes`。状态为：等待中、处理中、已压缩、已解压、已跳过、失败、已取消。

下拉框索引约定：

| 控件 | 0 | 1 | 2 | 3 |
| --- | --- | --- | --- | --- |
| `Mode` | 压缩 | 解压 | — | — |
| `Preset` | 均衡 -7 | 快速 -1 | 极致 --best | — |
| `StatusFilter` | 全部 | 等待 / 处理中 | 已完成（含跳过） | 失败 / 已取消 |
| `Overlay` | 保留 | 丢弃 | 跳过 | — |
| `Icons` | 不压缩 | 保留第一个图标 | 保留第一个图标目录 | 全部压缩 |

`Mode` 和 `Preset` 可直接显示，也可以保留为隐藏状态源，由新界面的按键设置 `SelectedIndex`。当前分段按键只是 `Appearance.cs` 中的可选适配。

## 可选视觉控件

`Workspace`、`DropSurface`、`DropTitle`、`ModeHint`、`ModeSelector`、`PresetSelector`、`CompressionOptions`、`PackMode`、`UnpackMode`、`FastPreset`、`BalancedPreset`、`BestPreset`、`AdvancedExpander`、`DetailsExpander`、`RunIndicator`、`MeterBefore`、`MeterAfter`、`InputMeter`、`OutputMeter` 均由 `Appearance.cs` 处理。可删除或重新实现；功能层不直接操作这些控件。

目前的颜色、动效和刻度样式不构成新 UI 的限制。新增动画应继续尊重 `SystemParameters.ClientAreaAnimation`。

## 验证与发布

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -ExecutionPolicy Bypass -File .\test-native.ps1
powershell -ExecutionPolicy Bypass -File .\package.ps1
```

`UiFlowTests` 会操作真实的 WPF 控件，覆盖导入、筛选、取消、重试、移除、模式切换和运行中追加文件。控件结构变化时同步调整这些测试；不要仅删除失败断言。`UiSmoke` 验证两种窗口尺寸和单 EXE 离线启动。

应用版本位于 `src/Updater.cs` 和 `src/AssemblyInfo.cs`，两处需要一致。匹配版本的 `v主.次.修订` 标签会触发 CI，测试通过后发布正式 Release。普通分支推送只生成构建产物。

应用图标源文件是 `src/Assets/AppIcon.xaml`，窗口与页头共用此矢量资源。修改后运行 `powershell -ExecutionPolicy Bypass -File src/Assets/Export-Icon.ps1`，生成随源码提交的多尺寸 ICO 和 PNG；构建将 ICO 嵌入 EXE。

## 新手演练与排版

`src/Tutorial.cs` 管理独立演示状态，`TutorialOverlay` 在工作区之上显示三步演练。关闭、返回、重开时会废弃尚未完成的演示操作，演练不会调用真实压缩队列或写入用户设置。底部和空队列各有一个入口；`Esc` 退出，Tab 焦点限制在演练面板内，退出后恢复原焦点。

`ModePill`、`PresetPill` 是选项滑动底板；选项改变和尺寸变化时由 `Appearance.cs` 定位，禁用动画时直接到达最终位置。

界面使用 Microsoft YaHei UI，字号仅用 12、13、16、24，字重只用 Normal 与 SemiBold。正文默认 13，辅助信息 12，分区标题 16，应用名称和教程标题 24。
