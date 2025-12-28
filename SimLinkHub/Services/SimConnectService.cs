using System;
using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace SimLinkHub.Services
{
    public class SimConnectService
    {
        public event Action<SimData>? OnDataReceived;
        private SimConnect? _simConnect;
        public bool IsConnected { get; private set; }

        // User-defined message ID
        public const int WM_USER_SIMCONNECT = 0x0402;

        public void Connect(IntPtr windowHandle)
        {
            if (IsConnected) return;

            try
            {
                _simConnect = new SimConnect("SimLinkHub", windowHandle, WM_USER_SIMCONNECT, null, 0);

                // 1. Map the Sim Variables to our struct
                _simConnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_DATA, "TRAILING EDGE FLAPS LEFT PERCENT", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                _simConnect.AddToDataDefinition(DEFINITIONS.AIRCRAFT_DATA, "ELEVATOR TRIM PCT", "Percent", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);


                // 2. Register the struct type
                _simConnect.RegisterDataDefineStruct<SimData>(DEFINITIONS.AIRCRAFT_DATA);

                // 3. Listen for the data
                _simConnect.OnRecvSimobjectData += OnRecvSimobjectData;

                // 4. Request the data automatically every time it changes (SIMCONNECT_PERIOD.VISUAL_FRAME)
                _simConnect.RequestDataOnSimObject(DATA_REQUESTS.AIRCRAFT_DATA, DEFINITIONS.AIRCRAFT_DATA, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.VISUAL_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);


                // Subscribe to basic events
                // Subscribe to basic events
                _simConnect.OnRecvOpen += (s, data) =>
                {
                    IsConnected = true;
                    StartManualPump(); // <--- START THE PUMP HERE!
                };

                _simConnect.OnRecvQuit += (s, data) => Disconnect();
            }
            catch (COMException)
            {
                IsConnected = false;
            }
        }

        public void Disconnect()
        {
            _simConnect?.Dispose();
            _simConnect = null;
            IsConnected = false;
        }

        // This must be called when a Windows Message arrives
        public void ReceiveMessage()
        {
            _simConnect?.ReceiveMessage();
        }

        private void OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (data.dwRequestID == (uint)DATA_REQUESTS.AIRCRAFT_DATA)
            {
                SimData aircraftData = (SimData)data.dwData[0];

                // This sends the data out to the ViewModel
                OnDataReceived?.Invoke(aircraftData);
            }
        }
        public void StartManualPump()
        {
            Task.Run(async () => {
                while (IsConnected)
                {
                    ReceiveMessage(); // Manually pull messages every 10ms
                    await Task.Delay(10);
                }
            });
        }

    }

    public enum DATA_REQUESTS
    {
        AIRCRAFT_DATA
    }

    public enum DEFINITIONS
    {
        AIRCRAFT_DATA
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SimData
    {
        // This variable name doesn't matter, but the "units" in RegisterDataDefine do!
        public double FlapsTrailingEdgePercent { get; set; }
        public double ElevatorTrimPercent { get; set; }
    }

}