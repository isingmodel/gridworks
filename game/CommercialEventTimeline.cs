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
    private static readonly Color Completed = Color.FromHtml("5fc5c9");
    private static readonly Color Current = Color.FromHtml("efb75d");
    private static readonly Color Upcoming = Color.FromHtml("657174");
    private static readonly Color Text = Color.FromHtml("e8e0d1");
    private static readonly Color Muted = Color.FromHtml("9a9b94");

    private CommercialEventTimelinePresentation? _presentation;
    private float _uiScale = 1f;

    public int StepCount => _presentation?.Steps.Count ?? 0;

    public string CurrentStepLabel => _presentation?.Steps
        .FirstOrDefault(step => step.State == CommercialTimelineStepState.Current)?.Label ?? string.Empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0f, 92f);
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
        Rect2 outer = new(Vector2.Zero, Size);
        DrawRect(outer, Plate);
        DrawRect(new Rect2(3f, 3f, Size.X - 6f, Size.Y - 6f), PlateInset);
        DrawRect(new Rect2(1f, 1f, Size.X - 2f, Size.Y - 2f), Brass, false, 2f);
        DrawRivet(new Vector2(10f, 10f));
        DrawRivet(new Vector2(Size.X - 10f, 10f));
        DrawRivet(new Vector2(10f, Size.Y - 10f));
        DrawRivet(new Vector2(Size.X - 10f, Size.Y - 10f));

        if (_presentation is null || _presentation.Steps.Count == 0)
        {
            return;
        }

        int headingSize = ScaledFont(12);
        DrawString(
            GetThemeDefaultFont(),
            new Vector2(20f, 21f),
            $"사건 흐름  //  {_presentation.ChapterLabel}",
            HorizontalAlignment.Left,
            Size.X * 0.55f,
            headingSize,
            Current);
        DrawString(
            GetThemeDefaultFont(),
            new Vector2(Size.X * 0.64f, 21f),
            _presentation.ProgressText,
            HorizontalAlignment.Right,
            Size.X * 0.32f,
            headingSize,
            Text);

        Rect2 deadlineTrack = new(20f, 29f, Size.X - 40f, 4f);
        DrawRect(deadlineTrack, BrassMuted);
        DrawRect(new Rect2(
            deadlineTrack.Position,
            new Vector2(deadlineTrack.Size.X * Math.Clamp(_presentation.DeadlineRatio, 0f, 1f), 4f)),
            _presentation.DeadlineRatio >= 0.88f ? Current : Completed);

        float left = 40f;
        float right = Size.X - 40f;
        float stepY = 53f;
        int count = _presentation.Steps.Count;
        float spacing = count <= 1 ? 0f : (right - left) / (count - 1);
        for (int index = 0; index < count - 1; index++)
        {
            CommercialTimelineStep step = _presentation.Steps[index];
            CommercialTimelineStep next = _presentation.Steps[index + 1];
            Color connector = step.State == CommercialTimelineStepState.Completed &&
                next.State != CommercialTimelineStepState.Upcoming
                ? Completed
                : BrassMuted;
            DrawLine(
                new Vector2(left + (spacing * index) + 8f, stepY),
                new Vector2(left + (spacing * (index + 1)) - 8f, stepY),
                connector,
                step.State == CommercialTimelineStepState.Completed ? 3f : 2f,
                true);
        }

        int labelSize = ScaledFont(count >= 7 ? 10 : 11);
        for (int index = 0; index < count; index++)
        {
            CommercialTimelineStep step = _presentation.Steps[index];
            Vector2 center = new(left + (spacing * index), stepY);
            DrawStepMarker(center, step.State);
            float labelWidth = count <= 1 ? right - left : Math.Max(84f, spacing - 12f);
            DrawString(
                GetThemeDefaultFont(),
                new Vector2(center.X - (labelWidth / 2f), 77f),
                step.Label,
                HorizontalAlignment.Center,
                labelWidth,
                labelSize,
                step.State == CommercialTimelineStepState.Current ? Current :
                    step.State == CommercialTimelineStepState.Completed ? Text : Muted);
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
                DrawRect(new Rect2(center - new Vector2(6f, 6f), new Vector2(12f, 12f)), color);
                DrawLine(center + new Vector2(-3f, 0f), center + new Vector2(-1f, 3f), PlateInset, 2f);
                DrawLine(center + new Vector2(-1f, 3f), center + new Vector2(4f, -3f), PlateInset, 2f);
                break;
            case CommercialTimelineStepState.Current:
                Vector2[] diamond =
                [
                    center + new Vector2(0f, -8f),
                    center + new Vector2(8f, 0f),
                    center + new Vector2(0f, 8f),
                    center + new Vector2(-8f, 0f),
                ];
                DrawColoredPolygon(diamond, color);
                DrawPolyline(diamond.Append(diamond[0]).ToArray(), Color.FromHtml("fff0bf"), 1.5f, true);
                break;
            default:
                DrawCircle(center, 6f, PlateInset);
                DrawArc(center, 6f, 0f, Mathf.Tau, 20, color, 2f, true);
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
