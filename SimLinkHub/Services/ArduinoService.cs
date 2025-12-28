using SimLinkHub.Data;
using Microsoft.EntityFrameworkCore;
using System.IO.Ports;
using System.Threading.Tasks;

namespace SimLinkHub.Services
{
    public class ArduinoService
    {
        private SerialPort? _serialPort;
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public async Task<bool> AttemptConnectionAsync()
        {
            // 1. Try the "Remembered" port first to save time
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();

                var savedConfig = await db.Configs.FirstOrDefaultAsync(c => c.ProfileName == "Default");
                if (savedConfig != null && !string.IsNullOrEmpty(savedConfig.ComPort))
                {
                    if (await TryConnect(savedConfig.ComPort, savedConfig.BaudRate))
                        return true;
                }
            }

            // 2. If remembered port fails, scan all available ports
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                if (await TryConnect(port, 115200)) // Use your specific baud rate
                {
                    // 3. SUCCESS! Save this port to the database for next time
                    using var db = new AppDbContext();
                    var config = await db.Configs.FirstOrDefaultAsync(c => c.ProfileName == "Default")
                                 ?? new DeviceConfig { ProfileName = "Default" };

                    config.ComPort = port;
                    config.BaudRate = 115200;

                    if (db.Entry(config).State == EntityState.Detached) db.Configs.Add(config);
                    await db.SaveChangesAsync();

                    return true;
                }
            }
            return false;
        }

        // Helper method to keep the code clean
        private async Task<bool> TryConnect(string portName, int baud)
        {
            try
            {
                var p = new SerialPort(portName, baud);

                // Set timeouts BEFORE opening
                p.WriteTimeout = 500;
                p.ReadTimeout = 500;

                p.Open();

                // --- ADD/UPDATE THESE SETTINGS HERE ---
                p.DtrEnable = true;  // Crucial for Leonardo to stay awake
                p.RtsEnable = true;  // Handshaking line
                                     // --------------------------------------

                p.DiscardInBuffer();
                p.Write("?");

                await Task.Delay(600);

                string response = p.ReadExisting();

                if (response.Contains("LEO_BRIDGE"))
                {
                    // Cleanup old port if necessary
                    if (_serialPort != null)
                    {
                        _serialPort.DataReceived -= SerialPort_DataReceived;
                        _serialPort.Close();
                    }

                    _serialPort = p;
                    _serialPort.DataReceived += SerialPort_DataReceived;

                    return true;
                }

                p.Close();
                p.Dispose();
            }
            catch
            {
                /* Port busy or timed out */
            }
            return false;
        }

        // Add this method to your ArduinoService class
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                string data = _serialPort.ReadExisting();
                // Forward this data to your ViewModel via an event or Action
                System.Diagnostics.Debug.WriteLine($"Arduino Data: {data}");
            }
            catch { /* Handle unexpected disconnects */ }
        }

        public void SendData(char prefix, byte value)
        {
            // Only attempt to send if the port is open and valid
            if (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    // We create a small array of 2 bytes: [Prefix, Value]
                    // Example: ['F', 128]
                    byte[] buffer = new byte[2];
                    buffer[0] = (byte)prefix;
                    buffer[1] = value;

                    _serialPort.Write(buffer, 0, 2);
                }
                catch (UnauthorizedAccessException)
                {
                    // This happens if the port is suddenly snatched away or busy
                    System.Diagnostics.Debug.WriteLine("Serial Port Busy - Access Denied.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Serial Send Error: {ex.Message}");
                }
            }
        }

    }
}