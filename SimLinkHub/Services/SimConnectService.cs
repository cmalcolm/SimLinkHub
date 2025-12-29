using Microsoft.FlightSimulator.SimConnect;
using SimLinkHub.Data;
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
                // 1. CREATE the SimConnect instance FIRST
                _simConnect = new SimConnect("SimLinkHub", windowHandle, WM_USER_SIMCONNECT, null, 0);

                // 2. Clear and Log the order
                System.Diagnostics.Debug.WriteLine("=== SIMCONNECT REGISTRATION ORDER ===");

                // 3. SINGLE LOOP to register everything
                for (int i = 0; i < instruments.Count; i++)
                {
                    var inst = instruments[i];
                    System.Diagnostics.Debug.WriteLine($"Definition Slot [{i}] -> Registering: {inst.SimVarName} (Index: {inst.DataIndex})");

                    _simConnect.AddToDataDefinition(
                        DEFINITIONS.GenericData,
                        inst.SimVarName,
                        inst.Units,
                        SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                }

                // 4. Register the Buffer Struct
                _simConnect.RegisterDataDefineStruct<DynamicBuffer>(DEFINITIONS.GenericData);

                // 5. Setup Data Handler
                //_simConnect.OnRecvSimobjectData += (sender, data) =>
                //{
                //    if (data.dwRequestID == (uint)DATA_REQUESTS.GenericRequest)
                //    {
                //        if (data.dwData is object[] dataArray && dataArray.Length > 0)
                //        {
                //            var buffer = (DynamicBuffer)dataArray[0];
                //            double[] values = buffer.ToArray();
                //            OnDataReceived?.Invoke(values);
                //        }
                //    }
                //};
                
                _simConnect.OnRecvSimobjectData += (sender, data) =>
                {
                    if (data.dwRequestID == (uint)DATA_REQUESTS.GenericRequest)
                    {
                        // 1. data.dwData is an object array where [0] is your DynamicBuffer struct
                        if (data.dwData is object[] dataArray && dataArray.Length > 0)
                        {
                            // 2. Cast the first element back to the struct
                            var buffer = (DynamicBuffer)dataArray[0];

                            // 3. Convert the 32-field struct into a double array
                            double[] allValues = buffer.ToArray();

                            // 4. IMPORTANT: We only want to send the values we actually registered
                            // If you have 4 instruments, only send the first 4 doubles
                            double[] activeValues = allValues.Take(instruments.Count).ToArray();

                            System.Diagnostics.Debug.WriteLine($"RAW DATA: {string.Join(" | ", activeValues)}");

                            OnDataReceived?.Invoke(activeValues);
                        }
                    }
                };

                // 6. Setup Connection Event
                _simConnect.OnRecvOpen += (s, data) =>
                {
                    IsConnected = true;
                    _simConnect.RequestDataOnSimObject(
                        DATA_REQUESTS.GenericRequest,
                        DEFINITIONS.GenericData,
                        SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        SIMCONNECT_PERIOD.VISUAL_FRAME,
                        SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);
                };

                _simConnect.OnRecvQuit += (s, data) => Disconnect();
            }
            catch (COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"SimConnect Connection Failed: {ex.Message}");
                IsConnected = false;
            }
        }

        //public void Connect(IntPtr windowHandle, List<SimInstrument> instruments)
        //{
        //    if (IsConnected) return;

        //    try
        //    {
        //        _simConnect = new SimConnect("SimLinkHub", windowHandle, WM_USER_SIMCONNECT, null, 0);

        //        // 2. Map SimVars to indices in the buffer
        //        foreach (var inst in instruments)
        //        {
        //            _simConnect.AddToDataDefinition(
        //                DEFINITIONS.AIRCRAFT_DATA,
        //                inst.SimVarName,
        //                inst.Units,
        //                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        //        }

        //        // 3. Register the DynamicBuffer struct
        //        _simConnect.RegisterDataDefineStruct<DynamicBuffer>(DEFINITIONS.AIRCRAFT_DATA);

        //        // 4. Handle Incoming Data
        //        _simConnect.OnRecvSimobjectDataBytype += (sender, data) =>
        //        {
        //            // Use the standardized enum AIRCRAFT_DATA_REQUEST
        //            if (data.dwRequestID == (uint)DATA_REQUESTS.AIRCRAFT_DATA_REQUEST)
        //            {
        //                DynamicBuffer buffer = (DynamicBuffer)data.dwData[0];
        //                double[] values = buffer.ToArray();
        //                OnDataReceived?.Invoke(values);
        //            }
        //        };

        //        // 5. Request Data
        //        _simConnect.RequestDataOnSimObject(
        //            DATA_REQUESTS.AIRCRAFT_DATA_REQUEST,
        //            DEFINITIONS.AIRCRAFT_DATA,
        //            SimConnect.SIMCONNECT_OBJECT_ID_USER,
        //            SIMCONNECT_PERIOD.VISUAL_FRAME,
        //            SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);

        //        _simConnect.OnRecvOpen += (s, data) =>
        //        {
        //            IsConnected = true;
        //            StartManualPump();
        //        };

        //        _simConnect.OnRecvQuit += (s, data) => Disconnect();
        //    }
        //    catch (COMException)
        //    {
        //        IsConnected = false;
        //    }
        //}
        //public void Connect(IntPtr windowHandle, List<SimInstrument> instruments)
        //{
        //    if (IsConnected) return;

        //    System.Diagnostics.Debug.WriteLine("=== SIMCONNECT REGISTRATION ORDER ===");
        //    for (int i = 0; i < instruments.Count; i++)
        //    {
        //        var inst = instruments[i];
        //        System.Diagnostics.Debug.WriteLine($"Definition Slot [{i}] -> Registering: {inst.SimVarName}");

        //        _simConnect.AddToDataDefinition(
        //            DEFINITIONS.GenericData,
        //            inst.SimVarName,
        //            inst.Units,
        //            SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        //    }

        //    try
        //    {
        //        _simConnect = new SimConnect("SimLinkHub", windowHandle, WM_USER_SIMCONNECT, null, 0);

        //        // 1. Loop through instruments and add to the definition
        //        for (int i = 0; i < instruments.Count; i++)
        //        {
        //            _simConnect.AddToDataDefinition(
        //                DEFINITIONS.GenericData,
        //                instruments[i].SimVarName,
        //                instruments[i].Units,
        //                SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        //        }

        //        // 2. Register the Buffer Struct to this definition
        //        _simConnect.RegisterDataDefineStruct<DynamicBuffer>(DEFINITIONS.GenericData);

        //        // 3. Handle Incoming Data (Note: using OnRecvSimobjectData instead of Bytype)
        //        //_simConnect.OnRecvSimobjectData += (sender, data) =>
        //        //{
        //        //    if (data.dwRequestID == (uint)DATA_REQUESTS.GenericRequest)
        //        //    {
        //        //        // The fix: data.dwData comes back as an array of objects.
        //        //        // We need the first element [0], which is our DynamicBuffer.
        //        //        if (data.dwData is object[] dataArray && dataArray.Length > 0)
        //        //        {
        //        //            DynamicBuffer buffer = (DynamicBuffer)dataArray[0];
        //        //            double[] values = buffer.ToArray();
        //        //            OnDataReceived?.Invoke(values);
        //        //        }
        //        //    }
        //        //};
        //        _simConnect.OnRecvSimobjectData += (sender, data) =>
        //        {
        //            if (data.dwRequestID == (uint)DATA_REQUESTS.GenericRequest)
        //            {
        //                // 1. Cast the 'dwData' to the object array the library provided
        //                if (data.dwData is object[] dataArray && dataArray.Length > 0)
        //                {
        //                    // 2. Extract the first element and cast it to your struct
        //                    var buffer = (DynamicBuffer)dataArray[0];

        //                    // 3. Convert to array and send to the ViewModel
        //                    double[] values = buffer.ToArray();
        //                    OnDataReceived?.Invoke(values);
        //                }
        //            }
        //        };

        //        // 4. Set up Connection Events
        //        _simConnect.OnRecvOpen += (s, data) =>
        //        {
        //            IsConnected = true;

        //            // 5. Request the data once the connection is open
        //            _simConnect.RequestDataOnSimObject(
        //                DATA_REQUESTS.GenericRequest,
        //                DEFINITIONS.GenericData,
        //                SimConnect.SIMCONNECT_OBJECT_ID_USER,
        //                SIMCONNECT_PERIOD.VISUAL_FRAME,
        //                SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);

        //            //StartManualPump();
        //        };

        //        _simConnect.OnRecvQuit += (s, data) => Disconnect();
        //    }
        //    catch (COMException)
        //    {
        //        IsConnected = false;
        //    }
        //}
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
        public void RegisterSimVars()
        {
            try
            {
                // 1. Get the list of unique variables from your Database
                List<SimInstrument> instruments;
                using (var db = new AppDbContext())
                {
                    instruments = db.Instruments.ToList();
                }

                // 2. Register each variable with SimConnect
                // We use the index as the 'DatumID' to keep track of them
                for (int i = 0; i < instruments.Count; i++)
                {
                    _simConnect.AddToDataDefinition(
                        DEFINITIONS.GenericData,
                        instruments[i].SimVarName,
                        instruments[i].Units,
                        SIMCONNECT_DATATYPE.FLOAT64,
                        0.0f,
                        (uint)i // This ID helps us identify the data later
                    );
                }

                // 3. Request the data to be sent every visual frame
                _simConnect.RequestDataOnSimObject(
                    DATA_REQUESTS.GenericRequest,
                    DEFINITIONS.GenericData,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.VISUAL_FRAME,
                    SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, // Only send if value changes
                    0, 0, 0
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error Registering Vars: {ex.Message}");
            }
        }

    }

    // Standardized Enums
    public enum DATA_REQUESTS { AIRCRAFT_DATA_REQUEST, GenericRequest = 100 }
    public enum DEFINITIONS { AIRCRAFT_DATA, GenericData = 100 }

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