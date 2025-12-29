using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using SimLinkHub.Data;
using SimLinkHub.Models;
using SimLinkHub.Services;
using System.Collections.ObjectModel;

namespace SimLinkHub.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SimConnectService _simService;
    private readonly ArduinoService _arduinoService;
    private IntPtr _mainWindowHandle;

    //private List<SimInstrument> _activeInstruments = new();
    [ObservableProperty]
    private ObservableCollection<SimInstrument> _activeInstruments = new();
    private Dictionary<int, double> _lastSentValues = new();

    #region UI Properties
    [ObservableProperty] private string _statusMessage = "Ready to Connect";
    [ObservableProperty] private bool _isSimConnected;
    [ObservableProperty] private bool _isArduinoConnected;
    [ObservableProperty] private string _flapsDisplay = "0.0%";
    [ObservableProperty] private string _trimDisplay = "0.0";
    #endregion

    public MainViewModel()
    {
        _simService = new SimConnectService();
        _arduinoService = new ArduinoService();

        // UPDATED: Receives a double array instead of a SimData struct
        _simService.OnDataReceived += (double[] values) =>
        {
            HandleSimData(values);
        };
    }

    //public async Task LoadInstrumentsFromDb()
    //{
    //    try
    //    {
    //        using var db = new AppDbContext();
    //        //_activeInstruments = await db.Instruments.Include(i => i.Arduino).ToListAsync();
    //        _activeInstruments = await db.Instruments
    //            .Include(i => i.Arduino)
    //            .Where(i => i.TelemetryPrefix != null && i.TelemetryPrefix != ' ') // Only get outputs
    //            .ToListAsync();

    //        // --- LOGGING START ---
    //        System.Diagnostics.Debug.WriteLine("===== DB CONTENT START =====");
    //        foreach (var inst in _activeInstruments)
    //        {
    //            System.Diagnostics.Debug.WriteLine($"Instrument: {inst.Name} | Max: {inst.OutputMax} | Index: {inst.DataIndex}");
    //        }
    //        System.Diagnostics.Debug.WriteLine("===== DB CONTENT END =====");
    //        // --- LOGGING END ---

    //        _lastSentValues = _activeInstruments.ToDictionary(i => i.Id, i => -999.0);

    //        System.Diagnostics.Debug.WriteLine($"DB LOADED: Found {_activeInstruments.Count} instruments.");
    //    }
    //    catch (Exception ex)
    //    {
    //        System.Diagnostics.Debug.WriteLine($"Database Load Error: {ex.Message}");
    //    }
    //}
    public async Task LoadInstrumentsFromDb()
    {
        try
        {
            using var db = new AppDbContext();
            var instruments = await db.Instruments
                .Include(i => i.Arduino)
                .Where(i => i.TelemetryPrefix != null && i.TelemetryPrefix != ' ')
                .ToListAsync();

            // Run this on the UI thread to ensure the collection update is thread-safe
            Application.Current.Dispatcher.Invoke(() =>
            {
                ActiveInstruments.Clear();
                foreach (var inst in instruments)
                {
                    ActiveInstruments.Add(inst);
                }
            });

            _lastSentValues = ActiveInstruments.ToDictionary(i => i.Id, i => -999.0);
            System.Diagnostics.Debug.WriteLine($"DB LOADED: Found {ActiveInstruments.Count} instruments.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database Load Error: {ex.Message}");
        }
    }

    // UPDATED: Uses the DataIndex to pull values from the array
    private void HandleSimData(double[] values)
    {
        if (!_arduinoService.IsConnected) return;

        foreach (var inst in _activeInstruments)
        {
            // Ensure the instrument's index exists in the data we received
            if (inst.DataIndex >= 0 && inst.DataIndex < values.Length)
            {
                double rawValue = values[inst.DataIndex];

                if (Math.Abs(rawValue - _lastSentValues[inst.Id]) > 0.5)
                {
                    _lastSentValues[inst.Id] = rawValue;

                    // Update UI (Direct mapping based on prefix)
                    Application.Current.Dispatcher.Invoke(() => {
                        if (inst.TelemetryPrefix == 'F') FlapsDisplay = $"{rawValue:F1}%";
                        if (inst.TelemetryPrefix == 'T') TrimDisplay = rawValue.ToString("F1");
                    });

                    byte scaledValue = ScaleValue(rawValue, inst);
                    _arduinoService.SendData(inst.TelemetryPrefix, scaledValue);
                }
            }
        }
    }

    #region Helpers

    private byte ScaleValue(double input, SimInstrument config)
    {
        // 1. Constrain input
        double constrainedInput = Math.Max(config.InputMin, Math.Min(config.InputMax, input));

        // 2. Force double-precision math by using (double) casts or the 'input' variable
        double numerator = (constrainedInput - config.InputMin) * (config.OutputMax - config.OutputMin);
        double denominator = (config.InputMax - config.InputMin);

        if (denominator == 0) return (byte)config.OutputMin;

        double result = (numerator / denominator) + config.OutputMin;

        // 3. Handle Inversion and return as a byte (0-255)
        if (config.IsInverted)
        {
            return (byte)Math.Clamp(config.OutputMax - result + config.OutputMin, 0, 255);
        }

        return (byte)Math.Clamp(result, 0, 255);
    }

    // REMOVED: GetValueByVarName (Reflection is no longer needed!)

    #endregion

    #region Connection Management

    public void Initialize(IntPtr handle)
    {
        _mainWindowHandle = handle;

        _ = Task.Run(async () =>
        {
            using (var db = new AppDbContext())
            {
                if (!db.Instruments.Any())
                {
                    var flapNano = new ArduinoDevice { FriendlyName = "Flap Nano", I2CAddress = 0x09 };
                    var trimNano = new ArduinoDevice { FriendlyName = "Trim Nano", I2CAddress = 0x0A };
                    db.Arduinos.AddRange(flapNano, trimNano);
                    await db.SaveChangesAsync();

                    db.Instruments.AddRange(
                        new SimInstrument
                        {
                            Name = "Flaps",
                            ArduinoDeviceId = flapNano.Id,
                            SimVarName = "TRAILING EDGE FLAPS LEFT PERCENT",
                            Units = "Percent",
                            TelemetryPrefix = 'F',
                            DataIndex = 0,
                            InputMin = 0,
                            InputMax = 100,
                            OutputMin = 0,
                            OutputMax = 100
                        },
                        new SimInstrument
                        {
                            Name = "Elevator Trim",
                            ArduinoDeviceId = trimNano.Id,
                            SimVarName = "ELEVATOR TRIM PCT",
                            Units = "Percent",
                            TelemetryPrefix = 'T',
                            DataIndex = 1,
                            InputMin = -100,
                            InputMax = 100,
                            OutputMin = 0,
                            OutputMax = 255
                        }
                    );
                    await db.SaveChangesAsync();
                }
            }
            await LoadInstrumentsFromDb();
            await StartConnectionWatcher();
        });
    }

    public void ProcessSimConnectMessage() => _simService.ReceiveMessage();

    private async Task StartConnectionWatcher()
    {
        while (true)
        {
            try
            {
                if (!_simService.IsConnected && _mainWindowHandle != IntPtr.Zero)
                {
                    // UPDATED: Pass the instruments to the Connect method
                    //await Task.Run(() => _simService.Connect(_mainWindowHandle, _activeInstruments));
                    await Task.Run(() => _simService.Connect(_mainWindowHandle, ActiveInstruments.ToList()));
                }

                if (!_arduinoService.IsConnected)
                {
                    await _arduinoService.AttemptConnectionAsync();
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsSimConnected = _simService.IsConnected;
                    IsArduinoConnected = _arduinoService.IsConnected;
                    UpdateStatusMessage();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Watcher Error: {ex.Message}");
            }

            await Task.Delay(5000);
        }
    }

    private void UpdateStatusMessage()
    {
        if (IsSimConnected && IsArduinoConnected) StatusMessage = "All Systems Active";
        else if (!IsSimConnected && !IsArduinoConnected) StatusMessage = "Searching for Sim & Arduino...";
        else if (!IsSimConnected) StatusMessage = "Sim Disconnected...";
        else StatusMessage = "Arduino Disconnected...";
    }
    #endregion

    public void TestFlaps(double testPercent)
    {
        // 1. Find the Flaps configuration in our loaded list
        var flapConfig = _activeInstruments.FirstOrDefault(i => i.TelemetryPrefix == 'F');

        if (flapConfig != null && _arduinoService.IsConnected)
        {
            // 2. Use the exact same math the Simulator uses
            byte scaledValue = ScaleValue(testPercent, flapConfig);

            // 3. Send the translated byte to the Leonardo
            _arduinoService.SendData('F', scaledValue);

            System.Diagnostics.Debug.WriteLine($"TEST: Input {testPercent}% translated to Byte {scaledValue} using OutputMax {flapConfig.OutputMax}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("TEST: Could not find Flap config or Arduino is disconnected.");
        }
    }
    public void ManualTest(SimInstrument instrument, double percent)
    {
        if (_arduinoService.IsConnected && instrument != null)
        {
            // Calculate the actual value within the Sim's range
            // Example Trim: -100 + (0.5 * (100 - (-100))) = 0
            // Example Flaps: 0 + (0.5 * (100 - 0)) = 50
            double simValue = instrument.InputMin + (percent / 100.0 * (instrument.InputMax - instrument.InputMin));

            byte scaledValue = ScaleValue(simValue, instrument);
            _arduinoService.SendData(instrument.TelemetryPrefix, scaledValue);

            System.Diagnostics.Debug.WriteLine($"Manual Test [{instrument.Name}]: UI {percent}% -> SimValue {simValue} -> Byte {scaledValue}");
        }
    }
}