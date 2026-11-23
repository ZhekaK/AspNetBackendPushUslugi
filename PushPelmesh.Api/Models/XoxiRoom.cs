namespace PushPelmesh.Api.Models;

public class XoxiRoom
{
    public const int Columns = 10;
    public const int Rows = 5;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? PasswordHash { get; set; }
    public int OwnerUserId { get; set; }
    public User Owner { get; set; } = null!;
    public int? OpponentUserId { get; set; }
    public User? Opponent { get; set; }
    public bool Started { get; set; }
    public bool OwnerPlaysNoughts { get; set; }
    public int[] Cells { get; set; } = new int[Columns * Rows];
    public int Moves { get; set; }
    public int? LastCell { get; set; }
    public int? Winner { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int OwnerFigure => OwnerPlaysNoughts ? 2 : 1;
    public int Turn => !Started || Winner.HasValue ? 0 : Moves % 2 + 1;

    public void Touch()
    {
        var nextVersion = UpdatedAt.AddTicks(10);
        var now = DateTime.UtcNow;
        UpdatedAt = now > nextVersion ? now : nextVersion;
    }

    public bool Drop(int column)
    {
        if (column is < 0 or >= Columns || Turn == 0)
            return false;

        var row = 0;
        while (row < Rows && Cells[column * Rows + row] != 0)
            row++;
        if (row == Rows)
            return false;

        var figure = Turn;
        Cells[column * Rows + row] = figure;
        LastCell = column * Rows + row;
        Moves++;
        if (HasLine(column, row, figure))
            Winner = figure;
        else if (Moves == Columns * Rows)
            Winner = 0;
        return true;
    }

    private bool HasLine(int column, int row, int figure)
    {
        return HasLine(1, 0) || HasLine(0, 1) || HasLine(1, 1) || HasLine(1, -1);

        bool HasLine(int dx, int dy) => 1 + Count(dx, dy) + Count(-dx, -dy) >= 4;

        int Count(int dx, int dy)
        {
            var count = 0;
            for (int x = column + dx, y = row + dy;
                x is >= 0 and < Columns && y is >= 0 and < Rows && Cells[x * Rows + y] == figure;
                x += dx, y += dy)
            {
                count++;
            }
            return count;
        }
    }
}
