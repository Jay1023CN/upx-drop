// SPDX-License-Identifier: GPL-2.0-or-later
// Optional presentation hooks. Keep queue operations in App.cs and Queue.cs.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace UpxDrop {
 public partial class Program {
  void LoadBrandIcon() {
   using (var stream = typeof(Program).Assembly.GetManifestResourceStream("AppIcon.xaml")) {
    var icon = (DrawingImage)System.Windows.Markup.XamlReader.Load(stream);
    icon.Freeze(); window.Icon = icon;
    var brand = Optional<Image>("BrandIcon"); if (brand != null) brand.Source = icon;
   }
  }
  T Optional<T>(string name) where T : class { return window.FindName(name) as T; }
  static T VisualChild<T>(DependencyObject parent) where T : DependencyObject {
   for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
    var child = VisualTreeHelper.GetChild(parent, i); var match = child as T;
    if (match != null) return match;
    match = VisualChild<T>(child); if (match != null) return match;
   }
   return null;
  }
  void InstallEmptyStateLayout() {
   var host = Optional<Grid>("EmptyStateHost"); var icon = Optional<Border>("EmptyIcon"); var hint = Optional<TextBlock>("EmptyHint");
   if (host == null || icon == null || hint == null) return;
   files.LayoutUpdated += delegate {
    if (files.Items.Count != 0) return;
    var header = VisualChild<GridViewHeaderRowPresenter>(files);
    if (header != null && header.ActualHeight > 0) {
     double top = Math.Ceiling(header.TranslatePoint(new Point(0, header.ActualHeight), files).Y);
     if (Math.Abs(host.Margin.Top - top) > 0.1) host.Margin = new Thickness(0, top, 0, 0);
    }
    icon.Visibility = host.ActualHeight < 164 ? Visibility.Collapsed : Visibility.Visible;
    hint.Visibility = host.ActualHeight < 120 ? Visibility.Collapsed : Visibility.Visible;
   };
  }
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
   SetText("ModeHint", packing ? new[] { "兼顾处理速度和文件大小。", "优先处理速度。", "尝试更充分的压缩，耗时较长。" }[preset.SelectedIndex] : "还原 UPX 压缩过的文件，体积会增大。");
   SetText("DropTitle", packing ? "拖入文件" : "拖入待解压文件");
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
  Brush normalDropBackground;
  bool dropHighlighted;
  void HighlightDrop() {
   var border = Optional<Border>("DropSurface"); if (border == null) return;
   if (dropHighlighted) return; dropHighlighted = true;
   if (normalDropBorder == null) normalDropBorder = border.BorderBrush;
   normalDropBackground = border.Background;
   border.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
   border.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
   SetText("DropTitle", "松开鼠标，加入队列");
  }
  void ResetDrop() {
   var border = Optional<Border>("DropSurface"); if (border == null || !dropHighlighted) return;
   dropHighlighted = false; border.BorderBrush = normalDropBorder; border.Background = normalDropBackground;
   SetText("DropTitle", mode.SelectedIndex == 0 ? "拖入文件" : "拖入待解压文件");
  }
  void AnimateEntrance() {
   if (!SystemParameters.ClientAreaAnimation) return;
   var target = Optional<FrameworkElement>("Workspace"); if (target == null) return;
   target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
   var translate = new TranslateTransform(); target.RenderTransform = translate;
   translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(3, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
  }
  void InstallMotion() {
   InstallSelectionMotion("ModeSelector", "ModePill");
   InstallSelectionMotion("PresetSelector", "PresetPill");
   if (!SystemParameters.ClientAreaAnimation) return;
   foreach (string name in new[] { "AddButton", "FolderButton", "StartButton", "StopButton", "RetryButton", "ExportButton" }) {
    var button = Optional<Button>(name); if (button == null) continue; var scale = new ScaleTransform(1, 1);
    button.RenderTransform = scale; button.RenderTransformOrigin = new Point(0.5, 0.5);
    button.MouseLeave += delegate { AnimateScale(scale, 1); };
    button.PreviewMouseLeftButtonDown += delegate { AnimateScale(scale, 0.985); };
    button.PreviewMouseLeftButtonUp += delegate { AnimateScale(scale, 1); };
   }
   foreach (string name in new[] { "AdvancedExpander", "DetailsExpander", "EngineExpander" }) {
    var expander = Optional<Expander>(name); if (expander == null) continue; var content = expander.Content as FrameworkElement; if (content == null) continue;
    expander.Expanded += delegate {
     content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
     var shift = new TranslateTransform(0, -5); content.RenderTransform = shift;
     shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    };
   }
  }
  void InstallSelectionMotion(string selectorName, string pillName) {
   var selector = Optional<System.Windows.Controls.Primitives.UniformGrid>(selectorName); var pill = Optional<Border>(pillName);
   if (selector == null || pill == null) return;
   var slide = new TranslateTransform(); pill.RenderTransform = slide;
   Action<bool> position = animate => {
    if (selector.ActualWidth <= 0 || selector.Children.Count == 0) return;
    double width = selector.ActualWidth / selector.Children.Count; int selected = 0;
    for (int i = 0; i < selector.Children.Count; i++) if (((RadioButton)selector.Children[i]).IsChecked == true) selected = i;
    double previous = slide.X, target = width * selected; pill.Width = width;
    slide.BeginAnimation(TranslateTransform.XProperty, null); slide.X = target;
    if (animate && SystemParameters.ClientAreaAnimation) slide.BeginAnimation(TranslateTransform.XProperty,
     new DoubleAnimation(previous, target, TimeSpan.FromMilliseconds(190)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
   };
   selector.SizeChanged += delegate { position(false); };
   selector.IsEnabledChanged += delegate { pill.Opacity = selector.IsEnabled ? 1 : 0.45; };
   foreach (RadioButton radio in selector.Children) radio.Checked += delegate { position(true); };
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
