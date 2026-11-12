using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kerf.Engine.Animation;
using Kerf.Model;

namespace Kerf.UI.Dialogs
{
    public enum OrphanResolutionAction
    {
        Keep = 0,
        Rebind = 1,
        Purge = 2
    }

    public class OrphanResolution
    {
        public OrphanResolutionAction Action { get; set; }
        public string NewTargetLayerId { get; set; }

        public OrphanResolution()
        {
            Action = OrphanResolutionAction.Keep;
            NewTargetLayerId = string.Empty;
        }

        public OrphanResolution(OrphanResolutionAction action, string newTargetLayerId)
        {
            Action = action;
            NewTargetLayerId = newTargetLayerId ?? string.Empty;
        }
    }

    public class MigrationReviewDialog : Window
    {
        private readonly KerfDocument _doc;
        private readonly MigrationReport _report;
        private readonly List<LayerNode> _availableLayers;
        private readonly Dictionary<string, ComboBox> _actionCombos;
        private readonly Dictionary<string, ComboBox> _rebindCombos;
        private readonly Dictionary<string, OrphanResolution> _resolutions;

        public MigrationReviewDialog(KerfDocument doc, MigrationReport report = null)
        {
            _doc = doc ?? new KerfDocument();
            _report = report ?? new MigrationReport();
            _availableLayers = CollectAllDocumentLayers(_doc);
            _actionCombos = new Dictionary<string, ComboBox>();
            _rebindCombos = new Dictionary<string, ComboBox>();
            _resolutions = new Dictionary<string, OrphanResolution>();

            Title = "Migration Review — Unresolved Animation References";
            Width = 720;
            Height = 540;
            MinWidth = 600;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = KerfBrand.ObsidianBrush;
            Foreground = KerfBrand.TextWhiteBrush;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Resources[typeof(TextBox)] = KerfBrand.CreateDarkTextBoxStyle(6);
            Resources[typeof(ComboBox)] = KerfBrand.CreateDarkComboBoxStyle(6);

            BuildUI();
        }

        private static List<LayerNode> CollectAllDocumentLayers(KerfDocument doc)
        {
            var list = new List<LayerNode>();
            if (doc != null && doc.Pages != null)
            {
                foreach (var page in doc.Pages)
                {
                    if (page == null || page.Layers == null) continue;
                    CollectRecursive(page.Layers, list);
                }
            }
            return list;
        }

        private static void CollectRecursive(List<LayerNode> layers, List<LayerNode> output)
        {
            if (layers == null || output == null) return;
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l == null) continue;
                output.Add(l);
                if (l.Children != null && l.Children.Count > 0)
                {
                    CollectRecursive(l.Children, output);
                }
            }
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();
            rootGrid.Margin = new Thickness(16);
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Cards ScrollViewer
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Buttons

            // --- HEADER ---
            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var titleText = new TextBlock
            {
                Text = "Schema 4.0.0 Migration Report",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(titleText);

            var versionBadge = new Border
            {
                Background = KerfBrand.FigmaCardBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = string.Format("Source: {0} -> Target: {1}", _report.SourceVersion, _report.TargetVersion),
                    FontSize = 11,
                    Foreground = KerfBrand.MintBrush,
                    FontWeight = FontWeights.SemiBold
                }
            };
            titleRow.Children.Add(versionBadge);
            headerStack.Children.Add(titleRow);

            var summaryText = new TextBlock
            {
                Text = string.Format("Scanned {0} document layers. Resolved {1} tracks & {2} listeners. Found {3} orphaned reference(s) requiring review.",
                    _report.TotalLayersChecked,
                    _report.ResolvedTracksCount,
                    _report.ResolvedListenersCount,
                    _report.OrphanedItems.Count),
                FontSize = 12,
                Foreground = KerfBrand.TextMutedBrush,
                Margin = new Thickness(0, 0, 0, 6)
            };
            headerStack.Children.Add(summaryText);

            var explanationText = new TextBlock
            {
                Text = "Animation tracks or interactive listeners reference objects that no longer exist in the document. Choose whether to Keep them inactive, Rebind them to an existing layer, or Purge them.",
                FontSize = 11,
                Foreground = KerfBrand.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap
            };
            headerStack.Children.Add(explanationText);

            Grid.SetRow(headerStack, 0);
            rootGrid.Children.Add(headerStack);

            // --- CARDS LIST ---
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 14)
            };

            var itemsStack = new StackPanel();

            if (_report.OrphanedItems == null || _report.OrphanedItems.Count == 0)
            {
                var emptyCard = new Border
                {
                    Background = KerfBrand.FigmaCardBrush,
                    BorderBrush = KerfBrand.FigmaBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(20),
                    Child = new TextBlock
                    {
                        Text = "No unresolved references found. All tracks and listeners mapped cleanly.",
                        Foreground = KerfBrand.MintBrush,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                itemsStack.Children.Add(emptyCard);
            }
            else
            {
                for (int i = 0; i < _report.OrphanedItems.Count; i++)
                {
                    var item = _report.OrphanedItems[i];
                    if (item == null) continue;
                    itemsStack.Children.Add(CreateItemCard(item));
                }
            }

            scroll.Content = itemsStack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            // --- FOOTER BUTTONS ---
            var footerDock = new DockPanel { LastChildFill = false };

            var keepAllBtn = new Button
            {
                Content = "Keep All Inactive",
                Width = 140,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                Background = KerfBrand.FigmaCardBrush,
                Foreground = KerfBrand.TextWhiteBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            keepAllBtn.Click += (s, e) =>
            {
                foreach (var combo in _actionCombos.Values)
                {
                    combo.SelectedIndex = 0; // Keep
                }
                DialogResult = true;
                Close();
            };
            DockPanel.SetDock(keepAllBtn, Dock.Left);
            footerDock.Children.Add(keepAllBtn);

            var applyBtn = new Button
            {
                Content = "Apply & Continue",
                Width = 150,
                Height = 32,
                Background = KerfBrand.MintBrush,
                Foreground = KerfBrand.TextBlackBrush,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                IsDefault = true
            };
            applyBtn.Click += (s, e) =>
            {
                DialogResult = true;
                Close();
            };
            DockPanel.SetDock(applyBtn, Dock.Right);
            footerDock.Children.Add(applyBtn);

            Grid.SetRow(footerDock, 2);
            rootGrid.Children.Add(footerDock);

            Content = rootGrid;
        }

        private UIElement CreateItemCard(OrphanedReferenceItem item)
        {
            var cardBorder = new Border
            {
                Background = KerfBrand.FigmaCardBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

            // Info Column
            var infoStack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };

            var typeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            var badgeColor = item.Kind == OrphanKind.Track ? KerfBrand.Amber : KerfBrand.Mint;
            var badgeBorder = new Border
            {
                Background = new SolidColorBrush(badgeColor) { Opacity = 0.2 },
                BorderBrush = new SolidColorBrush(badgeColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = item.Kind == OrphanKind.Track ? "TRACK" : "LISTENER",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(badgeColor)
                }
            };
            typeRow.Children.Add(badgeBorder);

            var titleText = new TextBlock
            {
                Text = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : item.Id,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = KerfBrand.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            typeRow.Children.Add(titleText);
            infoStack.Children.Add(typeRow);

            var descText = new TextBlock
            {
                Text = item.Description,
                FontSize = 11,
                Foreground = KerfBrand.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap
            };
            infoStack.Children.Add(descText);

            Grid.SetColumn(infoStack, 0);
            cardGrid.Children.Add(infoStack);

            // Action Selection Column
            var actionStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MinWidth = 200 };

            var actionCombo = new ComboBox
            {
                Height = 28,
                Margin = new Thickness(0, 0, 0, 4)
            };
            KerfBrand.ApplyDarkRoundedComboBoxStyle(actionCombo, 6);

            actionCombo.Items.Add(new ComboBoxItem { Content = "Keep Inactive (Safe)", Tag = OrphanResolutionAction.Keep });
            actionCombo.Items.Add(new ComboBoxItem { Content = "Rebind to Layer...", Tag = OrphanResolutionAction.Rebind });
            actionCombo.Items.Add(new ComboBoxItem { Content = "Purge (Delete)", Tag = OrphanResolutionAction.Purge });
            actionCombo.SelectedIndex = 0; // Default Keep

            var layerCombo = new ComboBox
            {
                Height = 28,
                Visibility = Visibility.Collapsed
            };
            KerfBrand.ApplyDarkRoundedComboBoxStyle(layerCombo, 6);

            for (int l = 0; l < _availableLayers.Count; l++)
            {
                var layer = _availableLayers[l];
                if (layer == null) continue;
                layerCombo.Items.Add(new ComboBoxItem
                {
                    Content = string.Format("{0} ({1})", layer.Name, layer.Id),
                    Tag = layer.Id
                });
            }
            if (layerCombo.Items.Count > 0)
            {
                layerCombo.SelectedIndex = 0;
            }

            actionCombo.SelectionChanged += (s, e) =>
            {
                var selItem = actionCombo.SelectedItem as ComboBoxItem;
                if (selItem != null && (OrphanResolutionAction)selItem.Tag == OrphanResolutionAction.Rebind)
                {
                    layerCombo.Visibility = Visibility.Visible;
                }
                else
                {
                    layerCombo.Visibility = Visibility.Collapsed;
                }
            };

            actionStack.Children.Add(actionCombo);
            actionStack.Children.Add(layerCombo);

            _actionCombos[item.Id] = actionCombo;
            _rebindCombos[item.Id] = layerCombo;

            Grid.SetColumn(actionStack, 1);
            cardGrid.Children.Add(actionStack);

            cardBorder.Child = cardGrid;
            return cardBorder;
        }

        public Dictionary<string, OrphanResolution> GetUserResolutions()
        {
            var dict = new Dictionary<string, OrphanResolution>();
            if (_report == null || _report.OrphanedItems == null) return dict;

            for (int i = 0; i < _report.OrphanedItems.Count; i++)
            {
                var item = _report.OrphanedItems[i];
                if (item == null) continue;

                ComboBox actBox;
                if (_actionCombos.TryGetValue(item.Id, out actBox))
                {
                    var selItem = actBox.SelectedItem as ComboBoxItem;
                    OrphanResolutionAction action = (selItem != null) ? (OrphanResolutionAction)selItem.Tag : OrphanResolutionAction.Keep;
                    string targetId = string.Empty;

                    if (action == OrphanResolutionAction.Rebind)
                    {
                        ComboBox rebindBox;
                        if (_rebindCombos.TryGetValue(item.Id, out rebindBox))
                        {
                            var layerItem = rebindBox.SelectedItem as ComboBoxItem;
                            if (layerItem != null && layerItem.Tag != null)
                            {
                                targetId = layerItem.Tag.ToString();
                            }
                        }
                    }

                    dict[item.Id] = new OrphanResolution(action, targetId);
                }
                else
                {
                    dict[item.Id] = new OrphanResolution(OrphanResolutionAction.Keep, string.Empty);
                }
            }

            return dict;
        }

        public static void ApplyResolutions(
            KerfDocument doc,
            MigrationReport report,
            IDictionary<string, OrphanResolution> choices)
        {
            if (doc == null || report == null || choices == null) return;

            var tracksToRemove = new List<Tuple<Clip, Track>>();
            var listenersToRemove = new List<NodeListener>();

            // 1. Process clips and tracks
            if (doc.Animation != null && doc.Animation.Clips != null)
            {
                for (int c = 0; c < doc.Animation.Clips.Count; c++)
                {
                    var clip = doc.Animation.Clips[c];
                    if (clip == null || clip.Tracks == null) continue;

                    for (int t = 0; t < clip.Tracks.Count; t++)
                    {
                        var track = clip.Tracks[t];
                        if (track == null || string.IsNullOrEmpty(track.Id)) continue;

                        OrphanResolution choice;
                        if (choices.TryGetValue(track.Id, out choice) && choice != null)
                        {
                            if (choice.Action == OrphanResolutionAction.Rebind)
                            {
                                if (!string.IsNullOrEmpty(choice.NewTargetLayerId))
                                {
                                    track.TargetNodeId = choice.NewTargetLayerId;
                                    track.NodeId = PropertyBinder.GetNumericNodeId(choice.NewTargetLayerId);
                                    track.IsActive = true;
                                    report.ResolvedTracksCount++;
                                    if (report.UnresolvedTracksCount > 0) report.UnresolvedTracksCount--;
                                }
                            }
                            else if (choice.Action == OrphanResolutionAction.Purge)
                            {
                                tracksToRemove.Add(Tuple.Create(clip, track));
                                if (report.UnresolvedTracksCount > 0) report.UnresolvedTracksCount--;
                            }
                            else
                            {
                                track.IsActive = false;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < tracksToRemove.Count; i++)
            {
                var pair = tracksToRemove[i];
                if (pair.Item1 != null && pair.Item2 != null)
                {
                    pair.Item1.Tracks.Remove(pair.Item2);
                }
            }

            // 2. Process state machine listeners
            if (doc.Animation != null && doc.Animation.StateMachine != null && doc.Animation.StateMachine.Listeners != null)
            {
                var sm = doc.Animation.StateMachine;
                for (int l = 0; l < sm.Listeners.Count; l++)
                {
                    var listener = sm.Listeners[l];
                    if (listener == null || string.IsNullOrEmpty(listener.ListenerId)) continue;

                    OrphanResolution choice;
                    if (choices.TryGetValue(listener.ListenerId, out choice) && choice != null)
                    {
                        if (choice.Action == OrphanResolutionAction.Rebind)
                        {
                            if (!string.IsNullOrEmpty(choice.NewTargetLayerId))
                            {
                                listener.TargetLayerId = choice.NewTargetLayerId;
                                listener.TargetNodeId = PropertyBinder.GetNumericNodeId(choice.NewTargetLayerId);
                                listener.IsActive = true;
                                report.ResolvedListenersCount++;
                                if (report.UnresolvedListenersCount > 0) report.UnresolvedListenersCount--;
                            }
                        }
                        else if (choice.Action == OrphanResolutionAction.Purge)
                        {
                            listenersToRemove.Add(listener);
                            if (report.UnresolvedListenersCount > 0) report.UnresolvedListenersCount--;
                        }
                        else
                        {
                            listener.IsActive = false;
                        }
                    }
                }

                for (int i = 0; i < listenersToRemove.Count; i++)
                {
                    sm.Listeners.Remove(listenersToRemove[i]);
                }
            }

            // Remove purged or rebound items from report.OrphanedItems if desired
            report.OrphanedItems.RemoveAll(item =>
            {
                if (item == null) return true;
                OrphanResolution choice;
                if (choices.TryGetValue(item.Id, out choice) && choice != null)
                {
                    return choice.Action == OrphanResolutionAction.Purge;
                }
                return false;
            });
        }

        public static bool ShowModalReview(Window owner, KerfDocument doc, MigrationReport report)
        {
            if (doc == null || report == null || !report.RequiresUserReview) return false;

            var dlg = new MigrationReviewDialog(doc, report);
            if (owner != null)
            {
                dlg.Owner = owner;
            }

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                var choices = dlg.GetUserResolutions();
                ApplyResolutions(doc, report, choices);
                return true;
            }

            return false;
        }
    }
}
