using Godot;

namespace Gridworks.Game;

/// <summary>
/// Draws four fixed, code-native speaker portraits without shipping a raster asset.
/// Face details remain distinct when the accompanying role color cannot be perceived.
/// </summary>
internal sealed partial class CommercialPortrait : Control
{
    private string _personId = "yoon";
    private Color _accent = Color.FromHtml("d39acb");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    public void SetPerson(string personId, string accessibleName, Color accent)
    {
        _personId = personId;
        _accent = accent;
        AccessibilityName = $"{accessibleName} 고정 초상";
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color outline = Color.FromHtml("17212b");
        Color skin = Color.FromHtml("d9a77d");
        Color hair = Color.FromHtml("302b30");
        DrawRect(new Rect2(1f, 1f, 46f, 46f), _accent.Darkened(0.62f), true);
        DrawRect(new Rect2(2f, 2f, 44f, 44f), _accent, false, 2f);
        DrawCircle(new Vector2(24f, 19f), 11.5f, hair);
        DrawCircle(new Vector2(24f, 21f), 9.5f, skin);
        DrawRect(new Rect2(10f, 33f, 28f, 14f), _accent.Darkened(0.35f), true);
        DrawCircle(new Vector2(20.5f, 20f), 1.1f, outline);
        DrawCircle(new Vector2(27.5f, 20f), 1.1f, outline);
        DrawArc(new Vector2(24f, 23f), 3f, 0.35f, 2.79f, 12, outline, 1.2f);

        switch (_personId)
        {
            case "park":
                DrawRect(new Rect2(16.5f, 17f, 7f, 5.5f), outline, false, 1.2f);
                DrawRect(new Rect2(24.5f, 17f, 7f, 5.5f), outline, false, 1.2f);
                DrawLine(new Vector2(23.5f, 19f), new Vector2(24.5f, 19f), outline, 1.2f);
                break;
            case "kang":
                DrawLine(new Vector2(20f, 26f), new Vector2(24f, 24.5f), hair, 2f);
                DrawLine(new Vector2(24f, 24.5f), new Vector2(28f, 26f), hair, 2f);
                DrawLine(new Vector2(16f, 11f), new Vector2(31f, 11f), outline, 2f);
                break;
            case "lee":
                DrawArc(new Vector2(24f, 13f), 10f, 3.2f, 6.2f, 18, _accent.Darkened(0.5f), 4f);
                DrawLine(new Vector2(24f, 7f), new Vector2(33f, 11f), _accent.Darkened(0.5f), 3f);
                break;
            default:
                DrawCircle(new Vector2(35f, 19f), 4.5f, hair);
                DrawLine(new Vector2(14f, 12f), new Vector2(13f, 29f), _accent.Lightened(0.25f), 2f);
                break;
        }
    }
}
