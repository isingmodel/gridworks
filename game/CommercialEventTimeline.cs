using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Gridworks.Game;

internal enum CommercialTimelineStepState
{
    Completed,
    Current,
    Upcoming,
}

internal sealed record CommercialTimelineStep(
    string Label,
    string Detail,
    CommercialTimelineStepState State);

internal sealed record CommercialEventTimelinePresentation(
    string ChapterLabel,
    string ProgressText,
    float DeadlineRatio,
    IReadOnlyList<CommercialTimelineStep> Steps);

/// <summary>
/// A read-only authored event-flow bar. It reflects campaign boundaries and never
/// advances time itself, preserving the campaign runner as the only authority.
/// </summary>
internal sealed partial class CommercialEventTimeline : Control
{
    private static readonly Color Plate = Color.FromHtml("17191a");
    private static readonly Color PlateInset = Color.FromHtml("0d1214");
    private static readonly Color Brass = Color.FromHtml("8c7047");
    private static readonly Color BrassMuted = Color.FromHtml("5a503f");
    private static readonly Color Completed = Color.FromHtml("4b8589");
    private static readonly Color Current = Color.FromHtml("68cbd2");
    private static readonly Color Upcoming = Color.FromHtml("657174");
    private static readonly Color Text = Color.FromHtml("e8e0d1");
    private static readonly Color Muted = Color.FromHtml("9a9b94");

    private CommercialEventTimelinePresentation? _presentation;
    private float _uiScale = 1f;

    [Export]
    public Texture2D? ChromeFrameTexture { get; set; }

    public int StepCount => _presentation?.Steps.Count ?? 0;

    public string CurrentStepLabel => _presentation?.Steps
        .FirstOrDefault(step => step.State == CommercialTimelineStepState.Current)?.Label ?? string.Empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0f, 82f);
        Resized += QueueRedraw;
    }

    public void SetPresentation(CommercialEventTimelinePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        string steps = string.Join(", ", presentation.Steps.Select(step =>
            $"{step.Label} {StateText(step.State)} · {step.Detail}"));
        AccessibilityName =
            $"사건 흐름. {presentation.ChapterLabel}. {presentation.ProgressText}. {steps}. " +
            "이 표시줄은 시간을 진행하지 않습니다.";
        QueueRedraw();
    }

    public void SetUiScale(float scale)
    {
        _uiScale = Math.Clamp(scale, 1f, 1.25f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float plateWidth = Math.Min(Size.X - 20f, 660f * _uiScale);
        float plateLeft = ((Size.X - plateWidth) * 0.5f) - (78f * _uiScale);
        Rect2 outer = new(new Vector2(plateLeft, 0f), new Vector2(plateWidth, Size.Y));
        DrawChromeFrame(outer, Colors.White, 16f);

        if (_presentation is null || _presentation.Steps.Count == 0)
        {
            return;
        }

        float left = plateLeft + 18f;
        float right = plateLeft + plateWidth - 18f;
        int count = _presentation.Steps.Count;
        float gap = 7f;
        float cellWidth = Math.Max(58f, ((right - left) - (gap * (count - 1))) / count);
        int labelSize = ScaledFont(count >= 6 ? 9 : 11);
        for (int index = 0; index < count; index++)
        {
            CommercialTimelineStep step = _presentation.Steps[index];
            Rect2 cell = new(
                new Vector2(left + (index * (cellWidth + gap)), 9f),
                new Vector2(cellWidth, Size.Y - 18f));
            Color border = step.State switch
            {
                CommercialTimelineStepState.Completed => Completed,
                CommercialTimelineStepState.Current => Current,
                _ => BrassMuted,
            };
            DrawChromeFrame(
                cell,
                step.State switch
                {
                    CommercialTimelineStepState.Completed => new Color(0.54f, 0.70f, 0.70f, 1f),
                    CommercialTimelineStepState.Current => new Color(0.64f, 0.91f, 0.94f, 1f),
                    _ => new Color(0.58f, 0.58f, 0.54f, 0.88f),
                },
                12f);
            DrawRect(cell, new Color(border, step.State == CommercialTimelineStepState.Upcoming ? 0.65f : 0.94f),
                false, step.State == CommercialTimelineStepState.Current ? 2f : 1f);
            Vector2 marker = new(cell.GetCenter().X, cell.Position.Y + (cell.Size.Y * 0.38f));
            DrawStepMarker(marker, step.State);
            DrawString(
                GetThemeDefaultFont(),
                new Vector2(cell.Position.X + 8f, cell.End.Y - 10f),
                step.Label,
                HorizontalAlignment.Center,
                cellWidth - 16f,
                labelSize,
                step.State == CommercialTimelineStepState.Current ? Current :
                    step.State == CommercialTimelineStepState.Completed ? Text : Muted);
        }

        float progressWidth = (plateWidth - 40f) *
            Math.Clamp(_presentation.DeadlineRatio, 0f, 1f);
        DrawLine(
            new Vector2(plateLeft + 20f, Size.Y - 5f),
            new Vector2(plateLeft + 20f + progressWidth, Size.Y - 5f),
            _presentation.DeadlineRatio >= 0.88f ? Current : Completed,
            2f,
            true);
    }

    private void DrawChromeFrame(Rect2 destination, Color modulate, float destinationSlice)
    {
        if (ChromeFrameTexture is null)
        {
            DrawRect(destination, Plate);
            DrawRect(destination, Brass, false, 2f);
            return;
        }
        float sourceWidth = ChromeFrameTexture.GetWidth();
        float sourceHeight = ChromeFrameTexture.GetHeight();
        float sourceSlice = Math.Min(18f, Math.Min(sourceWidth, sourceHeight) * 0.25f);
        float drawSlice = Math.Min(
            destinationSlice,
            Math.Min(destination.Size.X, destination.Size.Y) * 0.34f);
        float[] sourceX = [0f, sourceSlice, sourceWidth - sourceSlice, sourceWidth];
        float[] sourceY = [0f, sourceSlice, sourceHeight - sourceSlice, sourceHeight];
        float[] drawX =
        [
            destination.Position.X,
            destination.Position.X + drawSlice,
            destination.End.X - drawSlice,
            destination.End.X,
        ];
        float[] drawY =
        [
            destination.Position.Y,
            destination.Position.Y + drawSlice,
            destination.End.Y - drawSlice,
            destination.End.Y,
        ];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                DrawTextureRectRegion(
                    ChromeFrameTexture,
                    new Rect2(
                        drawX[column],
                        drawY[row],
                        drawX[column + 1] - drawX[column],
                        drawY[row + 1] - drawY[row]),
                    new Rect2(
                        sourceX[column],
                        sourceY[row],
                        sourceX[column + 1] - sourceX[column],
                        sourceY[row + 1] - sourceY[row]),
                    modulate);
            }
        }
    }

    private void DrawStepMarker(Vector2 center, CommercialTimelineStepState state)
    {
        Color color = state switch
        {
            CommercialTimelineStepState.Completed => Completed,
            CommercialTimelineStepState.Current => Current,
            _ => Upcoming,
        };
        switch (state)
        {
            case CommercialTimelineStepState.Completed:
                DrawRect(new Rect2(center - new Vector2(8f, 8f), new Vector2(16f, 16f)), color);
                DrawLine(center + new Vector2(-4f, 0f), center + new Vector2(-1f, 4f), PlateInset, 2.4f);
                DrawLine(center + new Vector2(-1f, 4f), center + new Vector2(5f, -4f), PlateInset, 2.4f);
                break;
            case CommercialTimelineStepState.Current:
                Vector2[] diamond =
                [
                    center + new Vector2(0f, -11f),
                    center + new Vector2(11f, 0f),
                    center + new Vector2(0f, 11f),
                    center + new Vector2(-11f, 0f),
                ];
                DrawColoredPolygon(diamond, color);
                DrawPolyline(diamond.Append(diamond[0]).ToArray(), Color.FromHtml("fff0bf"), 1.5f, true);
                break;
            default:
                DrawCircle(center, 8f, PlateInset);
                DrawArc(center, 8f, 0f, Mathf.Tau, 24, color, 2.4f, true);
                break;
        }
    }

    private void DrawRivet(Vector2 center)
    {
        DrawCircle(center, 3.2f, BrassMuted);
        DrawCircle(center - new Vector2(0.8f, 0.8f), 1f, Brass);
    }

    private int ScaledFont(int size) => Math.Max(1, (int)MathF.Round(size * _uiScale));

    private static string StateText(CommercialTimelineStepState state) => state switch
    {
        CommercialTimelineStepState.Completed => "완료",
        CommercialTimelineStepState.Current => "현재",
        _ => "다음",
    };
}
