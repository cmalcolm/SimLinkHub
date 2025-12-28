using System.ComponentModel.DataAnnotations;

public class DeviceConfig
{
    [Key]
    public int Id { get; set; }
    public string ProfileName { get; set; } = "Default";
    public string ComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;
    public bool AutoConnect { get; set; } = false;
}