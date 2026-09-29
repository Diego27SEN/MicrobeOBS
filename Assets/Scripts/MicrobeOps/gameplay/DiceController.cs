using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DiceOutcome { None, Single, Double, Missile, Event }

// Solo el dado. Ponlo en un GameObject de la escena y asigna los campos en el Inspector.
public class DiceController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI diceResultValue;
    [SerializeField] private Button rollButton;
    [SerializeField] private bool eventsEnabled = false; // apagado: la cara 5 cuenta como doble

    public int RemainingShots { get; private set; }
    public bool HasRolled { get; private set; }
    public DiceOutcome CurrentOutcome { get; private set; } = DiceOutcome.None;

    // true cuando el disparo actual revienta un bloque 2x2
    public bool IsAreaShot => CurrentOutcome == DiceOutcome.Missile;
    public bool CanShoot => HasRolled && RemainingShots > 0;

    // Se dispara justo después de tirar el dado
    public event System.Action<DiceOutcome> OnRolled;

    private void Start()
    {
        if (rollButton != null) rollButton.onClick.AddListener(OnRollButtonPressed);
        UpdateUI();
    }

    private bool locked; // true durante el turno de la IA: el jugador no puede tirar

    public void SetLocked(bool value)
    {
        locked = value;
        UpdateUI();
    }

    private void OnRollButtonPressed()
    {
        if (locked) return;
        RollDice();
    }

    public static DiceOutcome OutcomeFromFace(int face, bool eventsEnabled)
    {
        if (eventsEnabled)
        {
            if (face <= 2) return DiceOutcome.Single;  // 1-2
            if (face <= 4) return DiceOutcome.Double;  // 3-4
            if (face == 5) return DiceOutcome.Event;   // 5
            return DiceOutcome.Missile;                // 6
        }

        if (face <= 3) return DiceOutcome.Single;      // 1-3
        if (face <= 5) return DiceOutcome.Double;      // 4-5
        return DiceOutcome.Missile;                    // 6
    }

    public void RollDice()
    {
        if (HasRolled) return; // una tirada por turno

        HasRolled = true;
        int face = Random.Range(1, 7);
        CurrentOutcome = OutcomeFromFace(face, eventsEnabled);
        Debug.Log($"Dado: {face} -> {CurrentOutcome}");

        switch (CurrentOutcome)
        {
            case DiceOutcome.Single: RemainingShots = 1; break;
            case DiceOutcome.Double: RemainingShots = 2; break;
            case DiceOutcome.Missile: RemainingShots = 1; break; // 1 disparo, área 2x2
            default: RemainingShots = 0; break;
        }

        UpdateUI();
        OnRolled?.Invoke(CurrentOutcome);
    }

    // Llámalo cada vez que el jugador dispara de verdad
    public void RegisterShot()
    {
        if (RemainingShots <= 0) return;
        RemainingShots--;
        UpdateUI();
    }

    // Llámalo al terminar el turno para poder volver a tirar
    public void ResetDice()
    {
        HasRolled = false;
        RemainingShots = 0;
        CurrentOutcome = DiceOutcome.None;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (rollButton != null) rollButton.interactable = !HasRolled && !locked;

        if (diceResultValue == null) return;

        if (!HasRolled || RemainingShots <= 0)
        {
            diceResultValue.text = "";
            return;
        }

        diceResultValue.text = CurrentOutcome == DiceOutcome.Missile
            ? "2x2"
            : RemainingShots.ToString();
    }
}