# 新界面接入

当前 `src/MainWindow.xaml` 是待替换的界面稿。功能已接好；新设计可以重做布局、配色、字体和动效。

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

版本号已预留为 1.2.0。按本轮约定，功能先推送；新 UI 合并并验证后，再创建 `v1.2.0` 标签，由 CI 发布 Release。
