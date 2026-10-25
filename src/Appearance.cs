// SPDX-License-Identifier: GPL-2.0-or-later
// Optional presentation hooks. Keep queue operations in App.cs and Queue.cs.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace UpxDrop {
 public partial class Program {
  T Optional<T>(string name) where T : class { return window.FindName(name) as T; }
  void BindModeSelectors() {
   SetText("AppVersion", "v" + Updater.CurrentVersion);
   BindSelector("PackMode", mode, 0); BindSelector("UnpackMode", mode, 1);
   BindSelector("BalancedPreset", preset, 0); BindSelector("FastPreset", preset, 1); BindSelector("BestPreset", preset, 2);
   preset.SelectionChanged += delegate { UpdateMode(); };
  }
  void BindSelector(string name, ComboBox selector, int index) {
   var radio = Optional<RadioButton>(name); if (radio != null) radio.Checked += delegate { selector.SelectedIndex = index; };
  }
  void SetChecked(string name, bool value) { var radio = Optional<RadioButton>(name); if (radio != null) radio.IsChecked = value; }
  void SetEnabled(string name, bool value) { var control = Optional<FrameworkElement>(name); if (control != null) control.IsEnabled = value; }
  void SetText(string name, string value) { var control = Optional<TextBlock>(name); if (control != null) control.Text = value; }
  void SyncModePresentation(bool packing) {
   SetEnabled("PresetSelector", !busy && packing); SetEnabled("ModeSelector", !busy);
   SetChecked("PackMode", packing); SetChecked("UnpackMode", !packing);
   SetChecked("BalancedPreset", preset.SelectedIndex == 0); SetChecked("FastPreset", preset.SelectedIndex == 1); SetChecked("BestPreset", preset.SelectedIndex == 2);
   SetEnabled("CompressionOptions", !busy && packing);
   SetText("ModeHint", packing ? "压缩后校验通过，再替换原文件。" : "还原 UPX 压缩文件，体积会增大。");
   SetText("DropTitle", packing ? "投放文件" : "投放待解压文件");
  }
  void UpdateActivityIndicator() {
   var indicator = Optional<System.Windows.Shapes.Ellipse>("RunIndicator");
   if (indicator != null) indicator.Fill = new SolidColorBrush(busy || scanning ? Color.FromRgb(37, 99, 235) : Color.FromRgb(16, 185, 129));
  }
  void UpdateSizeMeters(long input, long output) {
   SetText("MeterBefore", QueueTools.Size(input)); SetText("MeterAfter", input == 0 ? "—" : QueueTools.Size(output));
   long peak = Math.Max(input, output);
   UpdateMeter("InputMeter", peak == 0 ? 0 : input * 1.0 / peak, false);
   UpdateMeter("OutputMeter", peak == 0 ? 0 : output * 1.0 / peak, true);
  }
  Brush normalDropBorder;
  void HighlightDrop() {
   var border = Optional<Border>("DropSurface"); if (border == null) return;
   if (normalDropBorder == null) normalDropBorder = border.BorderBrush;
   border.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
  }
  void ResetDrop() { var border = Optional<Border>("DropSurface"); if (border != null && normalDropBorder != null) border.BorderBrush = normalDropBorder; }
  void AnimateEntrance() {
   if (!SystemParameters.ClientAreaAnimation) return;
   var target = Optional<FrameworkElement>("Workspace"); if (target == null) return;
   target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
   var translate = new TranslateTransform(); target.RenderTransform = translate;
   translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(3, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
  }
  void InstallMotion() {
   if (!SystemParameters.ClientAreaAnimation) return;
   foreach (string name in new[] { "AddButton", "FolderButton", "StartButton", "StopButton", "RetryButton", "ExportButton" }) {
    var button = Optional<Button>(name); if (button == null) continue; var scale = new ScaleTransform(1, 1);
    button.RenderTransform = scale; button.RenderTransformOrigin = new Point(0.5, 0.5);
    button.MouseEnter += delegate { AnimateScale(scale, 1.01); };
    button.MouseLeave += delegate { AnimateScale(scale, 1); };
    button.PreviewMouseLeftButtonDown += delegate { AnimateScale(scale, 0.985); };
    button.PreviewMouseLeftButtonUp += delegate { AnimateScale(scale, 1); };
   }
   foreach (string name in new[] { "PackMode", "UnpackMode", "FastPreset", "BalancedPreset", "BestPreset" }) {
    var radio = Optional<RadioButton>(name); if (radio == null) continue;
    radio.Checked += delegate { radio.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(130))); };
   }
   foreach (string name in new[] { "AdvancedExpander", "DetailsExpander" }) {
    var expander = Optional<Expander>(name); if (expander == null) continue; var content = expander.Content as FrameworkElement; if (content == null) continue;
    expander.Expanded += delegate { content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))); };
   }
  }
  static void AnimateScale(ScaleTransform transform, double value) {
   var animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
   transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation); transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
  }
  void SetProgress(double value) {
   double previous = progress.Value;
   progress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
   progress.Value = value;
   if (SystemParameters.ClientAreaAnimation && value > previous) progress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,
    new DoubleAnimation(previous, value, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
  }
  void UpdateMeter(string name, double ratio, bool orange) {
   var meter = Optional<System.Windows.Controls.Primitives.UniformGrid>(name); if (meter == null) return;
   if (meter.Children.Count == 0) for (int i = 0; i < 24; i++) meter.Children.Add(new Border { Margin = new Thickness(0, 0, 2, 0), CornerRadius = new CornerRadius(2) });
   for (int i = 0; i < 24; i++) {
    var bar = (Border)meter.Children[i]; Color color = i < Math.Ceiling(ratio * 24) ? (orange ? Color.FromRgb(16, 185, 129) : Color.FromRgb(37, 99, 235)) : Color.FromRgb(226, 232, 240);
    var brush = bar.Background as SolidColorBrush;
    if (brush == null) bar.Background = new SolidColorBrush(color);
    else if (SystemParameters.ClientAreaAnimation) brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(180)));
    else brush.Color = color;
   }
  }
 }
}
