using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[System.Serializable]
public class PlayerSide
{
    public string displayName = "Jugador";
    public GridManager grid;
    public BoardView view;
    [HideInInspector] public Board board;
    [HideInInspector] public bool isAI;
}

public enum TurnPhase { Rolling, Shooting, Switching, GameOver }

// Modos: Local (2 jugadores en el mismo dispositivo) y VsAI (tú = Glóbulos Blancos, IA = Bacterias).
public class GameManager : MonoBehaviour
{
    [Header("Modo (el menú lo puede cambiar con GameSettings.Select)")]
    public GameMode mode = GameMode.Local;

    [Header("Bandos")]
    public PlayerSide whiteCells = new PlayerSide { displayName = "Glóbulos Blancos" };
    public PlayerSide bacteria = new PlayerSide { displayName = "Bacterias" };

    [Header("Referencias")]
    public DiceController dice;
    public Camera cam;

    [Header("UI (opcional)")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI messageText;

    [Header("Reglas")]
    public float turnDuration = 30f;
    public float switchDelay = 1.5f;

    [Header("IA")]
    public float aiThinkDelay = 1.2f; // pausa antes de tirar el dado
    public float aiShotDelay = 0.9f;  // pausa entre disparos

    public TurnPhase Phase { get; private set; }

    private PlayerSide[] sides;
    private int currentIndex;
    private float timeLeft;
    private readonly EnemyAI ai = new EnemyAI();

    private PlayerSide Current => sides[currentIndex];
    private PlayerSide Opponent => sides[1 - currentIndex];

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        if (Gamesettings.HasSelection) mode = Gamesettings.Mode;

        sides = new[] { whiteCells, bacteria };
        whiteCells.isAI = false;
        bacteria.isAI = mode == GameMode.VsAI;

        foreach (var s in sides)
            s.board = new Board(s.grid.cols, s.grid.rows);

        RandomPlacer.PlaceAll(whiteCells.board, CreateWhiteCells());
        RandomPlacer.PlaceAll(bacteria.board, CreateBacteria());

        foreach (var s in sides)
            foreach (var o in s.board.Organisms)
                s.view.ShowOwn(o);

        dice.OnRolled += HandleRolled;
        StartTurn(0);
    }

    private void OnDestroy()
    {
        if (dice != null) dice.OnRolled -= HandleRolled;
    }

    private void Update()
    {
        if (Phase != TurnPhase.Rolling && Phase != TurnPhase.Shooting) return;
        if (Current.isAI) return; // la IA juega por corrutinas y no usa el temporizador

        timeLeft -= Time.deltaTime;
        UpdateTimerUI();
        if (timeLeft <= 0f)
        {
            TimeUp();
            return;
        }

        if (Phase == TurnPhase.Shooting && Input.GetMouseButtonDown(0))
            HandleClick();
    }

    // ---------- Turnos ----------

    private void StartTurn(int index)
    {
        currentIndex = index;
        timeLeft = turnDuration;
        Phase = TurnPhase.Rolling;

        dice.ResetDice();
        dice.SetLocked(Current.isAI);

        ApplyOwnVisibility();
        ShowBoard(mode == GameMode.VsAI ? whiteCells : Current);

        if (turnText != null)
            turnText.text = Current.isAI ? $"Turno: {Current.displayName} (IA)" : $"Turno: {Current.displayName}";

        UpdateTimerUI();

        if (Current.isAI)
        {
            Say($"Turno de {Current.displayName} (IA)...");
            StartCoroutine(AIRollRoutine());
        }
        else
        {
            Say($"Turno de {Current.displayName}: lanza el dado.");
        }
    }

    private void HandleRolled(DiceOutcome outcome)
    {
        if (Phase != TurnPhase.Rolling) return;

        if (!dice.CanShoot) // p. ej. un evento aún sin implementar
        {
            Say($"Resultado: {outcome}. No hay disparos, se pasa el turno.");
            StartCoroutine(SwitchTurnRoutine());
            return;
        }

        Phase = TurnPhase.Shooting;
        ApplyOwnVisibility();

        if (Current.isAI)
        {
            Say($"{Current.displayName} sacó {outcome}.");
            StartCoroutine(AIShootRoutine());
            return;
        }

        // Al tirar, se pasa al tablero rival para disparar
        ShowBoard(Opponent);
        Say(dice.IsAreaShot
            ? "¡Misil! Elige la esquina inferior izquierda del área 2x2."
            : $"Elige {dice.RemainingShots} casilla(s) del tablero de {Opponent.displayName}.");
    }

    private void TimeUp()
    {
        Say(dice.HasRolled
            ? "¡Se acabó el tiempo! Pierdes los disparos que te quedaban."
            : "¡Se acabó el tiempo! Pierdes el turno.");
        StartCoroutine(SwitchTurnRoutine());
    }

    private IEnumerator SwitchTurnRoutine()
    {
        Phase = TurnPhase.Switching;
        yield return new WaitForSeconds(switchDelay);
        StartTurn(1 - currentIndex);
    }

    // ---------- Disparos del jugador ----------

    private void HandleClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var target = Opponent;
        var cell = target.grid.WorldToCell(cam.ScreenToWorldPoint(Input.mousePosition));
        if (!target.board.InBounds(cell)) return;

        if (!Fire(target, cell)) return; // casilla ya disparada: no gasta el tiro

        dice.RegisterShot();

        if (target.board.AllSunk()) { EndGame(); return; }
        if (!dice.CanShoot) StartCoroutine(SwitchTurnRoutine());
    }

    // Dispara (1 casilla o área 2x2 según el dado). Devuelve false si no se pudo disparar.
    // Notifica a la IA de cada resultado (solo le sirve cuando ella es quien dispara).
    private bool Fire(PlayerSide target, Vector2Int cell)
    {
        if (dice.IsAreaShot)
        {
            var results = target.board.ShootArea2x2(cell);
            foreach (var kv in results)
            {
                ApplyShot(target, kv.Key, kv.Value);
                if (Current.isAI) ai.OnResult(kv.Key, kv.Value);
            }
            return results.Count > 0;
        }

        var r = target.board.Shoot(cell);
        if (r == ShotResult.Invalid) return false;
        ApplyShot(target, cell, r);
        if (Current.isAI) ai.OnResult(cell, r);
        return true;
    }

    // ---------- Turno de la IA ----------

    private IEnumerator AIRollRoutine()
    {
        yield return new WaitForSeconds(aiThinkDelay);
        if (Phase == TurnPhase.Rolling && Current.isAI)
            dice.RollDice(); // dispara OnRolled -> HandleRolled -> AIShootRoutine
    }

    private IEnumerator AIShootRoutine()
    {
        var target = Opponent;

        while (dice.CanShoot)
        {
            yield return new WaitForSeconds(aiShotDelay);

            var cell = ai.PickTarget(target.board);
            Fire(target, cell);
            dice.RegisterShot(); // siempre gasta el tiro: evita bucles infinitos

            if (target.board.AllSunk()) { EndGame(); yield break; }
        }

        StartCoroutine(SwitchTurnRoutine());
    }

    // ---------- Utilidades ----------

    private void ApplyShot(PlayerSide target, Vector2Int c, ShotResult r)
    {
        target.view.ShowShot(c, r);
        if (r == ShotResult.Sunk)
        {
            var o = target.board.GetOrganismAt(c);
            target.view.ShowOrganism(o);
            Say($"¡{o.Name} eliminado!");
        }
    }

    // Quién ve sus propios organismos:
    //  - VsAI: el humano siempre; los de la IA nunca.
    //  - Local: solo el jugador en turno, y solo mientras tira el dado.
    private void ApplyOwnVisibility()
    {
        foreach (var s in sides)
        {
            bool visible = mode == GameMode.VsAI
                ? !s.isAI
                : (s == Current && Phase == TurnPhase.Rolling);
            s.view.SetOwnVisible(visible);
        }
    }

    private void EndGame()
    {
        Phase = TurnPhase.GameOver;
        Say($"¡{Current.displayName} gana la partida!");
    }

    private void ShowBoard(PlayerSide side)
    {
        var p = side.grid.transform.position;
        cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        if (Current.isAI) { timerText.text = ""; return; }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        timerText.text = seconds.ToString();
        timerText.color = seconds <= 10 ? Color.red : Color.white;
    }

    private void Say(string msg)
    {
        Debug.Log(msg);
        if (messageText != null) messageText.text = msg;
    }

    private List<Organism> CreateWhiteCells() => new List<Organism>
        { Organism.Neutrofilo(), Organism.Macrofago(), Organism.Linfocito(), Organism.Eosinofilo() };

    private List<Organism> CreateBacteria() => new List<Organism>
        { Organism.EColi(), Organism.Staphylococcus(), Organism.Salmonella(), Organism.Streptococcus() };
}