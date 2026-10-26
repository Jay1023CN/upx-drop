// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace UpxDrop {
 public partial class Program {
  bool tutorialOpen, tutorialReady;
  int tutorialStep, tutorialPreset = 1, tutorialGeneration;
  IInputElement tutorialReturnFocus;
  readonly string[] tutorialSizes = { "3.2 MB", "2.4 MB", "2.1 MB" };
  void InstallTutorial() {
   window.Closed += delegate { tutorialGeneration++; tutorialOpen = false; };
   foreach (var name in new[] { "TutorialButton", "EmptyTutorialButton" }) {
    var button = Optional<Button>(name); if (button != null) button.Click += delegate { OpenTutorial(); };
   }
   Find<Button>("TutorialClose").Click += delegate { CloseTutorial(); };
   Find<Button>("TutorialBack").Click += delegate { if (tutorialStep > 0) { tutorialStep--; ShowTutorialPage(); FocusTutorialPage(); } };
   Find<Button>("TutorialNext").Click += delegate {
    if (!tutorialReady) return;
    if (tutorialStep == 2) CloseTutorial(); else { tutorialStep++; ShowTutorialPage(); FocusTutorialPage(); }
   };
   Find<Button>("TutorialAction").Click += async delegate { await RunTutorialAction(); };
   string[] names = { "TutorialFast", "TutorialBalanced", "TutorialBest" };
   for (int i = 0; i < names.Length; i++) {
    int index = i; Find<Button>(names[i]).Click += delegate { tutorialPreset = index; ShowTutorialPage(); };
   }
  }
  void FocusTutorialPage() { Find<Button>(tutorialStep == 2 ? "TutorialNext" : "TutorialAction").Focus(); }
  void OpenTutorial() {
   if (tutorialOpen) return;
   tutorialReturnFocus = Keyboard.FocusedElement;
   ResetDrop();
   tutorialOpen = true; tutorialStep = 0; tutorialPreset = 1;
   Find<FrameworkElement>("Workspace").IsEnabled = false;
   Find<Grid>("TutorialOverlay").Visibility = Visibility.Visible;
   ShowTutorialPage();
   Find<Button>("TutorialAction").Focus();
  }
  void CloseTutorial() {
   if (!tutorialOpen) return;
   tutorialGeneration++; tutorialOpen = false;
   Find<Grid>("TutorialOverlay").Visibility = Visibility.Collapsed;
   Find<FrameworkElement>("Workspace").IsEnabled = true;
   var prior = tutorialReturnFocus as UIElement;
   if (prior != null && prior.IsVisible && prior.IsEnabled) Keyboard.Focus(prior);
   else Find<Button>("TutorialButton").Focus();
  }
  void ShowTutorialPage() {
   tutorialGeneration++; tutorialReady = tutorialStep == 2;
   SetText("TutorialStepLabel", (tutorialStep + 1) + " / 3");
   SetText("TutorialTitle", new[] { "添加文件", "选择压缩强度", "查看处理结果" }[tutorialStep]);
   SetText("TutorialBody", new[] {
    "先点“添加示例”试一次。正式使用时，把 EXE、DLL 或文件夹拖进窗口，也可以点“添加文件”。",
    "选好强度，点“压缩示例”。平时用“均衡”就行，想快一点可以选“快速”。",
    "这里能看到文件状态和前后大小。处理失败就点“重试未完成”，要保存结果就导出 CSV。"
   }[tutorialStep]);
   SetText("TutorialNote", new[] {
    "教程用的是示例，不会动你的文件。",
    "“添加后自动开始”打开时，加入文件就会开始处理。",
    "想留下原文件，处理前勾选“保留原文件备份”。"
   }[tutorialStep]);
   SetText("TutorialFileStatus", tutorialStep == 0 ? "示例文件 · 8.0 MB" : tutorialStep == 1 ? "已加入 · 等待压缩" : "已压缩 · 8.0 MB → " + tutorialSizes[tutorialPreset]);
   Find<StackPanel>("TutorialPresets").Visibility = tutorialStep == 1 ? Visibility.Visible : Visibility.Collapsed;
   Find<Button>("TutorialAction").Visibility = tutorialStep == 2 ? Visibility.Hidden : Visibility.Visible;
   Find<Button>("TutorialAction").Content = tutorialStep == 0 ? "添加示例" : "压缩示例";
   Find<Button>("TutorialAction").IsEnabled = true;
   Find<Button>("TutorialBack").IsEnabled = tutorialStep > 0;
   Find<Button>("TutorialNext").Content = tutorialStep == 2 ? "开始使用" : "下一步";
   Find<Button>("TutorialNext").IsEnabled = tutorialReady;
   string[] names = { "TutorialFast", "TutorialBalanced", "TutorialBest" };
   for (int i = 0; i < names.Length; i++) {
    var choice = Find<Button>(names[i]); choice.Background = new SolidColorBrush(i == tutorialPreset ? Color.FromRgb(219, 234, 254) : Colors.White);
    choice.FontWeight = i == tutorialPreset ? FontWeights.SemiBold : FontWeights.Normal;
    System.Windows.Automation.AutomationProperties.SetHelpText(choice, i == tutorialPreset ? "当前选中" : "点击选择此压缩强度");
   }
   for (int i = 0; i < 3; i++) Find<Border>("TutorialDot" + i).Background = new SolidColorBrush(i <= tutorialStep ? Color.FromRgb(37, 99, 235) : Color.FromRgb(226, 232, 240));
   var bar = Find<ProgressBar>("TutorialProgress"); bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null); bar.Value = tutorialStep == 2 ? 100 : 0;
   var file = Find<Border>("TutorialFile"); file.BeginAnimation(UIElement.OpacityProperty, null); file.Opacity = tutorialStep == 0 ? 0.55 : 1;
   if (SystemParameters.ClientAreaAnimation) {
    var page = Find<Grid>("TutorialPage"); var slide = new TranslateTransform(10, 0); page.RenderTransform = slide;
    page.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(180)));
    slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
   }
  }
  async Task RunTutorialAction() {
   if (!tutorialOpen || !Find<Button>("TutorialAction").IsEnabled || tutorialStep == 2) return;
   int generation = tutorialGeneration;
   Find<Button>("TutorialAction").IsEnabled = false;
   if (tutorialStep == 0) {
    Find<Border>("TutorialFile").Opacity = 1;
    SetText("TutorialFileStatus", "已加入 · 等待压缩");
    Find<Button>("TutorialAction").Content = "已加入队列";
   } else {
    SetText("TutorialFileStatus", "正在压缩示例…");
    var bar = Find<ProgressBar>("TutorialProgress"); bar.Value = 100;
    if (SystemParameters.ClientAreaAnimation) {
     bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new DoubleAnimation(0, 100, TimeSpan.FromMilliseconds(700)) { FillBehavior = FillBehavior.Stop });
     await Task.Delay(750);
    }
    if (generation != tutorialGeneration || !tutorialOpen) return;
    SetText("TutorialFileStatus", "8.0 MB → " + tutorialSizes[tutorialPreset]);
    Find<Button>("TutorialAction").Content = "已压缩";
   }
   tutorialReady = true; Find<Button>("TutorialNext").IsEnabled = true;
   if (window.IsActive) Find<Button>("TutorialNext").Focus();
  }
 }
}
