using System.Collections.Generic;
using UnityEngine;

// Lógica pura del tablero: no sabe nada de sprites.
public class Board
{
    public readonly int Cols;
    public readonly int Rows;

    private readonly CellState[,] cells;
    private readonly Organism[,] organismAt;
    private readonly List<Organism> organisms = new List<Organism>();

    public IReadOnlyList<Organism> Organisms => organisms;

    public Board(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
        cells = new CellState[cols, rows];
        organismAt = new Organism[cols, rows];
    }

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.x < Cols && c.y >= 0 && c.y < Rows;

    public CellState GetState(Vector2Int c) => cells[c.x, c.y];

    public bool CanPlace(Organism o, Vector2Int origin, int rotation)
    {
        o.Origin = origin;
        o.Rotation = rotation;

        foreach (var c in o.Cells())
        {
            if (!InBounds(c)) return false;
            if (organismAt[c.x, c.y] != null) return false;
        }
        return true;
    }

    public bool Place(Organism o, Vector2Int origin, int rotation)
    {
        if (!CanPlace(o, origin, rotation)) return false;

        foreach (var c in o.Cells())
        {
            cells[c.x, c.y] = CellState.Ship; // (puedes renombrar este estado a Organism)
            organismAt[c.x, c.y] = o;
        }
        organisms.Add(o);
        return true;
    }

    // Disparo a 1 casilla
    public ShotResult Shoot(Vector2Int c)
    {
        if (!InBounds(c)) return ShotResult.Invalid;

        var state = cells[c.x, c.y];
        if (state == CellState.Miss || state == CellState.Hit)
            return ShotResult.Invalid;

        if (state == CellState.Ship)
        {
            cells[c.x, c.y] = CellState.Hit;
            var o = organismAt[c.x, c.y];
            o.Hits++;
            return o.IsSunk ? ShotResult.Sunk : ShotResult.Hit;
        }

        cells[c.x, c.y] = CellState.Miss;
        return ShotResult.Miss;
    }

    // Ataque de área 2x2: 'origin' es la esquina inferior izquierda del bloque.
    // Devuelve el resultado de cada casilla afectada (ignora las ya disparadas o fuera de tablero).
    public List<KeyValuePair<Vector2Int, ShotResult>> ShootArea2x2(Vector2Int origin)
    {
        var results = new List<KeyValuePair<Vector2Int, ShotResult>>();
        for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
            {
                var c = origin + new Vector2Int(dx, dy);
                var r = Shoot(c);
                if (r != ShotResult.Invalid)
                    results.Add(new KeyValuePair<Vector2Int, ShotResult>(c, r));
            }
        return results;
    }

    public bool WasShot(Vector2Int c)
    {
        var st = cells[c.x, c.y];
        return st == CellState.Miss || st == CellState.Hit;
    }

    public bool HasOrganism(Vector2Int c) => organismAt[c.x, c.y] != null;

    public Organism GetOrganismAt(Vector2Int c) => organismAt[c.x, c.y];

    public bool AllSunk()
    {
        foreach (var o in organisms)
            if (!o.IsSunk) return false;
        return organisms.Count > 0;
    }
}