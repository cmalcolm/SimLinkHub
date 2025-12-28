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

namespace SimLinkHub.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SimConnectService _simService;
    private readonly ArduinoService _arduinoService;
    private IntPtr _mainWindowHandle;

    // Cache for instruments loaded from the Database
    private List<SimInstrument> _activeInstruments = new();
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

        // Wire up the SimConnect Data Bridge
        _simService.OnDataReceived += (data) =>
        {
            // 1. Update UI Elements directly
            FlapsDisplay = $"{data.FlapsTrailingEdgePercent:F1}%";
            //TrimDisplay = data.ElevatorTrimPercent.ToString("F1");

            // 2. Route data to the generic database-driven handler
            HandleSimData(data);
        };
    }

    /// <summary>
    /// Loads instrument configurations from SQLite and caches them.
    /// </summary>
    public async Task LoadInstrumentsFromDb()
    {
        try
        {
            using var db = new AppDbContext();
            _activeInstruments = await db.Instruments.Include(i => i.Arduino).ToListAsync();

            // Initialize the "last sent" tracker for each instrument to prevent spam
            _lastSentValues = _activeInstruments.ToDictionary(i => i.Id, i => -999.0);

            System.Diagnostics.Debug.WriteLine($"DB LOADED: Found {_activeInstruments.Count} instruments.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database Load Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Iterates through all instruments in the DB and sends updates to Arduino if data changed.
    /// </summary>
    private void HandleSimData(SimData data)
    {
        if (!_arduinoService.IsConnected) return;

        foreach (var inst in _activeInstruments)
        {
            // 1. Extract the specific value from the SimData struct using Reflection
            double rawValue = GetValueByVarName(data, inst.SimVarName);

            // 2. Check for significant change (Threshold of 0.5 to prevent jitter)
            if (Math.Abs(rawValue - _lastSentValues[inst.Id]) > 0.5)
            {
                _lastSentValues[inst.Id] = rawValue;

                // 3. Map the Sim value (e.g. -100 to 100) to Byte (0 to 255)
                byte scaledValue = ScaleValue(rawValue, inst);

                // 4. Send via Serial (Leonardo will route it via I2C using the Prefix)
                _arduinoService.SendData(inst.TelemetryPrefix, scaledValue);
            }
        }
    }

    #region Helpers

    private byte ScaleValue(double input, SimInstrument config)
    {
        // Constrain input to defined limits
        double constrainedInput = Math.Max(config.InputMin, Math.Min(config.InputMax, input));

        // Linear Map formula: (val - in_min) * (out_max - out_min) / (in_max - in_min) + out_min
        double result = (constrainedInput - config.InputMin) * (config.OutputMax - config.OutputMin)
                        / (config.InputMax - config.InputMin) + config.OutputMin;

        return config.IsInverted ? (byte)(config.OutputMax - (byte)result) : (byte)result;
    }

    private double GetValueByVarName(SimData data, string varName)
    {
        // Matches DB string (e.g. "ElevatorTrimPercent") to the property in SimData struct
        var prop = typeof(SimData).GetProperty(varName);
        return prop != null ? (double)prop.GetValue(data) : 0;
    }

    #endregion

    #region Connection Management

    public void Initialize(IntPtr handle)
    {
        _mainWindowHandle = handle;

        // Load settings then start watcher
        _ = Task.Run(async () =>
        {
            using (var db = new AppDbContext())
            {
                // Only seed if the database is empty
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
                            SimVarName = "FlapsTrailingEdgePercent",
                            TelemetryPrefix = 'F',
                            InputMin = 0,
                            InputMax = 100,
                            OutputMin = 0,
                            OutputMax = 100
                        },
                        new SimInstrument
                        {
                            Name = "Elevator Trim",
                            ArduinoDeviceId = trimNano.Id,
                            SimVarName = "ElevatorTrimPercent",
                            TelemetryPrefix = 'T',
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
                    await Task.Run(() => _simService.Connect(_mainWindowHandle));
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

    public void TestFlaps(byte targetValue)
    {
        if (_arduinoService.IsConnected)
            _arduinoService.SendData('F', targetValue);
    }
}