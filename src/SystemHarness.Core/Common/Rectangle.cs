namespace SystemHarness;

/// <summary>
/// A rectangle defined by position and size, in screen coordinates.
/// </summary>
public readonly record struct Rectangle(int X, int Y, int Width, int Height)
{
    /// <summary>Right edge (X + Width).</summary>
    public int Right => X + Width;

    /// <summary>Bottom edge (Y + Height).</summary>
    public int Bottom => Y + Height;

    /// <summary>Center X coordinate.</summary>
    public int CenterX => X + Width / 2;

    /// <summary>Center Y coordinate.</summary>
    public int CenterY => Y + Height / 2;

    /// <summary>Returns true if this rectangle contains the given point.</summary>
    public bool Contains(int px, int py)
        => px >= X && px < Right && py >= Y && py < Bottom;

    /// <summary>Returns true if this rectangle intersects with another.</summary>
    public bool Intersects(Rectangle other)
        => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}
