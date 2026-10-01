using DeviceBatteryInfo.Core;
using MacroDeck.Localization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.References;
using MacroDeck.Ui.Runtime;

namespace DeviceBatteryInfo.Ui;

internal static class BatteryWidgetView
{
    private const double BreathingRoomPx = 8;
    private static readonly double CornerClearance = 1 - (1 / Math.Sqrt(2));

    // Fractions of the view basis.
    private const double EdgeInset = 0.06;
    private const double TitleHeight = 0.12;
    private const double GridGap = 0.06;

    // Fractions of the ring's diameter. The inset centres the stroke on the bolt, so the bolt fills
    // the charging gap.
    private const double RingThickness = 0.085;
    private const double BoltSize = 0.22;
    private const double GaugeInset = (BoltSize - RingThickness) / 2;
    private const double ChargingGapDegrees = 20;

    // Small enough that "100%" clears the ring's inner circle.
    private const double FaceGlyph = 0.28;
    private const double FaceGlyphAlone = 0.44;
    private const double FacePercent = 0.16;
    private const double NameShare = 0.24;
    private const double TrendShare = 0.19;

    // An aspect range of the rings' box and the aspect the ring sizes assume for it.
    private static readonly (double? Min, double? Max, double Aspect)[] AspectBuckets =
    [
        (null, 0.7, 0.5),
        (1.3, 1.8, 1.5),
        (1.8, 2.6, 2.2),
        (2.6, 3.6, 3.1),
        (3.6, null, 4.4),
    ];

    // A press event claims the gesture and the host then skips the widget's flows, so pass onPress
    // only while the widget has none.
    public static UiElement Build(
        string widgetLocalId,
        UiState<BatteryWidgetModel> state,
        int cornerRadius,
        Action? onPress = null
    )
    {
        UiStack root =
            widgetLocalId == BatteryWidgetTypes.TileId
                ? Tile(state, cornerRadius)
                : Panel(state, cornerRadius);

        return onPress is null
            ? root
            : root with
            {
                Events = [UiEventHandler.On(UiComponentEvents.Press, onPress)],
            };
    }

    private static UiSize SafeArea(int cornerRadius)
    {
        var inset = Math.Max(BreathingRoomPx, CornerClearance * Math.Max(0, cornerRadius));
        return UiSize.Of(UiLength.Capped(inset / UiLength.Cell, inset));
    }

    private static UiStack Panel(UiState<BatteryWidgetModel> state, int cornerRadius)
    {
        var options = state.Value.Options;
        var title = options.Title.Trim();
        var hasTitle = title.Length > 0;

        var children = new List<UiElement>();
        if (hasTitle)
        {
            children.Add(
                new UiTextRun
                {
                    Key = "title",
                    Text = title,
                    Size = UiSize.FromBasis(0.072, 0.9),
                    MinSize = 0.045,
                    Weight = UiComponentTextWeights.SemiBold,
                    Role = UiComponentTextRoles.Muted,
                    MaxLines = 1,
                    Wrap = false,
                }
            );
        }

        children.Add(
            new UiWhen
            {
                Key = "filled",
                Condition = () => state.Value.Rows.Count > 0,
                Content = () =>
                    options.Layout == BatteryWidgetLayout.List
                        ? ListBody(state, hasTitle)
                        : RingBody(state, hasTitle),
            }
        );
        children.Add(
            new UiWhen
            {
                Key = "empty",
                Condition = () => state.Value.Rows.Count == 0,
                Content = () => EmptyText("empty-text", 0.085),
            }
        );

        return new UiStack
        {
            Key = "battery-panel",
            Direction = UiComponentDirections.Vertical,
            Justify = UiComponentJustify.Start,
            Fill = true,
            Padding = SafeArea(cornerRadius),
            Gap = 0.035,
            Children = children,
        };
    }

    private static UiStack EmptyText(string key, double size) =>
        new()
        {
            Key = key + "-wrap",
            Direction = UiComponentDirections.Vertical,
            Justify = UiComponentJustify.Center,
            Fill = true,
            Children =
            [
                new UiTextRun
                {
                    Key = key,
                    Text = Strings.Widgets.Empty(),
                    Size = UiSize.FromBasis(size, 0.4),
                    MinSize = 0.05,
                    Role = UiComponentTextRoles.Muted,
                    Align = UiComponentAlignments.Center,
                    Wrap = true,
                    MaxLines = 3,
                },
            ],
        };

    private static UiResponsive RingBody(UiState<BatteryWidgetModel> state, bool hasTitle) =>
        new()
        {
            Key = "rings",
            Fill = true,
            Default = RingGrid(state, "rings-1", 1, hasTitle),
            Variants = AspectBuckets
                .Select(
                    (bucket, index) =>
                        new UiResponsiveVariant
                        {
                            MinAspect = bucket.Min,
                            MaxAspect = bucket.Max,
                            Content = RingGrid(state, $"rings-{index + 2}", bucket.Aspect, hasTitle),
                        }
                )
                .ToArray(),
        };

    private static UiGrid RingGrid(
        UiState<BatteryWidgetModel> state,
        string key,
        double aspect,
        bool hasTitle
    )
    {
        RingArrangement Arrangement() =>
            Arrange(
                state.Value.Rows.Count,
                aspect,
                hasTitle,
                state.Value.Options.ShowNames,
                state.Value.Options.ShowRingTrend
            );

        return new UiGrid
        {
            Key = key,
            Columns = UiValue.From(() => Arrangement().Columns),
            Rows = UiValue.From(() => Arrangement().Rows),
            Gap = UiSize.FromBasis(GridGap),
            Children =
            [
                new UiRepeat<BatteryWidgetRow>
                {
                    Key = "cells",
                    Items = UiValue.From(() => state.Value.Rows),
                    KeySelector = row => row.Id,
                    Template = (row, rowKey) =>
                        RingCell(row, rowKey, state.Value.Options, () => Arrangement().Diameter),
                },
            ],
        };
    }

    internal readonly record struct RingArrangement(int Columns, int Rows, double Diameter);

    // A tie goes to more columns, so two rings sit side by side.
    internal static RingArrangement Arrange(
        int count,
        double aspect,
        bool hasTitle,
        bool showNames,
        bool showTrend = false
    )
    {
        var shortSide =
            1 - (2 * EdgeInset) - (hasTitle && aspect >= 1 ? TitleHeight : 0);
        var width = Math.Max(aspect, 1) * shortSide;
        var height = Math.Max(1 / aspect, 1) * shortSide;
        var labelFactor = 1 + (showNames ? NameShare : 0) + (showTrend ? TrendShare : 0);

        var devices = Math.Max(count, 1);
        var best = new RingArrangement(1, 1, 0);
        for (var columns = 1; columns <= devices; columns++)
        {
            var rows = (devices + columns - 1) / columns;
            var cellWidth = (width - ((columns - 1) * GridGap)) / columns;
            var cellHeight = (height - ((rows - 1) * GridGap)) / rows;
            var diameter = Math.Min(cellWidth, cellHeight / labelFactor);
            if (diameter >= best.Diameter - 1e-6)
            {
                best = new RingArrangement(columns, rows, diameter);
            }
        }

        return best;
    }

    private static UiStack RingCell(
        BatteryWidgetRow row,
        string key,
        BatteryWidgetOptions options,
        Func<double> diameter
    )
    {
        var children = new List<UiElement>
        {
            Ring(row, options, diameter, showPercent: options.ShowPercent),
        };

        if (options.ShowNames)
        {
            children.Add(
                new UiTextRun
                {
                    Key = "name",
                    Text = row.Name,
                    Size = OfDiameter(diameter, 0.15),
                    Role = UiComponentTextRoles.Secondary,
                    Weight = UiComponentTextWeights.Medium,
                    Align = UiComponentAlignments.Center,
                    MaxLines = 1,
                    Wrap = false,
                }
            );
        }

        // A placeholder keeps every ring in a grid row at the same height while a trend is withheld.
        if (options.ShowRingTrend)
        {
            children.Add(
                new UiTextRun
                {
                    Key = "trend",
                    Text = row.Stale || string.IsNullOrEmpty(row.Trend) ? "–" : row.Trend,
                    Size = OfDiameter(diameter, 0.12),
                    Role = UiComponentTextRoles.Muted,
                    Align = UiComponentAlignments.Center,
                    MaxLines = 1,
                    Wrap = false,
                }
            );
        }

        return new UiStack
        {
            Key = key,
            Direction = UiComponentDirections.Vertical,
            Justify = UiComponentJustify.Center,
            Align = UiComponentAlignments.Center,
            Gap = OfDiameter(diameter, 0.05),
            Children = children,
        };
    }

    private static UiSize OfDiameter(Func<double> diameter, double fraction) =>
        UiSize.From(() => UiLength.OfBasis(fraction * diameter()));

    private static UiModifier Ring(
        BatteryWidgetRow row,
        BatteryWidgetOptions options,
        Func<double> diameter,
        bool showPercent
    )
    {
        var color = row.Color(options.LowThreshold, options.Colors);
        var charging = options.ShowCharging && row.Charging;
        var gap = charging ? ChargingGapDegrees : 0;

        var layers = new List<UiElement>();
        if (options.ShowBar)
        {
            layers.Add(
                new UiStack
                {
                    Key = "gauge-inset",
                    Padding = OfDiameter(diameter, GaugeInset),
                    Children =
                    [
                        new UiGauge
                        {
                            Key = "gauge",
                            Fill = true,
                            Level = row.Level,
                            StartAngle = gap,
                            EndAngle = 360 - gap,
                            LevelColor = color,
                            Thickness = OfDiameter(diameter, RingThickness),
                        },
                    ],
                }
            );
        }

        if (charging)
        {
            layers.Add(
                new UiStack
                {
                    Key = "bolt-lane",
                    Direction = UiComponentDirections.Vertical,
                    Justify = UiComponentJustify.Start,
                    Align = UiComponentAlignments.Center,
                    Children = [Glyph("bolt", DeviceGlyphs.Bolt, color, diameter, BoltSize)],
                }
            );
        }

        var face = new List<UiElement>
        {
            Glyph(
                "glyph",
                DeviceGlyphs.For(row.Kind),
                color,
                diameter,
                showPercent ? FaceGlyph : FaceGlyphAlone
            ),
        };
        if (showPercent)
        {
            face.Add(
                new UiTextRun
                {
                    Key = "pct",
                    Text = row.PercentText(),
                    Size = OfDiameter(diameter, FacePercent),
                    Weight = UiComponentTextWeights.SemiBold,
                    Role = row.Stale ? UiComponentTextRoles.Muted : UiComponentTextRoles.Primary,
                    Align = UiComponentAlignments.Center,
                    MaxLines = 1,
                    Wrap = false,
                }
            );
        }

        layers.Add(
            new UiStack
            {
                Key = "face",
                Direction = UiComponentDirections.Vertical,
                Justify = UiComponentJustify.Center,
                Align = UiComponentAlignments.Center,
                Gap = OfDiameter(diameter, 0.02),
                Children = face,
            }
        );

        return new UiModifier
        {
            Key = "ring",
            Fill = true,
            Frame = new UiFrame { AspectRatio = 1 },
            Child = new UiLayer { Key = "ring-layers", Children = layers },
        };
    }

    private static UiModifier Glyph(
        string key,
        string path,
        string color,
        Func<double> diameter,
        double fraction
    ) =>
        new()
        {
            Key = key,
            MainSize = OfDiameter(diameter, fraction),
            Frame = UiValue.From(() =>
            {
                var edge = UiLength.OfBasis(fraction * diameter());
                return new UiFrame { Width = edge, Height = edge };
            }),
            Child = new UiShape
            {
                Key = key + "-shape",
                Shape = UiComponentShapes.Path,
                Path = path,
                Color = color,
            },
        };

    // Fractions of the view basis.
    private const double ListGlyph = 0.1;
    private const double ListBolt = 0.075;
    private const double ListGap = 0.03;
    private const double NameSize = 0.082;
    private const double CaptionSize = 0.058;
    private const double CaptionGap = 0.02;
    private const double PercentSize = 0.09;
    // The real padding can exceed EdgeInset.
    private const double FitSlack = 0.02;

    // The host takes the first matching variant, so each bucket ends where the next begins.
    private static readonly double[] ListAspects = [1.15, 1.3, 1.5, 1.75, 2.0, 2.4, 2.8, 3.4];

    private static UiResponsive ListBody(UiState<BatteryWidgetModel> state, bool hasTitle) =>
        new()
        {
            Key = "body",
            Fill = true,
            Default = ListRows(state, "rows-1", ListWidth(1, hasTitle)),
            Variants = ListAspects
                .Select(
                    (aspect, index) =>
                        new UiResponsiveVariant
                        {
                            MinAspect = aspect,
                            MaxAspect = index + 1 < ListAspects.Length ? ListAspects[index + 1] : null,
                            Content = ListRows(state, $"rows-{index + 2}", ListWidth(aspect, hasTitle)),
                        }
                )
                .ToArray(),
        };

    // The basis is the widget's short side.
    internal static double ListWidth(double aspect, bool hasTitle)
    {
        var bodyHeight = 1 - (2 * EdgeInset) - (hasTitle ? TitleHeight : 0);
        return Math.Max(1 - (2 * EdgeInset), aspect * bodyHeight);
    }

    // Texts in a row have no shrink priority, so an overlong caption truncates the name too.
    internal static bool CaptionFitsInline(
        BatteryWidgetRow row,
        BatteryWidgetOptions options,
        double width
    )
    {
        if (Caption(row, options) is not { } caption)
        {
            return true;
        }

        var bolt = options.ShowCharging && row.Charging ? ListBolt + ListGap : 0;
        var percent = options.ShowPercent ? PercentWidth(row) + ListGap : 0;
        var available = width - ListGlyph - ListGap - bolt - percent;
        var needed =
            TextWidth.Of(row.Name, NameSize) + CaptionGap + TextWidth.Of(caption, CaptionSize);
        return needed + FitSlack <= available;
    }

    // Hugs the text, so the bolt sits next to the number.
    private static double PercentWidth(BatteryWidgetRow row) =>
        TextWidth.Of(row.PercentText(), PercentSize) + 0.01;

    private static UiStack ListRows(
        UiState<BatteryWidgetModel> state,
        string key,
        double width
    ) =>
        new()
        {
            Key = key,
            Direction = UiComponentDirections.Vertical,
            Justify = state.Value.Options.ListAlign switch
            {
                BatteryListAlignment.Center => UiComponentJustify.Center,
                BatteryListAlignment.Bottom => UiComponentJustify.End,
                _ => UiComponentJustify.Start,
            },
            Gap = 0.045,
            Children =
            [
                new UiRepeat<BatteryWidgetRow>
                {
                    Key = "rows",
                    Items = UiValue.From(() => state.Value.Rows),
                    KeySelector = row => row.Id,
                    Template = (row, rowKey) =>
                        ListRow(row, rowKey, state.Value.Options, width),
                },
            ],
        };

    private static UiStack ListRow(
        BatteryWidgetRow row,
        string key,
        BatteryWidgetOptions options,
        double width
    )
    {
        var color = row.Color(options.LowThreshold, options.Colors);
        var caption = Caption(row, options);
        var inlineCaption = CaptionFitsInline(row, options, width);

        var nameGroup = new List<UiElement> { NameText(row.Name) };
        if (caption is { } captionText)
        {
            nameGroup.Add(
                new UiTextRun
                {
                    Key = "state",
                    Text = captionText,
                    Size = UiSize.FromBasis(CaptionSize, 0.34),
                    MinSize = 0.04,
                    Role = UiComponentTextRoles.Muted,
                    MaxLines = 1,
                    Wrap = false,
                }
            );
        }

        var line = new List<UiElement>
        {
            Glyph("glyph", DeviceGlyphs.For(row.Kind), color, () => 1, ListGlyph),
            new UiStack
            {
                Key = "namegroup",
                Direction = inlineCaption
                    ? UiComponentDirections.Horizontal
                    : UiComponentDirections.Vertical,
                Align = inlineCaption ? UiComponentAlignments.Baseline : UiComponentAlignments.Start,
                Fill = true,
                Gap = inlineCaption ? 0.02 : 0.004,
                Children = nameGroup,
            },
        };
        if (options.ShowCharging && row.Charging)
        {
            line.Add(Glyph("bolt", DeviceGlyphs.Bolt, color, () => 1, ListBolt));
        }

        if (options.ShowPercent)
        {
            line.Add(
                new UiTextRun
                {
                    Key = "pct",
                    Text = row.PercentText(),
                    MainSize = PercentWidth(row),
                    Size = UiSize.FromBasis(PercentSize, 0.62),
                    MinSize = 0.055,
                    Digits = 4,
                    Weight = UiComponentTextWeights.SemiBold,
                    Role = row.Stale ? UiComponentTextRoles.Muted : UiComponentTextRoles.Primary,
                    Align = UiComponentAlignments.End,
                }
            );
        }

        var headline = new UiStack
        {
            Key = "line",
            Direction = UiComponentDirections.Horizontal,
            Align = UiComponentAlignments.Center,
            Gap = ListGap,
            Children = line,
        };

        var children = new List<UiElement> { headline };
        if (options.ShowBar && row.Percent is not null)
        {
            children.Add(
                new UiProgressBar
                {
                    Key = "bar",
                    Fill = true,
                    MainSize = 0.03,
                    Thickness = 0.018,
                    Value = Progress(row.Percent.Value),
                    StartColor = color,
                    EndColor = color,
                }
            );
        }

        return new UiStack
        {
            Key = key,
            Direction = UiComponentDirections.Vertical,
            Gap = 0.018,
            Children = children,
        };
    }

    private static UiTextRun NameText(string name) =>
        new()
        {
            Key = "name",
            Text = name,
            Size = UiSize.FromBasis(NameSize, 0.44),
            MinSize = 0.048,
            Weight = UiComponentTextWeights.Medium,
            Role = UiComponentTextRoles.Secondary,
            MaxLines = 1,
            Wrap = false,
        };

    private static UiStack Tile(UiState<BatteryWidgetModel> state, int cornerRadius) =>
        new()
        {
            Key = "battery-tile",
            Direction = UiComponentDirections.Vertical,
            Align = UiComponentAlignments.Stretch,
            Justify = UiComponentJustify.Center,
            Fill = true,
            Padding = SafeArea(cornerRadius),
            Children =
            [
                new UiRepeat<BatteryWidgetRow>
                {
                    Key = "tile-row",
                    Items = UiValue.From(() => FirstRow(state.Value.Rows)),
                    KeySelector = row => row.Id,
                    Template = (row, key) =>
                        new UiResponsive
                        {
                            Key = key,
                            Fill = true,
                            Default = TileStacked(row, state.Value.Options),
                            Variants =
                            [
                                new UiResponsiveVariant
                                {
                                    MinAspect = 1.6,
                                    Content = TileWide(row, state.Value.Options),
                                },
                            ],
                        },
                },
                new UiWhen
                {
                    Key = "tile-empty",
                    Condition = () => state.Value.Rows.Count == 0,
                    Content = () => EmptyText("tile-empty-text", 0.09),
                },
            ],
        };

    private static IReadOnlyList<BatteryWidgetRow> FirstRow(IReadOnlyList<BatteryWidgetRow> rows) =>
        rows.Count == 0 ? [] : [rows[0]];

    private static UiStack TileStacked(BatteryWidgetRow row, BatteryWidgetOptions options)
    {
        var caption = Caption(row, options);
        var diameter = 1 - (2 * EdgeInset) - 0.13 - (caption is null ? 0 : 0.1);

        var children = new List<UiElement>
        {
            Ring(row, options, () => diameter, options.ShowPercent),
            new UiTextRun
            {
                Key = "name",
                Text = row.Name,
                Size = UiSize.FromBasis(0.1, 0.9),
                MinSize = 0.055,
                Role = UiComponentTextRoles.Secondary,
                Weight = UiComponentTextWeights.Medium,
                MaxLines = 1,
                Wrap = false,
                Align = UiComponentAlignments.Center,
            },
        };

        if (caption is { } captionText)
        {
            children.Add(CaptionText(captionText, UiComponentAlignments.Center));
        }

        return new UiStack
        {
            Key = "stacked",
            Direction = UiComponentDirections.Vertical,
            Align = UiComponentAlignments.Center,
            Justify = UiComponentJustify.Center,
            Gap = 0.02,
            Children = children,
        };
    }

    private static UiStack TileWide(BatteryWidgetRow row, BatteryWidgetOptions options)
    {
        const double diameter = 1 - (2 * EdgeInset);
        var caption = Caption(row, options);

        var details = new List<UiElement>
        {
            new UiTextRun
            {
                Key = "name",
                Text = row.Name,
                Size = UiSize.FromBasis(0.12, 0.3),
                MinSize = 0.055,
                Role = UiComponentTextRoles.Secondary,
                Weight = UiComponentTextWeights.Medium,
                MaxLines = 1,
                Wrap = false,
            },
        };

        if (options.ShowPercent)
        {
            details.Add(
                new UiTextRun
                {
                    Key = "pct",
                    Text = row.PercentText(),
                    Size = UiSize.FromBasis(0.3, 0.6),
                    MinSize = 0.12,
                    Weight = UiComponentTextWeights.Bold,
                    Role = row.Stale ? UiComponentTextRoles.Muted : UiComponentTextRoles.Primary,
                    MaxLines = 1,
                    Wrap = false,
                }
            );
        }

        if (caption is { } captionText)
        {
            details.Add(CaptionText(captionText, UiComponentAlignments.Start) with { Size = UiSize.FromBasis(0.1, 0.9) });
        }

        return new UiStack
        {
            Key = "wide",
            Direction = UiComponentDirections.Horizontal,
            Align = UiComponentAlignments.Center,
            Gap = 0.1,
            Children =
            [
                new UiStack
                {
                    Key = "ring-slot",
                    MainSize = diameter,
                    Direction = UiComponentDirections.Vertical,
                    Justify = UiComponentJustify.Center,
                    Children = [Ring(row, options, () => diameter, showPercent: false)],
                },
                new UiStack
                {
                    Key = "details",
                    Direction = UiComponentDirections.Vertical,
                    Justify = UiComponentJustify.Center,
                    Fill = true,
                    Gap = 0.01,
                    Children = details,
                },
            ],
        };
    }

    private static UiTextRun CaptionText(LocalizedText text, string align) =>
        new()
        {
            Key = "caption",
            Text = text,
            Size = UiSize.FromBasis(0.075, 0.9),
            MinSize = 0.045,
            Role = UiComponentTextRoles.Muted,
            MaxLines = 1,
            Wrap = false,
            Align = align,
        };

    private static UiProgressReference Progress(int percent) =>
        UiProgressReference.Halted(Math.Clamp(percent, 0, 100), DateTimeOffset.UtcNow, 100);

    private static LocalizedText? Caption(BatteryWidgetRow row, BatteryWidgetOptions options)
    {
        if (row.Stale)
        {
            return Strings.Widgets.Caption.NoSignal();
        }

        if (row.Charging)
        {
            if (options.ShowTimeToFull && !string.IsNullOrEmpty(row.TimeToFull))
            {
                return Strings.Widgets.Caption.ChargingEta(row.TimeToFull!);
            }

            if (options.ShowTrend && !string.IsNullOrEmpty(row.Trend))
            {
                return row.Trend!;
            }

            return options.ShowCharging ? Strings.Widgets.Caption.Charging() : null;
        }

        if (row.Status == BatteryStatus.Full)
        {
            return Strings.Widgets.Caption.Full();
        }

        // `? row.Trend : null` would turn a null string into a non-null caption.
        if (options.ShowTrend && !string.IsNullOrEmpty(row.Trend))
        {
            return row.Trend;
        }

        return null;
    }
}
