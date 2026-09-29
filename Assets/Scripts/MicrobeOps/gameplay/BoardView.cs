using System.Collections.Generic;
using UnityEngine;

// Solo dibuja. Un BoardView por tablero.
// Sorting order sugerido en los prefabs: organismo 0, reveal 1, hit/miss 2.
public class BoardView : MonoBehaviour
{
    public GridManager grid;
    public GameObject hitPrefab;      // rojo
    public GameObject missPrefab;     // blanco/azul
    public GameObject organismPrefab; // casilla de organismo (una por casilla)
    public GameObject revealPrefab;   // marcador de "casilla revelada" (para eventos)

    private readonly HashSet<Organism> revealed = new HashSet<Organism>();
    private readonly List<GameObject> ownObjects = new List<GameObject>();

    public void ShowShot(Vector2Int c, ShotResult r) =>
        Spawn(r == ShotResult.Miss ? missPrefab : hitPrefab, c);

    // Dibuja los organismos PROPIOS (se pueden ocultar con SetOwnVisible)
    public void ShowOwn(Organism o)
    {
        foreach (var c in o.Cells())
        {
            var go = Spawn(organismPrefab, c);
            if (go != null) ownObjects.Add(go);
        }
    }

    public void SetOwnVisible(bool visible)
    {
        foreach (var go in ownObjects)
            if (go != null) go.SetActive(visible);
    }

    // Revela un organismo de forma permanente (cuando lo hunden)
    public void ShowOrganism(Organism o)
    {
        if (!revealed.Add(o)) return;
        foreach (var c in o.Cells()) Spawn(organismPrefab, c);
    }

    public void ShowReveal(Vector2Int c) => Spawn(revealPrefab, c);

    private GameObject Spawn(GameObject prefab, Vector2Int c)
    {
        if (prefab == null) return null;
        return Instantiate(prefab, grid.CellToWorld(c), Quaternion.identity, transform);
    }
}