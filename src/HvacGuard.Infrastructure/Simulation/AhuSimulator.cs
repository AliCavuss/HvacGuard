namespace HvacGuard.Infrastructure.Simulation;

public sealed record SimulatedSample(
    double SupplyAirTemperature,
    double ReturnAirTemperature,
    double RelativeHumidity,
    double FanSpeed,
    double FilterPressureDrop);

/// <summary>
/// A small physical model of an Air Handling Unit used to generate realistic demo telemetry
/// (in production the data would come from a PLC / SCADA gateway through the telemetry API).
/// <list type="bullet">
///   <item>Values move towards a target with first-order lag plus sensor noise.</item>
///   <item>The filter slowly clogs; the measured ΔP scales with airflow² (fan affinity law).</item>
///   <item>Random faults are injected: cooling coil failure, supply fan trip and high humidity.</item>
/// </list>
/// </summary>
public sealed class AhuSimulator
{
    private const double NominalFanSpeed = 78;

    private readonly Random _random;
    private readonly double _setpoint;
    private readonly double _faultProbability;

    private double _supply;
    private double _return;
    private double _humidity;
    private double _fan;
    private double _filterClogging; // ΔP in Pa at nominal airflow

    private FaultKind _fault = FaultKind.None;
    private int _faultTicksRemaining;

    public AhuSimulator(int unitId, double setpoint, double faultProbability = 0.0025)
    {
        _random = new Random(unitId * 7919);
        _setpoint = setpoint;
        _faultProbability = faultProbability;

        _supply = setpoint;
        _return = setpoint + 5.5;
        _humidity = 50 + _random.NextDouble() * 6;
        _fan = NominalFanSpeed;
        _filterClogging = 120 + _random.NextDouble() * 140;
    }

    public FaultKind ActiveFault => _fault;

    public SimulatedSample Next()
    {
        MaybeStartFault();

        var supplyTarget = _setpoint;
        var humidityTarget = 52.0;
        var fanTarget = NominalFanSpeed;

        switch (_fault)
        {
            case FaultKind.CoolingFailure:
                supplyTarget = _setpoint + 10;
                humidityTarget = 66;
                break;
            case FaultKind.FanTrip:
                fanTarget = 0;
                supplyTarget = _setpoint + 4;
                break;
            case FaultKind.HighHumidity:
                humidityTarget = 74;
                break;
        }

        _supply = Approach(_supply, supplyTarget, rate: 0.12, noise: 0.12);
        _return = Approach(_return, _supply + 5.5, rate: 0.08, noise: 0.10);
        _humidity = Math.Clamp(Approach(_humidity, humidityTarget, rate: 0.08, noise: 0.5), 15, 98);
        _fan = Math.Clamp(Approach(_fan, fanTarget, rate: 0.3, noise: 0.8), 0, 100);

        // Filter clogs slowly; "replaced" once it is far beyond the critical limit.
        _filterClogging += 0.06 + _random.NextDouble() * 0.04;
        if (_filterClogging > 360)
        {
            _filterClogging = 110;
        }

        var airflowRatio = _fan / NominalFanSpeed;
        var filterDp = _filterClogging * airflowRatio * airflowRatio + (_random.NextDouble() - 0.5) * 3;

        if (_fault != FaultKind.None && --_faultTicksRemaining <= 0)
        {
            _fault = FaultKind.None;
        }

        return new SimulatedSample(
            Math.Round(_supply, 2),
            Math.Round(_return, 2),
            Math.Round(_humidity, 1),
            Math.Round(_fan, 1),
            Math.Round(Math.Max(0, filterDp), 1));
    }

    private void MaybeStartFault()
    {
        if (_fault != FaultKind.None || _random.NextDouble() > _faultProbability)
        {
            return;
        }

        _fault = (FaultKind)_random.Next(1, 4);
        _faultTicksRemaining = _random.Next(40, 110);
    }

    private double Approach(double current, double target, double rate, double noise) =>
        current + (target - current) * rate + (_random.NextDouble() - 0.5) * 2 * noise;
}

public enum FaultKind
{
    None = 0,
    CoolingFailure = 1,
    FanTrip = 2,
    HighHumidity = 3
}
