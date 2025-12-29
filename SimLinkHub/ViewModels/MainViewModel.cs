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
    private Dictionary<string, double> _simDataValues = new Dictionary<string, double>();

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


    public async Task LoadInstrumentsFromDb()
    {
        try
        {
            using var db = new AppDbContext();
            var instruments = await db.Instruments
                .Include(i => i.Arduino)
                .Where(i => i.TelemetryPrefix != "" && i.TelemetryPrefix != " ")
                .OrderBy(i => i.DataIndex)
                .ToListAsync();

            System.Diagnostics.Debug.WriteLine("=== DB LOAD ORDER ===");
            for (int i = 0; i < instruments.Count; i++)
            {
                var inst = instruments[i];
                System.Diagnostics.Debug.WriteLine($"List Index [{i}] -> DB ID: {inst.Id}, Name: {inst.Name}, DataIndex: {inst.DataIndex}");
            }

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



    //private void HandleSimData(double[] incomingData)
    //{
    //    if (incomingData == null || incomingData.Length == 0) return;

    //    // Ensure we are on the UI thread to update the NotMapped properties
    //    Application.Current.Dispatcher.Invoke(() =>
    //    {
    //        for (int i = 0; i < ActiveInstruments.Count; i++)
    //        {
    //            if (i < incomingData.Length)
    //            {
    //                var inst = ActiveInstruments[i];

    //                // 1. Update the UI live values
    //                inst.RawSimValue = incomingData[i];

    //                // 2. Calculate the byte to send to Arduino
    //                byte scaledValue = ScaleValue(inst.RawSimValue, inst);
    //                inst.SentByteValue = scaledValue;

    //                // 3. SHIP IT! 
    //                // We use your local method that builds the 7-byte packet
    //                if (IsArduinoConnected)
    //                {
    //                    SendToHardware(inst, scaledValue);
    //                }
    //            }
    //        }
    //    });
    //}
    //private void HandleSimData(double[] incomingData)
    //{
    //    if (incomingData == null || incomingData.Length == 0) return;

    //    Application.Current.Dispatcher.Invoke(() =>
    //    {
    //        for (int i = 0; i < ActiveInstruments.Count; i++)
    //        {
    //            if (i < incomingData.Length)
    //            {
    //                var inst = ActiveInstruments[i];
    //                double newRawValue = incomingData[i];

    //                // 1. Calculate what the NEW byte WOULD be
    //                byte newScaledValue = ScaleValue(newRawValue, inst);

    //                // 2. ONLY act if the final byte has changed
    //                // This ignores tiny decimal jitters that don't result in a new position
    //                if (newScaledValue != inst.SentByteValue)
    //                {
    //                    // Update UI properties
    //                    inst.RawSimValue = newRawValue;
    //                    inst.SentByteValue = newScaledValue;

    //                    // 3. SHIP IT!
    //                    if (IsArduinoConnected)
    //                    {
    //                        SendToHardware(inst, newScaledValue);
    //                    }
    //                }
    //                else
    //                {
    //                    // Optional: Update the Raw value for UI display even if we don't send to hardware
    //                    // Comment this out if you want the UI to be as "quiet" as the hardware
    //                    inst.RawSimValue = newRawValue;
    //                }
    //            }
    //        }
    //    });
    //}
    private void HandleSimData(double[] incomingData)
    {
        if (incomingData == null || incomingData.Length == 0) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            for (int i = 0; i < ActiveInstruments.Count; i++)
            {
                if (i < incomingData.Length)
                {
                    var inst = ActiveInstruments[i];
                    double newRawValue = incomingData[i];
                    byte newScaledValue = ScaleValue(newRawValue, inst);

                    // ONLY act if the final byte has changed
                    if (newScaledValue != inst.SentByteValue)
                    {
                        // Update values
                        inst.RawSimValue = newRawValue;
                        byte oldByte = inst.SentByteValue; // For logging
                        inst.SentByteValue = newScaledValue;

                        if (IsArduinoConnected)
                        {
                            // Log the transmission
                            System.Diagnostics.Debug.WriteLine(
                                $"[TX] {inst.Name.PadRight(15)} | Raw: {newRawValue:F2} | Byte: {oldByte} -> {newScaledValue} (Slot: {inst.Slot})");

                            SendToHardware(inst, newScaledValue);
                        }
                    }
                    else
                    {
                        // Still update UI numbers, but notice we DON'T log [TX] here
                        inst.RawSimValue = newRawValue;
                    }
                }
            }
        });
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
                // Only seed if the database is truly empty
                if (!db.Instruments.Any())
                {
                    // 1. Create the Nano Devices
                    var flapNano = new ArduinoDevice { FriendlyName = "Flap Nano", I2CAddress = 0x09 };
                    var trimNano = new ArduinoDevice { FriendlyName = "Trim Nano", I2CAddress = 0x0A };

                    db.Arduinos.AddRange(flapNano, trimNano);
                    await db.SaveChangesAsync(); // Save to get IDs for the foreign keys

                    // 2. Create the Instruments
                    db.Instruments.AddRange(
                        new SimInstrument
                        {
                            Name = "Flaps",
                            ArduinoDeviceId = flapNano.Id,
                            SimVarName = "TRAILING EDGE FLAPS LEFT PERCENT",
                            Units = "Percent",
                            TelemetryPrefix = "FLP",  // New String Prefix
                            DeviceType = 1,           // 1 = Servo
                            Slot = 0,                // Pin 9 on the Nano
                            DataIndex = 0,
                            InputMin = 0,
                            InputMax = 100,
                            OutputMin = 0,
                            OutputMax = 255          // We scale to 0-255 for the I2C packet
                        },
                        new SimInstrument
                        {
                            Name = "Elevator Trim",
                            ArduinoDeviceId = trimNano.Id,
                            SimVarName = "ELEVATOR TRIM PCT",
                            Units = "Percent",
                            TelemetryPrefix = "TRM",  // New String Prefix
                            DeviceType = 1,           // 1 = Servo
                            Slot = 0,                // Pin 9 on the Trim Nano
                            DataIndex = 1,
                            InputMin = -100,
                            InputMax = 100,
                            OutputMin = 0,
                            OutputMax = 255          // We scale to 0-255 for the I2C packet
                        }
                    );
                    await db.SaveChangesAsync();
                }
            }

            // After seeding (or if already seeded), load the data into the UI
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
        var flapConfig = _activeInstruments.FirstOrDefault(i => i.TelemetryPrefix == "F");

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
            //_arduinoService.SendData(instrument.TelemetryPrefix, scaledValue);
            SendToHardware(instrument, scaledValue);

            System.Diagnostics.Debug.WriteLine($"Manual Test [{instrument.Name}]: UI {percent}% -> SimValue {simValue} -> Byte {scaledValue}");
        }
    }
    private void SendToHardware(SimInstrument inst, byte scaledValue)
    {
        if (!_arduinoService.IsConnected || inst.Arduino == null) return;

        // Build the 7-byte Packet
        byte[] packet = new byte[7];

        // Bytes 0-2: Prefix (e.g., "FLP", "TRM", "LF ")
        string prefix = (inst.TelemetryPrefix ?? "???").PadRight(3).Substring(0, 3);
        packet[0] = (byte)prefix[0];
        packet[1] = (byte)prefix[1];
        packet[2] = (byte)prefix[2];

        // Byte 3: The actual data
        packet[3] = scaledValue;

        // Bytes 4-6: Routing info
        packet[4] = (byte)inst.Arduino.I2CAddress;
        packet[5] = (byte)inst.DeviceType;
        packet[6] = (byte)inst.Slot;

        _arduinoService.SendDataRaw(packet);
    }
}