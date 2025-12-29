using Microsoft.FlightSimulator.SimConnect;
using SimLinkHub.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SimLinkHub.Services
{
    public class SimConnectService
    {
        // 1. UPDATED: The event now passes the array of doubles
        public event Action<double[]>? OnDataReceived;
        private SimConnect? _simConnect;
        public bool IsConnected { get; private set; }

        public const int WM_USER_SIMCONNECT = 0x0402;

        public void Connect(IntPtr windowHandle, List<SimInstrument> instruments)
        {
            if (IsConnected) return;

            try
            {
                _simConnect = new SimConnect("SimLinkHub", windowHandle, WM_USER_SIMCONNECT, null, 0);

                // 2. Map SimVars to indices in the buffer
                foreach (var inst in instruments)
                {
                    _simConnect.AddToDataDefinition(
                        DEFINITIONS.AIRCRAFT_DATA,
                        inst.SimVarName,
                        inst.Units,
                        SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                }

                // 3. Register the DynamicBuffer struct
                _simConnect.RegisterDataDefineStruct<DynamicBuffer>(DEFINITIONS.AIRCRAFT_DATA);

                // 4. Handle Incoming Data
                _simConnect.OnRecvSimobjectDataBytype += (sender, data) =>
                {
                    // Use the standardized enum AIRCRAFT_DATA_REQUEST
                    if (data.dwRequestID == (uint)DATA_REQUESTS.AIRCRAFT_DATA_REQUEST)
                    {
                        DynamicBuffer buffer = (DynamicBuffer)data.dwData[0];
                        double[] values = buffer.ToArray();
                        OnDataReceived?.Invoke(values);
                    }
                };

                // 5. Request Data
                _simConnect.RequestDataOnSimObject(
                    DATA_REQUESTS.AIRCRAFT_DATA_REQUEST,
                    DEFINITIONS.AIRCRAFT_DATA,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.VISUAL_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);

                _simConnect.OnRecvOpen += (s, data) =>
                {
                    IsConnected = true;
                    StartManualPump();
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

        public void ReceiveMessage() => _simConnect?.ReceiveMessage();

        public void StartManualPump()
        {
            Task.Run(async () => {
                while (IsConnected)
                {
                    ReceiveMessage();
                    await Task.Delay(10);
                }
            });
        }

    }

    // Standardized Enums
    public enum DATA_REQUESTS { AIRCRAFT_DATA_REQUEST }
    public enum DEFINITIONS { AIRCRAFT_DATA }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DynamicBuffer
    {
        public double Val0; public double Val1; public double Val2; public double Val3;
        public double Val4; public double Val5; public double Val6; public double Val7;
        public double Val8; public double Val9; public double Val10; public double Val11;
        public double Val12; public double Val13; public double Val14; public double Val15;
        public double Val16; public double Val17; public double Val18; public double Val19;
        public double Val20; public double Val21; public double Val22; public double Val23;
        public double Val24; public double Val25; public double Val26; public double Val27;
        public double Val28; public double Val29; public double Val30; public double Val31;

        public double[] ToArray()
        {
            return new double[] {
                Val0, Val1, Val2, Val3, Val4, Val5, Val6, Val7, Val8, Val9, Val10, Val11, Val12, Val13, Val14, Val15,
                Val16, Val17, Val18, Val19, Val20, Val21, Val22, Val23, Val24, Val25, Val26, Val27, Val28, Val29, Val30, Val31
            };
        }
    }

}