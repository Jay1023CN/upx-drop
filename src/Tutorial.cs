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
  bool tutorialOpen;
  int tutorialStep, tutorialGeneration;
  IInputElement tutorialReturnFocus;
  void InstallTutorial() {
   window.Closed += delegate { tutorialGeneration++; tutorialOpen = false; };
   foreach (var name in new[] { "TutorialButton", "EmptyTutorialButton" }) {
    var button = Optional<Button>(name); if (button != null) button.Click += delegate { OpenTutorial(); };
   }
   Find<Button>("TutorialClose").Click += delegate { CloseTutorial(); };
   Find<Button>("TutorialBack").Click += delegate { if (tutorialStep > 0) { tutorialStep--; ShowTutorialPage(); FocusTutorialPage(); } };
   Find<Button>("TutorialNext").Click += delegate {
    if (tutorialStep == 2) CloseTutorial(); else { tutorialStep++; ShowTutorialPage(); FocusTutorialPage(); }
   };

  }
  void FocusTutorialPage() { Find<Button>("TutorialNext").Focus(); }
  void OpenTutorial() {
   if (tutorialOpen) return;
   tutorialReturnFocus = Keyboard.FocusedElement;
   ResetDrop();
   tutorialOpen = true; tutorialStep = 0;
   Find<FrameworkElement>("Workspace").IsEnabled = false;
   Find<Grid>("TutorialOverlay").Visibility = Visibility.Visible;
   ShowTutorialPage();
   FocusTutorialPage();
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
   int generation = ++tutorialGeneration;
   SetText("TutorialStepLabel", (tutorialStep + 1) + " / 3");
   SetText("TutorialTitle", new[] { "添加文件", "选择压缩强度", "查看处理结果" }[tutorialStep]);
   SetText("TutorialBody", new[] {
    "把 EXE、DLL 或文件夹拖进窗口，也可以点“添加文件”。",
    "选好强度后，点“开始压缩”。平时用“均衡”就行，想快一点可以选“快速”。",
    "这里能看到文件状态和前后大小。处理失败就点“重试未完成”，要保存结果就导出 CSV。"
   }[tutorialStep]);
   SetText("TutorialNote", new[] {
    "要一次加入多个文件，可以直接拖入文件夹。",
    "“添加后自动开始”打开时，加入文件就会开始处理。",
    "想留下原文件，处理前勾选“保留原文件备份”。"
   }[tutorialStep]);
   SetText("TutorialFileStatus", tutorialStep == 0 ? "拖入文件 · 8.0 MB" : tutorialStep == 1 ? "正在压缩…" : "已压缩 · 8.0 MB → 2.4 MB");
   Find<StackPanel>("TutorialPresets").Visibility = tutorialStep == 1 ? Visibility.Visible : Visibility.Collapsed;
   Find<Button>("TutorialBack").IsEnabled = tutorialStep > 0;
   Find<Button>("TutorialNext").Content = tutorialStep == 2 ? "开始使用" : "下一步";
   for (int i = 0; i < 3; i++) Find<Border>("TutorialDot" + i).Background = new SolidColorBrush(i <= tutorialStep ? Color.FromRgb(37, 99, 235) : Color.FromRgb(226, 232, 240));
   var bar = Find<ProgressBar>("TutorialProgress"); bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null); bar.Value = tutorialStep == 2 ? 100 : 0;
   bar.Visibility = tutorialStep == 0 ? Visibility.Hidden : Visibility.Visible;
   var file = Find<Border>("TutorialFile"); file.BeginAnimation(UIElement.OpacityProperty, null); file.Opacity = tutorialStep == 0 ? 0.55 : 1;
   if (SystemParameters.ClientAreaAnimation) {
    var page = Find<Grid>("TutorialPage"); var slide = new TranslateTransform(10, 0); page.RenderTransform = slide;
    page.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(180)));
    slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
   }
   PlayTutorialPreview(generation, tutorialStep);
  }
  async void PlayTutorialPreview(int generation, int step) {
   if (step == 2) return;
   if (step == 0) Find<Border>("TutorialFile").Opacity = 1;
   else Find<ProgressBar>("TutorialProgress").Value = 100;
   if (SystemParameters.ClientAreaAnimation) {
    if (step == 0) {
     var file = Find<Border>("TutorialFile");
     file.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(450)));
    } else {
     var bar = Find<ProgressBar>("TutorialProgress"); bar.Value = 100;
     bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new DoubleAnimation(0, 100, TimeSpan.FromMilliseconds(700)) { FillBehavior = FillBehavior.Stop });
    }
    await Task.Delay(step == 0 ? 500 : 750);
   }
   if (generation != tutorialGeneration || !tutorialOpen) return;
   SetText("TutorialFileStatus", step == 0 ? "已加入队列 · 8.0 MB" : "已压缩 · 8.0 MB → 2.4 MB");
  }
 }
}
