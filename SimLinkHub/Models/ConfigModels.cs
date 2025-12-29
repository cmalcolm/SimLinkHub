using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SimLinkHub.Models;

public class ArduinoDevice
{
    [Key]
    public int Id { get; set; }
    public string FriendlyName { get; set; } = "New Nano";
    public byte I2CAddress { get; set; } // e.g., 0x09 or 0x10
}

//public class SimInstrument
public partial class SimInstrument : ObservableObject
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = "Unnamed Indicator"; // e.g. "Trim Indicator"

    // Linking to the Arduino table
    public int ArduinoDeviceId { get; set; }
    public ArduinoDevice? Arduino { get; set; }

    // SimConnect & Routing Info
    public string SimVarName { get; set; } = ""; // e.g. "ELEVATOR TRIM PCT"
    public string Units { get; set; } = "Percent";        // e.g. "Percent", "Knots", "Feet"
    public string TelemetryPrefix { get; set; } = string.Empty; // e.g. 'TRM' or 'FLP'
    public int DeviceType { get; set; } = 1; // 1=Servo, 2=LED, etc.
    public int Slot { get; set; } = 0;       // Pin/Index on the Nano

    public int DataIndex { get; set; }
    // Hardware/Pin Details
    public int SignalPin { get; set; } // The physical pin on the Nano
    public string PinType { get; set; } = "PWM"; // PWM, Servo, Stepper

    // Scaling/Math
    public double InputMin { get; set; } = -100.0;
    public double InputMax { get; set; } = 100.0;
    public byte OutputMin { get; set; } = 0;
    public byte OutputMax { get; set; } = 255;

    public bool IsInverted { get; set; } = false;
    [ObservableProperty]
    [property: NotMapped] // This forces [NotMapped] onto the generated 'RawSimValue'
    private double _rawSimValue;

    [ObservableProperty]
    [property: NotMapped] // This forces [NotMapped] onto the generated 'SentByteValue'
    private byte _sentByteValue;
}
