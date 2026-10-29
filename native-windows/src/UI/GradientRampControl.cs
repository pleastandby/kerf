using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Kerf.Engine;
using Kerf.Model;

namespace Kerf.UI
{
    /// <summary>
    /// Streamlined Figma-style Gradient Ramp & Control Panel.
    /// Provides an intuitive horizontal gradient track with circular stop handles,
    /// direct click-to-add, horizontal dragging, drag-down deletion, and compact stop controls.
    /// Works hand-in-hand with on-canvas gradient rollers.
    /// </summary>
    public class GradientRampControl : UserControl
    {
        private Paint _paint;
        private Action _onChanged;
        private Action _onStructureChanged;
        private int _selectedStopIndex = 0;

        // UI references
        private Border _trackBorder;
        private Rectangle _trackGradientRect;
        private Canvas _handlesCanvas;
        private Border _stopSwatch;
        private TextBox _stopHexBox;
        private Slider _stopOpacitySlider;
        private TextBlock _stopOpacityVal;
        private TextBox _stopPosBox;
        private Button _deleteStopBtn;
        private FrameworkElement _angleRow;
        private TextBox _angleBox;

        // Dragging state
        private bool _isDraggingHandle = false;
        private int _dragIndex = -1;
        private Ellipse _activeDragHandle = null;

        public int SelectedStopIndex
        {
            get { return _selectedStopIndex; }
            set
            {
                _selectedStopIndex = Math.Max(0, Math.Min(value, (_paint.GradientStops != null) ? _paint.GradientStops.Count - 1 : 0));
                UpdateTrackVisuals();
                RefreshStopDetails();
            }
        }

        public GradientRampControl(Paint paint, Action onChanged, Action onStructureChanged = null)
        {
            _paint = paint;
            _onChanged = onChanged;
            _onStructureChanged = onStructureChanged;

            EnsureStops();
            BuildUi();
        }

        private void EnsureStops()
        {
            if (_paint.GradientStops == null)
            {
                _paint.GradientStops = new List<GradientStopData>();
            }

            if (_paint.GradientStops.Count == 0)
            {
                _paint.GradientStops.Add(new GradientStopData(0.0, _paint.ColorHex ?? "#38BDF8", 1.0));
                _paint.GradientStops.Add(new GradientStopData(1.0, "#6366F1", 1.0));
            }
            else if (_paint.GradientStops.Count == 1)
            {
                _paint.GradientStops.Add(new GradientStopData(1.0, "#6366F1", 1.0));
            }

            if (_selectedStopIndex < 0 || _selectedStopIndex >= _paint.GradientStops.Count)
            {
                _selectedStopIndex = 0;
            }
        }

        private void BuildUi()
        {
            var mainStack = new StackPanel
            {
                Margin = new Thickness(0, 4, 0, 4)
            };

            // 1. TOOLBAR: Type Selector (Linear / Radial) & Reverse (⇄)
            var toolRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6), LastChildFill = false };

            var typeStack = new StackPanel { Orientation = Orientation.Horizontal };
            var linearBtn = CreateTypePill("Linear", _paint.Type == PaintType.LinearGradient, () =>
            {
                _paint.Type = PaintType.LinearGradient;
                if (_angleRow != null) _angleRow.Visibility = Visibility.Visible;
                NotifyChange();
                BuildUi();
            });
            var radialBtn = CreateTypePill("Radial", _paint.Type == PaintType.RadialGradient, () =>
            {
                _paint.Type = PaintType.RadialGradient;
                if (_angleRow != null) _angleRow.Visibility = Visibility.Collapsed;
                NotifyChange();
                BuildUi();
            });
            typeStack.Children.Add(linearBtn);
            typeStack.Children.Add(radialBtn);
            DockPanel.SetDock(typeStack, Dock.Left);
            toolRow.Children.Add(typeStack);

            var revBtn = CreateIconButton("⇄", "Reverse gradient direction (Flip)", () =>
            {
                ReverseGradient();
            });
            DockPanel.SetDock(revBtn, Dock.Right);
            toolRow.Children.Add(revBtn);

            mainStack.Children.Add(toolRow);

            // 2. LINEAR ANGLE CONTROLS
            _angleRow = BuildAngleRow();
            if (_paint.Type != PaintType.LinearGradient)
            {
                _angleRow.Visibility = Visibility.Collapsed;
            }
            mainStack.Children.Add(_angleRow);

            // 3. FIGMA-STYLE GRADIENT RAMP TRACK WITH EMBEDDED CIRCULAR HANDLES
            var stageContainer = new Grid
            {
                Height = 28,
                Margin = new Thickness(0, 2, 0, 6)
            };

            // Background Track Bar (Height 16px, rounded)
            _trackBorder = new Border
            {
                Height = 16,
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 88)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ToolTip = "Click track to insert a new gradient stop"
            };
            _trackBorder.Background = CreateCheckerboardBrush();

            _trackGradientRect = new Rectangle
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _trackBorder.Child = _trackGradientRect;
            stageContainer.Children.Add(_trackBorder);

            // Click track to add new stop
            _trackBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (_isDraggingHandle) return;
                Point pt = e.GetPosition(_trackBorder);
                double w = Math.Max(10.0, _trackBorder.ActualWidth);
                double pos = Math.Max(0.0, Math.Min(1.0, pt.X / w));

                Color sampled = GpuRenderer.SampleGradientColor(_paint.GradientStops, pos);
                string hex = string.Format("#{0:X2}{1:X2}{2:X2}", sampled.R, sampled.G, sampled.B);
                double op = sampled.A / 255.0;

                var newStop = new GradientStopData(Math.Round(pos, 3), hex, Math.Round(op, 2));
                _paint.GradientStops.Add(newStop);
                _paint.GradientStops.Sort((a, b) => a.Position.CompareTo(b.Position));
                _selectedStopIndex = _paint.GradientStops.IndexOf(newStop);

                UpdateTrackVisuals();
                RefreshStopDetails();
                NotifyChange();
            };

            // Handles Canvas (overlaid on top of track bar)
            _handlesCanvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = false,
                Background = Brushes.Transparent
            };
            stageContainer.Children.Add(_handlesCanvas);

            mainStack.Children.Add(stageContainer);

            _trackBorder.SizeChanged += (s, e) =>
            {
                UpdateTrackVisuals();
            };

            // 4. COMPACT ACTIVE STOP CONTROLS ROW
            var detailsPanel = BuildCompactStopDetails();
            mainStack.Children.Add(detailsPanel);

            Content = mainStack;

            UpdateTrackVisuals();
            RefreshStopDetails();
        }

        public void UpdateTrackVisuals()
        {
            if (_paint == null || _paint.GradientStops == null) return;

            // 1. Update gradient bar brush
            var lgb = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };
            GpuRenderer.PopulateGradientStops(lgb.GradientStops, _paint);
            _trackGradientRect.Fill = lgb;

            if (_angleBox != null)
            {
                _angleBox.Text = Math.Round(_paint.GradientAngle) + "°";
            }

            if (_isDraggingHandle) return;

            double width = _trackBorder.ActualWidth > 10 ? _trackBorder.ActualWidth : 220;

            // 2. Render circular stop handles along the track
            _handlesCanvas.Children.Clear();
            for (int k = 0; k < _paint.GradientStops.Count; k++)
            {
                int stopIdx = k;
                var stop = _paint.GradientStops[stopIdx];
                bool isSelected = (stopIdx == _selectedStopIndex);

                double handleX = stop.Position * width;
                double size = isSelected ? 16.0 : 14.0;

                var handle = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(GpuRenderer.ParseHexColor(stop.ColorHex, stop.Opacity)),
                    Stroke = isSelected ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : Brushes.White,
                    StrokeThickness = isSelected ? 2.5 : 1.8,
                    Cursor = Cursors.Hand,
                    ToolTip = string.Format("Stop {0}: {1} ({2:P0}, Opacity: {3:P0})\nDrag to reposition, double-click for color picker, drag down to delete",
                        stopIdx + 1, stop.ColorHex, stop.Position, stop.Opacity)
                };

                handle.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = isSelected ? 8 : 4,
                    ShadowDepth = 1,
                    Opacity = isSelected ? 0.9 : 0.6
                };

                Canvas.SetLeft(handle, handleX - size / 2.0);
                Canvas.SetTop(handle, (28.0 - size) / 2.0);

                handle.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    _selectedStopIndex = stopIdx;
                    _isDraggingHandle = true;
                    _dragIndex = stopIdx;
                    _activeDragHandle = handle;
                    handle.CaptureMouse();

                    UpdateTrackVisuals();
                    RefreshStopDetails();

                    if (e.ClickCount == 2)
                    {
                        OpenColorPickerForStop(stopIdx, handle);
                    }
                };

                handle.MouseMove += (s, e) =>
                {
                    if (_isDraggingHandle && _dragIndex == stopIdx && handle.IsMouseCaptured)
                    {
                        Point cur = e.GetPosition(_handlesCanvas);

                        // Drag down to delete (when > 2 stops)
                        if (cur.Y > 36 && _paint.GradientStops.Count > 2)
                        {
                            handle.Opacity = 0.35;
                        }
                        else
                        {
                            handle.Opacity = 1.0;
                            double newPos = cur.X / width;
                            newPos = Math.Max(0.0, Math.Min(1.0, newPos));
                            stop.Position = Math.Round(newPos, 3);

                            // Smooth in-place position update
                            double hx = stop.Position * width;
                            Canvas.SetLeft(handle, hx - size / 2.0);

                            // Smooth gradient brush update
                            var liveLgb = new LinearGradientBrush
                            {
                                StartPoint = new Point(0, 0.5),
                                EndPoint = new Point(1, 0.5),
                                MappingMode = BrushMappingMode.RelativeToBoundingBox
                            };
                            GpuRenderer.PopulateGradientStops(liveLgb.GradientStops, _paint);
                            _trackGradientRect.Fill = liveLgb;

                            if (_stopPosBox != null)
                            {
                                _stopPosBox.Text = Math.Round(stop.Position * 100.0) + "%";
                            }

                            NotifyChange();
                        }
                    }
                };

                handle.MouseLeftButtonUp += (s, e) =>
                {
                    if (_isDraggingHandle && _dragIndex == stopIdx)
                    {
                        Point cur = e.GetPosition(_handlesCanvas);
                        handle.ReleaseMouseCapture();
                        _isDraggingHandle = false;
                        _dragIndex = -1;
                        _activeDragHandle = null;

                        if (cur.Y > 36 && _paint.GradientStops.Count > 2)
                        {
                            _paint.GradientStops.RemoveAt(stopIdx);
                            _selectedStopIndex = Math.Max(0, Math.Min(_selectedStopIndex, _paint.GradientStops.Count - 1));
                            UpdateTrackVisuals();
                            RefreshStopDetails();
                            NotifyChange();
                            return;
                        }

                        _paint.GradientStops.Sort((a, b) => a.Position.CompareTo(b.Position));
                        _selectedStopIndex = _paint.GradientStops.IndexOf(stop);
                        UpdateTrackVisuals();
                        RefreshStopDetails();
                        NotifyChange();
                    }
                };

                _handlesCanvas.Children.Add(handle);
            }
        }

        private StackPanel BuildCompactStopDetails()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };

            // Row 1: Swatch + Hex + Pos + Delete Button
            var row1 = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = true };

            // Stop Swatch
            _stopSwatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 72, 100)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "Click to pick color"
            };
            _stopSwatch.MouseLeftButtonUp += (s, e) =>
            {
                OpenColorPickerForStop(_selectedStopIndex, _stopSwatch);
            };
            DockPanel.SetDock(_stopSwatch, Dock.Left);
            row1.Children.Add(_stopSwatch);

            // Delete Button (on the right)
            _deleteStopBtn = CreateIconButton("✕", "Remove stop (min 2)", () =>
            {
                DeleteSelectedStop();
            });
            _deleteStopBtn.Margin = new Thickness(4, 0, 0, 0);
            DockPanel.SetDock(_deleteStopBtn, Dock.Right);
            row1.Children.Add(_deleteStopBtn);

            // Hex Box
            var hexBorder = new Border
            {
                Width = 80,
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _stopHexBox = new TextBox
            {
                Text = "#FFFFFF",
                FontSize = 11,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            _stopHexBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyHexInput();
                    _stopHexBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            };
            _stopHexBox.LostFocus += (s, e) => ApplyHexInput();
            hexBorder.Child = _stopHexBox;
            DockPanel.SetDock(hexBorder, Dock.Left);
            row1.Children.Add(hexBorder);

            // Pos Box
            var posBorder = new Border
            {
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };
            _stopPosBox = new TextBox
            {
                Text = "0%",
                FontSize = 11,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            _stopPosBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyPosInput();
                }
            };
            _stopPosBox.LostFocus += (s, e) => ApplyPosInput();
            posBorder.Child = _stopPosBox;
            row1.Children.Add(posBorder);

            panel.Children.Add(row1);

            // Row 2: Opacity Slider
            var opRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var opLbl = new TextBlock
            {
                Text = "Opacity",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                Width = 46,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(opLbl, Dock.Left);
            opRow.Children.Add(opLbl);

            _stopOpacityVal = new TextBlock
            {
                Text = "100%",
                FontSize = 11,
                Foreground = Brushes.White,
                Width = 36,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_stopOpacityVal, Dock.Right);
            opRow.Children.Add(_stopOpacityVal);

            _stopOpacitySlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = 100,
                IsMoveToPointEnabled = true,
                Cursor = Cursors.Hand,
                Style = ScrubControls.GetSharedSliderStyle(),
                VerticalAlignment = VerticalAlignment.Center
            };
            _stopOpacitySlider.ValueChanged += (s, e) =>
            {
                var cur = GetCurrentStop();
                if (cur != null)
                {
                    cur.Opacity = Math.Round(_stopOpacitySlider.Value / 100.0, 2);
                    _stopOpacityVal.Text = Math.Round(_stopOpacitySlider.Value) + "%";
                    _stopSwatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(cur.ColorHex, cur.Opacity));
                    UpdateTrackVisuals();
                    NotifyChange();
                }
            };
            opRow.Children.Add(_stopOpacitySlider);
            panel.Children.Add(opRow);

            return panel;
        }

        private void RefreshStopDetails()
        {
            var cur = GetCurrentStop();
            if (cur == null) return;

            _stopSwatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(cur.ColorHex, cur.Opacity));
            _stopHexBox.Text = cur.ColorHex.ToUpperInvariant();
            _stopPosBox.Text = Math.Round(cur.Position * 100.0) + "%";
            _stopOpacitySlider.Value = cur.Opacity * 100.0;
            _stopOpacityVal.Text = Math.Round(cur.Opacity * 100.0) + "%";

            _deleteStopBtn.IsEnabled = (_paint.GradientStops.Count > 2);
            _deleteStopBtn.Opacity = (_paint.GradientStops.Count > 2) ? 1.0 : 0.4;
        }

        private GradientStopData GetCurrentStop()
        {
            if (_paint.GradientStops == null || _paint.GradientStops.Count == 0) return null;
            if (_selectedStopIndex < 0 || _selectedStopIndex >= _paint.GradientStops.Count)
                _selectedStopIndex = 0;
            return _paint.GradientStops[_selectedStopIndex];
        }

        private void ApplyHexInput()
        {
            var cur = GetCurrentStop();
            if (cur == null) return;
            string text = _stopHexBox.Text.Trim();
            if (!text.StartsWith("#")) text = "#" + text;

            try
            {
                Color c = (Color)ColorConverter.ConvertFromString(text);
                cur.ColorHex = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
                _stopSwatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(cur.ColorHex, cur.Opacity));
                UpdateTrackVisuals();
                NotifyChange();
            }
            catch
            {
                _stopHexBox.Text = cur.ColorHex;
            }
        }

        private void ApplyPosInput()
        {
            var cur = GetCurrentStop();
            if (cur == null) return;
            string text = _stopPosBox.Text.Replace("%", "").Trim();
            double val;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            {
                cur.Position = Math.Max(0.0, Math.Min(1.0, val / 100.0));
                _paint.GradientStops.Sort((a, b) => a.Position.CompareTo(b.Position));
                _selectedStopIndex = _paint.GradientStops.IndexOf(cur);
                UpdateTrackVisuals();
                RefreshStopDetails();
                NotifyChange();
            }
            else
            {
                _stopPosBox.Text = Math.Round(cur.Position * 100.0) + "%";
            }
        }

        private void OpenColorPickerForStop(int stopIndex, FrameworkElement anchor)
        {
            if (stopIndex < 0 || stopIndex >= _paint.GradientStops.Count) return;
            var stop = _paint.GradientStops[stopIndex];

            ColorPickerPopup.ShowAt(anchor, stop.ColorHex, newHex =>
            {
                stop.ColorHex = newHex;
                _stopHexBox.Text = newHex.ToUpperInvariant();
                _stopSwatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(newHex, stop.Opacity));
                UpdateTrackVisuals();
                NotifyChange();
            });
        }

        private void DeleteSelectedStop()
        {
            if (_paint.GradientStops.Count <= 2) return;
            if (_selectedStopIndex >= 0 && _selectedStopIndex < _paint.GradientStops.Count)
            {
                _paint.GradientStops.RemoveAt(_selectedStopIndex);
                _selectedStopIndex = Math.Max(0, _selectedStopIndex - 1);
                UpdateTrackVisuals();
                RefreshStopDetails();
                NotifyChange();
            }
        }

        private void ReverseGradient()
        {
            if (_paint.GradientStops == null || _paint.GradientStops.Count < 2) return;

            foreach (var st in _paint.GradientStops)
            {
                st.Position = Math.Round(1.0 - st.Position, 4);
            }
            _paint.GradientStops.Sort((a, b) => a.Position.CompareTo(b.Position));
            _selectedStopIndex = 0;

            if (_paint.GradientStart.HasValue && _paint.GradientEnd.HasValue)
            {
                var tmp = _paint.GradientStart.Value;
                _paint.GradientStart = _paint.GradientEnd.Value;
                _paint.GradientEnd = tmp;
            }
            _paint.GradientAngle = (_paint.GradientAngle + 180.0) % 360.0;

            UpdateTrackVisuals();
            RefreshStopDetails();
            NotifyChange();
        }

        private FrameworkElement BuildAngleRow()
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var angleLbl = new TextBlock
            {
                Text = "Angle",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                Width = 42,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(angleLbl, Dock.Left);
            dock.Children.Add(angleLbl);

            var angleBoxBorder = new Border
            {
                Width = 52,
                Height = 22,
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            _angleBox = new TextBox
            {
                Text = Math.Round(_paint.GradientAngle) + "°",
                FontSize = 11,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 2, 0)
            };
            _angleBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyAngleInput();
                }
            };
            _angleBox.LostFocus += (s, e) => ApplyAngleInput();
            angleBoxBorder.Child = _angleBox;
            DockPanel.SetDock(angleBoxBorder, Dock.Left);
            dock.Children.Add(angleBoxBorder);

            // Quick Angle Presets: 0°, 45°, 90°, 180°
            var presetsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            presetsStack.Children.Add(CreateAnglePill("0°", 0));
            presetsStack.Children.Add(CreateAnglePill("45°", 45));
            presetsStack.Children.Add(CreateAnglePill("90°", 90));
            presetsStack.Children.Add(CreateAnglePill("180°", 180));
            dock.Children.Add(presetsStack);

            return dock;
        }

        private void ApplyAngleInput()
        {
            string text = _angleBox.Text.Replace("°", "").Trim();
            double val;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            {
                _paint.GradientAngle = (val % 360.0 + 360.0) % 360.0;
                _paint.GradientStart = null;
                _paint.GradientEnd = null;
                _angleBox.Text = Math.Round(_paint.GradientAngle) + "°";
                NotifyChange();
            }
            else
            {
                _angleBox.Text = Math.Round(_paint.GradientAngle) + "°";
            }
        }

        private Button CreateAnglePill(string label, double angle)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 9.5,
                Padding = new Thickness(4, 1, 4, 1),
                Height = 22,
                Margin = new Thickness(2, 0, 0, 0),
                Background = Math.Abs(_paint.GradientAngle - angle) < 1.0 ? new SolidColorBrush(Color.FromRgb(45, 54, 80)) : new SolidColorBrush(Color.FromRgb(24, 28, 40)),
                Foreground = Math.Abs(_paint.GradientAngle - angle) < 1.0 ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 46, 66)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) =>
            {
                _paint.GradientAngle = angle;
                _paint.GradientStart = null;
                _paint.GradientEnd = null;
                if (_angleBox != null) _angleBox.Text = angle + "°";
                NotifyChange();
                BuildUi();
            };
            return btn;
        }

        private Button CreateTypePill(string label, bool isActive, Action onClick)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 10,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                Height = 22,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Background = isActive ? new SolidColorBrush(Color.FromRgb(67, 56, 202)) : new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                BorderBrush = isActive ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(40, 48, 70)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Button CreateIconButton(string icon, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Content = icon,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Width = 22,
                Height = 22,
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 48)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 220)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 78)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private static DrawingBrush CreateCheckerboardBrush()
        {
            var group = new DrawingGroup();
            var rect1 = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(32, 38, 54)),
                null,
                new RectangleGeometry(new Rect(0, 0, 8, 8)));
            var rect2 = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(22, 26, 38)),
                null,
                new RectangleGeometry(new Rect(0, 0, 4, 4)));
            var rect3 = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(22, 26, 38)),
                null,
                new RectangleGeometry(new Rect(4, 4, 4, 4)));

            group.Children.Add(rect1);
            group.Children.Add(rect2);
            group.Children.Add(rect3);

            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 8, 8),
                ViewportUnits = BrushMappingMode.Absolute
            };
            brush.Freeze();
            return brush;
        }

        private void NotifyChange()
        {
            if (_onChanged != null) _onChanged();
        }
    }
}
