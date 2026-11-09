using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ExaltHelper.Proxy.DataStructures;

namespace ExaltHelper;

internal sealed class HudGameSprite
{
    public int ObjectType { get; internal set; }
    public string Name { get; internal set; }
    public int AtlasId { get; internal set; }
    public Rectangle Bounds { get; internal set; }
}

// Object portraits borrow the existing atlases. No per-row bitmaps or guessed
// atlas selection: animated actors use their idle frame from the declared atlas.
internal static class HudGameSprites
{
    private sealed class Frame { public Rectangle Bounds; public int Atlas; public int Priority; }
    private sealed class Catalog
    {
        public readonly Dictionary<int, HudGameSprite> Icons = new Dictionary<int, HudGameSprite>();
        public readonly Dictionary<string, int> Classes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Monsters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Portals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
    private static readonly Lazy<Catalog> catalog = new Lazy<Catalog>(Load, true);
    public static void Initialize() { ItemSpriteManager.InitializeAssets(); _ = catalog.Value; }
    public static HudGameSprite Object(int type) => catalog.Value.Icons.TryGetValue(type, out var icon) ? icon : null;
    private static HudGameSprite Find(Dictionary<string, int> names, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string key = name.Trim();
        if (names.TryGetValue(key, out int type)) return Object(type);
        return key.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && names.TryGetValue(key.Substring(4), out type) ? Object(type) : null;
    }
    public static HudGameSprite Class(string name) => Find(catalog.Value.Classes, name);
    public static HudGameSprite Monster(string name) => Find(catalog.Value.Monsters, name);
    public static HudGameSprite Portal(string dungeon) => Find(catalog.Value.Portals, dungeon);
    public static HudGameSprite Player(PlayerDamageEntry player) => player == null ? null
        : Object(player.SkinType) ?? Object(player.ClassType) ?? Class(player.ClassName);
    public static HudGameSprite Player(PlayerParseEntry player) => player == null ? null
        : Object(player.SkinType) ?? Object(player.ClassType) ?? Class(player.ClassName);

    public static void Draw(Graphics graphics, HudGameSprite sprite, Rectangle bounds, string fallback = "combat")
    {
        var atlas = sprite == null ? null : ItemSpriteManager.GetAtlas(sprite.AtlasId);
        if (atlas == null || !new Rectangle(Point.Empty, atlas.Size).Contains(sprite.Bounds))
        { HudDrawing.Icon(graphics, fallback, Rectangle.Inflate(bounds, -5, -5), HudTheme.Muted); return; }
        var state = graphics.Save();
        try
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            double scale = Math.Min(bounds.Width / (double)sprite.Bounds.Width, bounds.Height / (double)sprite.Bounds.Height);
            int width = Math.Max(1, (int)(sprite.Bounds.Width * scale)), height = Math.Max(1, (int)(sprite.Bounds.Height * scale));
            graphics.DrawImage(atlas, new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height), sprite.Bounds, GraphicsUnit.Pixel);
        }
        finally { graphics.Restore(state); }
    }

    public static void Attach(DataGridView grid, string column, Func<object, HudGameSprite> selector, string fallback)
    {
        Initialize();
        grid.Columns[column].MinimumWidth = Math.Max(145, grid.Columns[column].MinimumWidth);
        grid.CellPainting += (_, e) => {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != column) return;
            e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
            var state = e.Graphics.Save();
            try
            {
                e.Graphics.SetClip(e.ClipBounds, CombineMode.Intersect);
                int size = Math.Min(28, e.CellBounds.Height - 6);
                Draw(e.Graphics, selector(grid.Rows[e.RowIndex].Tag), new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + (e.CellBounds.Height - size) / 2, size, size), fallback);
                var text = new Rectangle(e.CellBounds.X + size + 16, e.CellBounds.Y, Math.Max(1, e.CellBounds.Width - size - 22), e.CellBounds.Height);
                HudDrawing.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), e.CellStyle.Font ?? grid.Font, text,
                    (e.State & DataGridViewElementStates.Selected) != 0 ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            finally { e.Graphics.Restore(state); }
            e.Handled = true;
        };
    }

    private static Catalog Load()
    {
        var result = new Catalog();
        string root = Path.GetDirectoryName(typeof(HudGameSprites).Assembly.Location);
        try
        {
            var frames = ReadFrames(File.ReadAllBytes(Path.Combine(root, "assets", "flatbuffer", "spritesheetf")));
            using var stream = typeof(HudGameSprites).Assembly.GetManifestResourceStream("ObjectID.list");
            if (stream == null) return result;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(';');
                if (parts.Length < 8 || !int.TryParse(parts[0], out int type)) continue;
                var texture = parts[5].Split(',');
                if (texture.Length < 2 || !int.TryParse(texture[0], out int index) || !frames.TryGetValue(texture[1] + ":" + index, out var frame)) continue;
                if (frame.Atlas != 2 && frame.Atlas != 4) continue;
                string name = string.IsNullOrWhiteSpace(parts[1]) || parts[1].StartsWith("{s.", StringComparison.Ordinal) ? parts[7] : parts[1];
                result.Icons[type] = new HudGameSprite { ObjectType = type, Name = name, AtlasId = frame.Atlas, Bounds = frame.Bounds };
                Dictionary<string, int> names = parts[2] == "Player" ? result.Classes : parts[2] == "Portal" ? result.Portals
                    : parts[2] == "Character" && parts[6].Split(',').Contains("ENEMY") ? result.Monsters : null;
                if (names == null) continue;
                // Prefer the first actual entity definition to unrelated objects
                // which happen to reuse its localized display name.
                foreach (string alias in new[] { name, parts[7] })
                    if (!string.IsNullOrWhiteSpace(alias) && !names.ContainsKey(alias)) names[alias] = type;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException)
        { System.Diagnostics.Debug.WriteLine("HUD game portraits unavailable: " + ex.Message); }
        return result;
    }

    // FlatBuffer schema: root fields 0=static sheets, 1=animated frames;
    // sprite fields 0=rectangle, 3=index, 7=atlas id (2=characters, 4=objects).
    internal static Dictionary<string, Tuple<Rectangle, int>> InspectFrames(byte[] bytes)
        => ReadFrames(bytes).ToDictionary(x => x.Key, x => Tuple.Create(x.Value.Bounds, x.Value.Atlas), StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, Frame> ReadFrames(byte[] bytes)
    {
        var data = new FlatReader(bytes);
        var frames = new Dictionary<string, Frame>(StringComparer.OrdinalIgnoreCase);
        int root = data.Relative(0);
        foreach (int sheet in data.Vector(data.Field(root, 0)))
        {
            string name = data.String(data.Field(sheet, 0));
            foreach (int sprite in data.Vector(data.Field(sheet, 2)))
            {
                var frame = data.Frame(sprite); int index = data.IntField(sprite, 3);
                if (frame != null) frames[name + ":" + index] = frame;
            }
        }
        foreach (int animation in data.Vector(data.Field(root, 1)))
        {
            string name = data.String(data.Field(animation, 0)); int index = data.IntField(animation, 1);
            int pointer = data.Field(animation, 5); if (pointer == 0) continue;
            var frame = data.Frame(data.Relative(pointer)); if (frame == null) continue;
            int direction = data.IntField(animation, 3), action = data.IntField(animation, 4), set = data.IntField(animation, 2);
            frame.Priority = (action == 0 ? 0 : 100) + (direction == 0 ? 0 : 10 + direction) + (set == 0 ? 0 : 1);
            string key = name + ":" + index;
            if (!frames.TryGetValue(key, out var previous) || frame.Priority < previous.Priority) frames[key] = frame;
        }
        return frames;
    }
    private sealed class FlatReader
    {
        private readonly byte[] data;
        public FlatReader(byte[] value) { data = value ?? throw new InvalidDataException("Missing sprite data."); Require(0, 8); }
        private void Require(int offset, int size) { if (offset < 0 || size < 0 || offset > data.Length - size) throw new InvalidDataException("Sprite table is out of bounds."); }
        private int Int(int offset) { Require(offset, 4); return BitConverter.ToInt32(data, offset); }
        private int Short(int offset) { Require(offset, 2); return BitConverter.ToUInt16(data, offset); }
        public int Relative(int pointer) { int target = checked(pointer + Int(pointer)); Require(target, 4); return target; }
        public int Field(int table, int field)
        {
            int vtable = checked(table - Int(table)), length = Short(vtable);
            if (4 + field * 2 + 2 > length) return 0;
            int offset = Short(vtable + 4 + field * 2); if (offset == 0) return 0;
            int target = checked(table + offset); Require(target, 4); return target;
        }
        public int IntField(int table, int field) { int pointer = Field(table, field); return pointer == 0 ? 0 : Int(pointer); }
        public string String(int pointer)
        {
            if (pointer == 0) return "";
            int start = Relative(pointer), length = Int(start); if (length > 1024) throw new InvalidDataException("Invalid sprite sheet name.");
            Require(start + 4, length); return Encoding.UTF8.GetString(data, start + 4, length);
        }
        public IEnumerable<int> Vector(int pointer)
        {
            if (pointer == 0) yield break;
            int start = Relative(pointer), length = Int(start); Require(start + 4, checked(length * 4));
            for (int i = 0; i < length; i++) yield return Relative(start + 4 + i * 4);
        }
        public Frame Frame(int table)
        {
            int pointer = Field(table, 0); if (pointer == 0) return null; Require(pointer, 16);
            var values = Enumerable.Range(0, 4).Select(i => BitConverter.ToSingle(data, pointer + i * 4)).ToArray();
            if (values.Any(x => float.IsNaN(x) || float.IsInfinity(x) || x < 0 || x > 65536) || values[2] <= 0 || values[3] <= 0) return null;
            int atlas = Field(table, 7); if (atlas != 0) Require(atlas, 8);
            return new Frame { Bounds = new Rectangle((int)values[0], (int)values[1], (int)values[2], (int)values[3]), Atlas = atlas == 0 ? 1 : checked((int)BitConverter.ToInt64(data, atlas)), Priority = 1000 };
        }
    }
}
