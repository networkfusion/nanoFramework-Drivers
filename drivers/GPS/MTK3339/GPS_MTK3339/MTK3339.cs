/*
 * Original Driver based on:
 * 
 * Copyright 2010 Thomas W. Holtquist
 * www.skewworks.com
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * Updated by Robin Jones
 */

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.Devices.Gpio;

namespace nanoframework.Devices.GPS
{

    #region Event Delegates

    public delegate void OnAltitudeChanged(MTK3339 sender, double meters, int feet);
    public delegate void OnCoordinatesUpdated(MTK3339 sender);
    public delegate void OnCourseChanged(MTK3339 sender, double degrees);
    public delegate void OnError(MTK3339 sender, string data);
    public delegate void OnFixModeChanged(MTK3339 sender, MTK3339.FixModes e);
    public delegate void OnFixTypeChanged(MTK3339 sender, MTK3339.FixTypes e);
    public delegate void OnGSAModeChanged(MTK3339 sender, MTK3339.GSAModes e);
    public delegate void OnRMCModeChanged(MTK3339 sender, MTK3339.RMCModes e);
    public delegate void OnSatellitesInViewChanged(MTK3339 sender, MTK3339.SatelliteInView[] e);
    public delegate void OnSatellitesUsedChanged(MTK3339 sender, int count);
    public delegate void OnSpeedChanged(MTK3339 sender, double knots, double mph);
    public delegate void OnTimeChanged(MTK3339 sender, int hour, int minute, int second);
    public delegate void OnDateChanged(MTK3339 sender, int month, int day, int year);
    public delegate void OnVTGModeChanged(MTK3339 sender, MTK3339.VTGModes e);

    #endregion

    public class MTK3339
    {

        #region Constants

        private const string UPDATE_RATE_1HZ = "$PMTK220,1000*1F\r\n";
        private const string UPDATE_RATE_5HZ = "$PMTK220,200*2C\r\n";
        private const string UPDATE_RATE_10HZ = "$PMTK220,100*2F\r\n";

        private const string START_HOT = "$PMTK101*32\r\n";
        private const string START_WARM = "$PMTK102*31\r\n";
        private const string START_COLD = "$PMTK103*30\r\n";
        private const string START_FACTORY_RESET = "$PMTK104*37\r\n";

        private const string SUBSCRIBE_RMCONLY = "$PMTK314,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0*29\r\n";
        private const string SUBSCRIBE_RMCGGA = "$PMTK314,0,1,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0*28\r\n";
        private const string SUBSCRIBE_ALLDATA = "$PMTK314,1,1,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0*28\r\n";
        private const string SUBSCRIBE_OUTOFF = "$PMTK314,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0*28\r\n";

        private const string ENTER_BINARY_MODE = "$PMTK253,1,57600*33\r\n";
        private const string CLEAR_EPO_INFO = "$PMTK127*36\r\n";

        #endregion

        #region Enumerations

        public enum FixModes
        {
            Manual = 0,
            Automatic = 1,
        }

        public enum FixTypes
        {
            NoFix = 0,
            GPSFix = 1,
            DiffGPS = 2,
        }

        public enum GSAModes
        {
            NoFix = 0,
            Fix2D = 1,
            Fix3D = 2,
        }

        public enum RMCModes
        {
            Autonomous = 0,
            Differential = 1,
            Estimated = 2,
        }

        public enum SubscriptionLevels
        {
            AllData = 0,
            RMCGGA = 1,
            RMCOnly = 2,
            OutputOff = 3,
        }

        public enum UpdateRates
        {
            GPS_1HZ = 0,
            GPS_5HZ = 1,
            GPS_10HZ = 2,
        }

        public enum StartupMode
        {
            Cold,
            Warm,
            Hot,
            FactoryReset
        }

        public enum VTGModes
        {
            Autonomous = 0,
            Differential = 1,
            Estimated = 2,
        }

        #endregion

        #region Structures

        public struct SatelliteInView
        {
            public int PRNNumber;
            public int Elevation;
            public int Azimuth;
            public double SignalNoiseRatio;
            public SatelliteInView(int PRNNumber, int Elevation, int Azimuth, double SignalNoiseRatio)
            {
                this.PRNNumber = PRNNumber;
                this.Elevation = Elevation;
                this.Azimuth = Azimuth;
                this.SignalNoiseRatio = SignalNoiseRatio;
            }
        }

        #endregion

        #region Variables
        private SerialDevice serialPort;
        private GpioPin resetControl;
        private GpioPin sleepControl;

        private UpdateRates _rate;
        private SubscriptionLevels _subLevel;

        private string _latitude = "";
        private double _mapLatitude = double.NaN;

        private string _longitude = "";
        private double _mapLongitude = double.NaN;

        private int _timezoneOffset;
        private bool _dstEnable;

        private FixTypes _fixType;
        private int _satsUsed;

        private double _HOD;
        private double _POD;
        private double _VOD;

        private double _altitude;
        private double _geoSep;
        private int _diffAge;

        private double _speed;
        private double _course;
        private RMCModes _rmcMode;

        private FixModes _fixMode;
        private GSAModes _gsaMode;
        private int[] _gsaSatellites;

        private SatelliteInView[] _satInView;
        private SatelliteInView[] _satInViewTmp;
        private int _svCount;
        private DateTime _currentDateTime;

        private VTGModes _vtgMode;

        // It seems that the module is ready to work after aprox. 100 ms after the reset
        private const int StartupTime = 200;

        private static class ResetState
        {
            public const GpioPinValue NotRunning = GpioPinValue.Low;
            public const GpioPinValue Running = GpioPinValue.High;
        }

        private static class SleepState
        {
            public const GpioPinValue Awaken = GpioPinValue.Low;
            public const GpioPinValue Sleeping = GpioPinValue.High;
        }

        #endregion

        #region DebugFunctions
        public void QueryEmbeddedAssistSystem()
        {
            byte[] b = UTF8Encoding.UTF8.GetBytes("$PMTK869,0*29\r\n");

            outputDataWriter.WriteBytes(b);
        }

        public void QuerySBAS()
        {
            byte[] b = UTF8Encoding.UTF8.GetBytes("$PMTK413*34\r\n");

            outputDataWriter.WriteBytes(b);
        }

        public void QuerySpeedThreshold()
        {
            byte[] b = UTF8Encoding.UTF8.GetBytes("$PMTK447*35\r\n");

            outputDataWriter.WriteBytes(b);
        }

        #endregion

        #region Constructors
        SerialDevice serialDevice;
        DataWriter outputDataWriter;
        //DataReader inputDataReader;


        static int PinNumber(char port, byte pin)
        {
            if (port < 'A' || port > 'J')

                throw new ArgumentException();

            return ((port - 'A') * 16) + pin;
        }

        /// <summary>Constructs a new instance.</summary>
        /// <param name="socketNumber">The socket that this module is plugged in to.</param>
        public MTK3339(string port, int resetPin, int sleepControlPin)
        {
            var serialPorts = SerialDevice.GetDeviceSelector();
            serialDevice = SerialDevice.FromId(port);
            outputDataWriter = new DataWriter(serialDevice.OutputStream);
            //inputDataReader = new DataReader(serialDevice.InputStream);

            //TODO: to make these work properly, look at: https://xbee.codeplex.com/SourceControl/latest#source/XBee.Gadgeteer_42/XBee.cs
            var gpioController = GpioController.GetDefault();
            //this.resetControl = GTI.DigitalOutputFactory.Create(socket, Socket.Pin.Three, ResetState.NotRunning, this);
            this.resetControl = gpioController.OpenPin(resetPin);
            this.resetControl.SetDriveMode(GpioPinDriveMode.Input);
            //this.sleepControl = GTI.DigitalOutputFactory.Create(socket, Socket.Pin.Eight, SleepState.Awaken, this);
            this.sleepControl = gpioController.OpenPin(sleepControlPin);
            this.sleepControl.SetDriveMode(GpioPinDriveMode.Input);


            //if (this.serialPort != null && this.serialPort.IsOpen)
            //    throw new InvalidOperationException("Configure can only be when the port is closed. Call Disable first");


            // set parameters
            serialDevice.BaudRate = 9600;
            serialDevice.DataBits = 8;
            serialDevice.Parity = SerialParity.None;
            serialDevice.StopBits = SerialStopBitCount.One;
            serialDevice.Handshake = SerialHandshake.None;

            serialDevice.ReadTimeout = new TimeSpan(0, 0, 4);
            serialPort.DataReceived += serialPort_DataReceived;


        }

        /// <summary>
        /// Perform module hardware reset.
        /// </summary>
        public void Reset()
        {
            // reset pulse must be at least 200 ns
            // .net mf latency between calls is enough
            // no need to add any extra Thread.Sleep
            Disable();
            Enable();
        }


        /// <summary>
        /// Returns state of the module that is controlled by reset pin.
        /// </summary>
        public bool Enabled
        {
            get
            {
                    return resetControl.Read() == ResetState.Running;
            }
        }

        /// <summary>
        /// If the module is configured to work in PinSleep mode this value determines if it's asleep or not.
        /// </summary>
        public bool Sleeping
        {
            get
            {
                return sleepControl.Read() == SleepState.Sleeping;
            }
        }

        /// <summary>
        /// Disables the module (power off).
        /// </summary>
        public void Disable()
        {
            //serialPort.Close(); TODO: No support in UWP does anything need to happen instead?
            resetControl.Write(ResetState.NotRunning);
        }

        /// <summary>
        /// Enabled the module (power on).
        /// </summary>
        public void Enable()
        {
            resetControl.Write(ResetState.Running);
            Thread.Sleep(StartupTime);
            //serialPort.Open(); TODO: No support in UWP does anything need to happen instead?

        }

        /// <summary>
        /// Sets the sleep control pin to active state (sleep request).
        /// </summary>
        public void Sleep()
        {
            sleepControl.Write(SleepState.Sleeping);
        }

        /// <summary>
        /// Sets the sleep control pin to inactive state (no sleep request)
        /// </summary>
        public void Awake()
        {
            sleepControl.Write(SleepState.Awaken);
        }




        /// <summary>
        /// Initializes the GPS with the given parameters.
        /// </summary>
        /// <param name="SubscriptionLevel"></param>
        /// <param name="UpdateRate"></param>
        public void Configure(SubscriptionLevels SubscriptionLevel, UpdateRates UpdateRate = UpdateRates.GPS_1HZ)
        {
            Enable();

            // Set our Update Rate
            _rate = UpdateRate;
            SetUpdateRate();

            // Set our Subscription Level
            _subLevel = SubscriptionLevel;
            SetSubscriptionLevel();

            SetModeNMEA(9600);
        }


        #endregion

        #region Events

        public event OnAltitudeChanged AltitudeChanged;
        protected virtual void OnAltitudeChanged(MTK3339 sender, double meters, int feet)
        {
            if (AltitudeChanged != null)
                AltitudeChanged(sender, meters, feet);
        }

        public event OnCoordinatesUpdated CoordinatesUpdated;
        protected virtual void OnCoordinatesUpdated(MTK3339 sender)
        {
            if (CoordinatesUpdated != null)
                CoordinatesUpdated(sender);
        }

        public event OnCourseChanged CourseChanged;
        protected virtual void OnCourseChanged(MTK3339 sender, double degrees)
        {
            if (CourseChanged != null)
                CourseChanged(sender, degrees);
        }

        public event OnError Error;
        protected virtual void OnError(MTK3339 sender, string data)
        {
            if (Error != null)
                Error(sender, data);
        }

        public event OnFixModeChanged FixModeChanged;
        protected virtual void OnFixModeChanged(MTK3339 sender, FixModes e)
        {
            if (FixModeChanged != null)
                FixModeChanged(sender, e);
        }

        public event OnFixTypeChanged FixTypeChanged;
        protected virtual void OnFixTypeChanged(MTK3339 sender, FixTypes e)
        {
            if (FixTypeChanged != null)
                FixTypeChanged(sender, e);
        }

        public event OnGSAModeChanged GSAModeChanged;
        protected virtual void OnGSAModeChanged(MTK3339 sender, GSAModes e)
        {
            if (GSAModeChanged != null)
                GSAModeChanged(sender, e);
        }

        public event OnRMCModeChanged RMCModeChanged;
        protected virtual void OnRMCModeChanged(MTK3339 sender, RMCModes e)
        {
            if (RMCModeChanged != null)
                RMCModeChanged(sender, e);
        }

        public event OnSatellitesInViewChanged SatellitesInViewChanged;
        protected virtual void OnSatellitesInViewChanged(MTK3339 sender, SatelliteInView[] e)
        {
            if (SatellitesInViewChanged != null)
                SatellitesInViewChanged(sender, e);
        }

        public event OnSatellitesUsedChanged SatellitesUsedChanged;
        protected virtual void OnSatellitesUsedChanged(MTK3339 sender, int count)
        {
            if (SatellitesUsedChanged != null)
                SatellitesUsedChanged(sender, count);
        }

        public event OnSpeedChanged SpeedChanged;
        protected virtual void OnSpeedChanged(MTK3339 sender, double knots, double mph)
        {
            if (SpeedChanged != null)
                SpeedChanged(sender, knots, mph);
        }

        public event OnVTGModeChanged VTGModeChanged;
        protected virtual void OnVTGModeChanged(MTK3339 sender, VTGModes e)
        {
            if (VTGModeChanged != null)
                VTGModeChanged(sender, e);
        }

        #endregion

        #region Properties

        public DateTime CurrentDateTime
        {
            get { return _currentDateTime; }
        }

        public int AgeOfDifferential
        {
            get { return _diffAge; }
        }

        public double Altitude
        {
            get { return _altitude; }
        }

        public int AltitudeInFeet
        {
            get { return (int)MetersToFeet(_altitude); }
        }

        public double Course
        {
            get { return _course; }
        }

        public bool DaylightSavingsTime
        {
            get { return _dstEnable; }
            set { _dstEnable = value; }
        }

        public bool FixAvailable
        {
            get { return _fixType != FixTypes.NoFix; }
        }

        public FixModes FixMode
        {
            get { return _fixMode; }
        }

        public FixTypes FixType
        {
            get { return _fixType; }
        }

        public GSAModes GSAMode
        {
            get { return _gsaMode; }
        }

        public int[] GSASatellites
        {
            get { return _gsaSatellites; }
        }

        public double GeoidalSeparation
        {
            get { return _geoSep; }
        }

        public double HorizontalDilution
        {
            get { return _HOD; }
        }

        public string Latitude
        {
            get { return _latitude; }
        }

        public string Longitude
        {
            get { return _longitude; }
        }

        public double MapLatitude
        {
            get { return _mapLatitude; }
        }

        public double MapLongitude
        {
            get { return _mapLongitude; }
        }

        public double PositionDilution
        {
            get { return _POD; }
        }

        public RMCModes RMCMode
        {
            get { return _rmcMode; }
        }

        public SatelliteInView[] SatellitesInView
        {
            get { return _satInView; }
        }

        public int SatellitesUsed
        {
            get { return _satsUsed; }
        }

        public double Speed
        {
            get { return _speed; }
        }

        public double SpeedInMPH
        {
            get { return _speed * 1.151; }
        }

        public SubscriptionLevels SubscriptionLevel
        {
            get { return _subLevel; }
            set
            {
                if (_subLevel == value)
                    return;
                _subLevel = value;
                SetSubscriptionLevel();
            }
        }

        public int TimeZoneOffset
        {
            get { return _timezoneOffset; }
            set { _timezoneOffset = value; }
        }

        public UpdateRates UpdateRate
        {
            get { return _rate; }
            set
            {
                if (_rate == value)
                    return;
                _rate = value;
                SetUpdateRate();
            }
        }

        public double VerticalDilution
        {
            get { return _VOD; }
        }

        public VTGModes VTGMode
        {
            get { return _vtgMode; }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reads Serial data on DataReceived event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //void serialPort_LineReceived(GTI.Serial sender, string line)
        void serialPort_LineReceived(string line)
        {
            if (currentProtocol == CommsProtocol.ASCII)
            {
                string[] data;
                try
                {
                    // Remove the CRLF
                    line = line.Substring(0, line.Length - 2);

                    // Remove Checksum if it exists
                    if (line.Substring(line.Length - 3, 1) == "*")
                        line = line.Substring(0, line.Length - 3);

                    // Split line
                    data = line.Split(',');

                    // Proccess command
                    switch (data[0])
                    {
                        case "$PMTK001": //Acknoledgement Packet
                            // Just echoing, do nothing
                            break;
                        case "$GPGGA":
                            ParseGGAData(data);
                            break;
                        case "$GPGSA":
                            ParseGSAData(data);
                            break;
                        case "$GPRMC":
                            ParseRMCData(data);
                            break;
                        case "$GPVTG":
                            ParseVTGData(data);
                            break;
                        case "$GPGSV":
                            ParseGSVData(data);
                            break;
                        case "$GPGLL": //Geographic Latitude and longitude (not currently decoded)
                            break;
                        //debug functions:
                        case "$PMTK011": //fall through!
                        case "$PMTK010": //System Message
                            if (Debugger.IsAttached) { Console.WriteLine("GPS Successfully initialised with state :" + data[1]); } //002 = awake
                            break;
                        case "$PMTK869":
                            if (Debugger.IsAttached) { Console.WriteLine("GPS Embedded Assist Enabled: " + data[2]); }
                            break;
                        case "$PMTK513":
                            if (Debugger.IsAttached) { Console.WriteLine("GPS SBAS Enabled: " + data[1]); }
                            break;
                        case "$PMTK527":
                            if (Debugger.IsAttached) { Console.WriteLine("GPS Speed Threshold: " + data[1]); }
                            break;
                        default:
                            if (Debugger.IsAttached) { Console.WriteLine("GPS Unknown Packet Received: " + line); }
                            break;
                    }
                }
                catch (Exception)
                {
                    if (Debugger.IsAttached) { Console.WriteLine("Parse error"); }
                    OnError(this, line);
                }
            }
            else
            {
                if (Debugger.IsAttached) { Console.WriteLine(line); }
            }
        }


        private void GeneralParseLatitude(string Value, string NSIndicator)
        {
            int pos = Value.IndexOf(".");
            double val;
            string tmp;

            _latitude = Value + NSIndicator;

            // Remove decimal point
            if (pos >= 0)
                Value = Value.Substring(0, pos) + Value.Substring(pos + 1);

            // Fix remainder
            tmp = (double.Parse(Value.Substring(2)) / 60).ToString();

            // Remove remainder decimal point
            pos = tmp.IndexOf(".");
            if (pos >= 0)
                tmp = tmp.Substring(0, pos) + tmp.Substring(pos + 1);

            // Join pieces
            val = double.Parse(Value.Substring(0, 2) + "." + tmp);

            // Put negative if South
            if (NSIndicator == "S")
                val = -val;

            _mapLatitude = val;
        }

        private void GeneralParseLongitude(string Value, string EWIndicator)
        {
            int pos = Value.IndexOf(".");
            double val;
            string tmp;

            _longitude = Value + EWIndicator;

            // Remove decimal point
            if (pos >= 0)
                Value = Value.Substring(0, pos) + Value.Substring(pos + 1);

            // Fix remainder
            tmp = (double.Parse(Value.Substring(3)) / 60).ToString();

            // Remove remainder decimal point
            pos = tmp.IndexOf(".");
            if (pos >= 0)
                tmp = tmp.Substring(0, pos) + tmp.Substring(pos + 1);

            // Join pieces
            val = double.Parse(Value.Substring(0, 3) + "." + tmp);

            // Put negative if South
            if (EWIndicator == "W")
                val = -val;

            _mapLongitude = val;
        }

        private double MetersToFeet(double Value)
        {
            return Value * 3.2808399;
        }

        private void ParseGGAData(string[] Data)
        {
            double lastLat = _mapLatitude;
            double lastLng = _mapLongitude;
            double dTmp;
            int iTmp;

            //Time
            double timeRawDouble = Double.Parse(Data[1]);
            int timeRaw = (int)timeRawDouble;
            int hours = timeRaw / 10000;
            int minutes = (timeRaw / 100) % 100;
            int seconds = timeRaw % 100;
            int milliseconds = (int)((timeRawDouble - timeRaw) * 1000.0);

            DateTime tmpDate = new DateTime(_currentDateTime.Year, _currentDateTime.Month, _currentDateTime.Day);
            _currentDateTime = tmpDate.Add(new TimeSpan(hours, minutes, seconds));

            // Latitude
            if (Data[2] != string.Empty && Data[3] != string.Empty)
                GeneralParseLatitude(Data[2], Data[3]);

            // Longitude
            if (Data[4] != string.Empty && Data[5] != string.Empty)
                GeneralParseLongitude(Data[4], Data[5]);

            // Fix Indicator
            if (Data[6] != string.Empty)
            {
                if (_fixType != (FixTypes)int.Parse(Data[6]))
                {
                    _fixType = (FixTypes)int.Parse(Data[6]);
                    OnFixTypeChanged(this, _fixType);
                }
            }

            // Satellites Used
            if (Data[7] != string.Empty)
            {
                iTmp = int.Parse(Data[7]);
                if (_satsUsed != iTmp)
                {
                    _satsUsed = iTmp;
                    OnSatellitesUsedChanged(this, _satsUsed);
                }
            }

            // Horizontal Dilution of Precision (in meters)
            if (Data[8] != string.Empty)
                _HOD = double.Parse(Data[8]);

            // MSL Altitude (in meters)
            if (Data[9] != string.Empty)
            {
                dTmp = double.Parse(Data[9]);
                if (_altitude != dTmp)
                {
                    _altitude = dTmp;
                    OnAltitudeChanged(this, _altitude, (int)MetersToFeet(_altitude));
                }
            }

            // Item 10 is ALWAYS M for Meters; so skip it.

            // Geoidal Separation
            if (Data[11] != string.Empty)
                _geoSep = double.Parse(Data[11]);

            // Item 12 is ALWAYS M for Meters; so skip it.

            // Age of Differential Correction (in seconds)
            if (Data[13] != string.Empty)
                _diffAge = int.Parse(Data[13]);

            // Raise Events
            if (_fixType != FixTypes.NoFix && (lastLat != _mapLatitude || lastLng != _mapLongitude))
                OnCoordinatesUpdated(this);
        }

        private void ParseGSAData(string[] Data)
        {
            int SatCount = 0;
            int i;

            // Fix selection Manual/Automatic
            if (Data[1] == string.Empty || Data[1] == "M")
            {
                if (_fixMode != FixModes.Manual)
                {
                    _fixMode = FixModes.Manual;
                    OnFixModeChanged(this, _fixMode);
                }
            }
            else
            {
                if (_fixMode != FixModes.Automatic)
                {
                    _fixMode = FixModes.Automatic;
                    OnFixModeChanged(this, _fixMode);
                }
            }

            // GSAMode
            if (Data[2] == string.Empty || Data[2] == "1")
            {
                if (_gsaMode != GSAModes.NoFix)
                {
                    _gsaMode = GSAModes.NoFix;
                    OnGSAModeChanged(this, _gsaMode);
                }
            }
            else if (Data[2] == "2")
            {
                if (_gsaMode != GSAModes.Fix2D)
                {
                    _gsaMode = GSAModes.Fix2D;
                    OnGSAModeChanged(this, _gsaMode);
                }
            }
            else
            {
                if (_gsaMode != GSAModes.Fix3D)
                {
                    _gsaMode = GSAModes.Fix3D;
                    OnGSAModeChanged(this, _gsaMode);
                }
            }

            // Satellites used
            for (i = 3; i < 15; i++)
            {
                if (Data[i] != string.Empty)
                    SatCount += 1;
                else
                    break;
            }

            // Move to array
            _gsaSatellites = new int[SatCount];
            for (i = 0; i < SatCount; i++)
                _gsaSatellites[i] = int.Parse(Data[i + 3]);

            // Position Dilution of Precision
            if (Data[15] != string.Empty)
                _POD = double.Parse(Data[15]);

            // Horizontal Dilution of Precision
            if (Data[16] != string.Empty)
                _HOD = double.Parse(Data[16]);

            // Vertical Dilution of Precision
            if (Data[17] != string.Empty)
                _VOD = double.Parse(Data[17]);
        }

        private void ParseGSVData(string[] Data)
        {
            // Total number of sentences (1-3)
            if (Data[1] == string.Empty || Data[2] == string.Empty)
                return;
            _svCount = int.Parse(Data[1]);

            // Current sentence
            int msgNumber = int.Parse(Data[2]);

            // Satellites in view
            if (msgNumber == 1)
                _satInViewTmp = new SatelliteInView[int.Parse(Data[3])];

            if (_satInViewTmp.Length != 0) //we may not have started at the first message
            {
                // Get Array Index
                int iIndex = (msgNumber - 1) * 4;

                for (int i = 4; i <= Data.Length - 1; i = i + 4)
                {
                    // Sat Data
                    if (Data[i] != string.Empty)
                    {
                        double snr = double.NaN; //if a sat isnt tracked the value will be empty
                        if (Data[i + 3] != string.Empty)
                            double.Parse(Data[i + 3]);
                        if (Data[i + 1] != string.Empty && Data[i + 2] != string.Empty)
                            _satInViewTmp[iIndex++] = new SatelliteInView(int.Parse(Data[i]), int.Parse(Data[i + 1]), int.Parse(Data[i + 2]), snr);
                    }
                }

            }
            // Raise event
            if (msgNumber == _svCount)
            {
                _satInView = _satInViewTmp;
                OnSatellitesInViewChanged(this, _satInView);
            }
        }

        private void ParseRMCData(string[] Data)
        {
            // Check Status [2]
            // Don't bother parsing if void as it is probably invalid data
            if (Data[2] == "A")
            {


                double lastLat = _mapLatitude;
                double lastLng = _mapLongitude;
                double dTmp;

                //Get DateTime
                if (Data[1] != string.Empty && Data[9] != string.Empty)
                {
                    double timeRawDouble = Double.Parse(Data[1]);
                    int timeRaw = (int)timeRawDouble;
                    int hours = timeRaw / 10000;
                    int minutes = (timeRaw / 100) % 100;
                    int seconds = timeRaw % 100;
                    int milliseconds = (int)((timeRawDouble - timeRaw) * 1000.0);

                    int dateRaw = Int32.Parse(Data[9]);
                    int days = dateRaw / 10000;
                    int months = (dateRaw / 100) % 100;
                    int years = 2000 + (dateRaw % 100);

                    _currentDateTime = new DateTime(years, months, days, hours, minutes, seconds);
                }

                // Latitude
                if (Data[3] != string.Empty && Data[4] != string.Empty)
                    GeneralParseLatitude(Data[3], Data[4]);

                // Longitude
                if (Data[5] != string.Empty && Data[6] != string.Empty)
                    GeneralParseLongitude(Data[5], Data[6]);

                // Raise Lat/Lng Event
                if (lastLat != _mapLatitude || lastLng != _mapLongitude)
                    OnCoordinatesUpdated(this);

                // Speed (in Knots)
                if (Data[7] != string.Empty)
                {
                    dTmp = double.Parse(Data[7]);
                    if (_speed != dTmp)
                    {
                        _speed = dTmp;
                        OnSpeedChanged(this, _speed, _speed * 1.151);
                    }
                }

                // Course (in degrees)
                if (Data[8] != string.Empty)
                {
                    dTmp = double.Parse(Data[8]);
                    if (_course != dTmp)
                    {
                        _course = dTmp;
                        OnCourseChanged(this, _course);
                    }
                }

                // Next 2 messages are only available w/ GlobalTop Customization Service
                // So ignore them

                // Mode
                if (Data[12] != string.Empty)
                {
                    switch (Data[12])
                    {
                        case "D":
                            if (_rmcMode != RMCModes.Differential)
                            {
                                _rmcMode = RMCModes.Differential;
                                OnRMCModeChanged(this, _rmcMode);
                            }
                            break;
                        case "E":
                            if (_rmcMode != RMCModes.Estimated)
                            {
                                _rmcMode = RMCModes.Estimated;
                                OnRMCModeChanged(this, _rmcMode);
                            }
                            break;
                        default:
                            if (_rmcMode != RMCModes.Autonomous)
                            {
                                _rmcMode = RMCModes.Autonomous;
                                OnRMCModeChanged(this, _rmcMode);
                            }
                            break;
                    }
                }

                //if (tokens[2] != "A")
                //{
                //    this.OnInvalidPositionReceived(this, null);

                //    return;
                //}

                //double timeRawDouble = Double.Parse(tokens[1]);

                //int timeRaw = (int)timeRawDouble;
                //int hours = timeRaw / 10000;
                //int minutes = (timeRaw / 100) % 100;
                //int seconds = timeRaw % 100;
                //int milliseconds = (int)((timeRawDouble - timeRaw) * 1000.0);
                //int dateRaw = Int32.Parse(tokens[9]);
                //int days = dateRaw / 10000;
                //int months = (dateRaw / 100) % 100;
                //int years = 2000 + (dateRaw % 100);

                //Position position = new Position();

                //position.FixTimeUtc = new DateTime(years, months, days, hours, minutes, seconds, milliseconds);
                //position.LatitudeString = tokens[3] + " " + tokens[4];
                //position.LongitudeString = tokens[5] + " " + tokens[6];

                //double latitudeRaw = double.Parse(tokens[3]);
                //int latitudeDegreesRaw = ((int)latitudeRaw) / 100;
                //double latitudeMinutesRaw = latitudeRaw - (latitudeDegreesRaw * 100);
                //position.Latitude = latitudeDegreesRaw + (latitudeMinutesRaw / 60.0);

                //if (tokens[4] == "S")
                //    position.Latitude = -position.Latitude;

                //double longitudeRaw = double.Parse(tokens[5]);
                //int longitudeDegreesRaw = ((int)longitudeRaw) / 100;
                //double longitudeMinutesRaw = longitudeRaw - (longitudeDegreesRaw * 100);
                //position.Longitude = longitudeDegreesRaw + (longitudeMinutesRaw / 60.0);

                //if (tokens[6] == "W")
                //    position.Longitude = -position.Longitude;

                //position.SpeedKnots = 0;
                //if (tokens[7] != "")
                //    position.SpeedKnots = Double.Parse(tokens[7]);

                //position.CourseDegrees = 0;
                //if (tokens[8] != "")
                //    position.CourseDegrees = Double.Parse(tokens[8]);

                //this.lastPositionReceived = GT.Timer.GetMachineTime();
                //this.LastPosition = position;
                //this.OnPositionReceived(this, position);
            }
        }

        private void ParseVTGData(string[] Data)
        {
            double dTmp;

            // Course (in degrees)
            if (Data[1] != string.Empty)
            {
                dTmp = double.Parse(Data[1]);
                if (_course != dTmp)
                {
                    _course = dTmp;
                    OnCourseChanged(this, _course);
                }
            }

            // Ignore next field (reference, always T)

            // Ignore next 2 fields (needs GlobalTop Customization Service)

            // Speed (in Knots)
            if (Data[5] != string.Empty)
            {
                dTmp = double.Parse(Data[5]);
                if (_speed != dTmp)
                {
                    _speed = dTmp;
                    OnSpeedChanged(this, _speed, _speed * 1.151);
                }
            }

            // Ignore Next field (reference, always N)

            // Ignore next 2 fields (same speed but in km/hr)

            // VTG Mode
            switch (Data[9])
            {
                case "D":   // Differential
                    if (_vtgMode != VTGModes.Differential)
                    {
                        _vtgMode = VTGModes.Differential;
                        OnVTGModeChanged(this, _vtgMode);
                    }
                    break;
                case "E":   // Estimated
                    if (_vtgMode != VTGModes.Estimated)
                    {
                        _vtgMode = VTGModes.Estimated;
                        OnVTGModeChanged(this, _vtgMode);
                    }
                    break;
                default:    // Autonomous
                    if (_vtgMode != VTGModes.Autonomous)
                    {
                        _vtgMode = VTGModes.Autonomous;
                        OnVTGModeChanged(this, _vtgMode);
                    }
                    break;
            }

        }

        /// <summary>
        /// Set the GPS subscription level
        /// </summary>
        private void SetSubscriptionLevel()
        {
            byte[] b;

            switch (_subLevel)
            {
                case SubscriptionLevels.RMCGGA:
                    b = UTF8Encoding.UTF8.GetBytes(SUBSCRIBE_RMCGGA);
                    break;
                case SubscriptionLevels.RMCOnly:
                    b = UTF8Encoding.UTF8.GetBytes(SUBSCRIBE_RMCONLY);
                    break;
                case SubscriptionLevels.OutputOff:
                    b = UTF8Encoding.UTF8.GetBytes(SUBSCRIBE_OUTOFF);
                    break;
                default: // All Data
                    b = UTF8Encoding.UTF8.GetBytes(SUBSCRIBE_ALLDATA);
                    break;
            }

            outputDataWriter.WriteBytes(b);
        }

        /// <summary>
        /// Sets the Update Rate (1, 5 or 10Hz) of the GPS
        /// </summary>
        private void SetUpdateRate()
        {
            byte[] b;

            switch (_rate)
            {
                case UpdateRates.GPS_10HZ:
                    b = UTF8Encoding.UTF8.GetBytes(UPDATE_RATE_10HZ);
                    break;
                case UpdateRates.GPS_5HZ:
                    b = UTF8Encoding.UTF8.GetBytes(UPDATE_RATE_5HZ);
                    break;
                default:    // 1HZ
                    b = UTF8Encoding.UTF8.GetBytes(UPDATE_RATE_1HZ);
                    break;
            }

            outputDataWriter.WriteBytes(b);
        }

        /// <summary>
        /// Sets the Update Rate (1, 5 or 10Hz) of the GPS
        /// </summary>
        public void SetStartType(StartupMode mode)
        {
            byte[] b;

            switch (mode)
            {
                case StartupMode.Hot:
                    b = UTF8Encoding.UTF8.GetBytes(START_HOT);
                    break;
                case StartupMode.Warm:
                    b = UTF8Encoding.UTF8.GetBytes(START_WARM);
                    break;
                case StartupMode.FactoryReset:
                    b = UTF8Encoding.UTF8.GetBytes(START_FACTORY_RESET);
                    break;
                default:    // 1HZ
                    b = UTF8Encoding.UTF8.GetBytes(START_COLD);
                    break;
            }

            outputDataWriter.WriteBytes(b);
        }

        #endregion

        #region EPO
        byte[] epoPacket = new byte[191];
        int epoCount = 0;
        bool finalEpoPacketRequired = false;
        bool flagStop = false;
        int totEPOcnt = 0;
        byte[] epoFileData;
        int failCount = 0;
        int SatSetSize = 60;
        int currentFileDataIndex = 0;

        enum CommsProtocol
        {
            Binary,
            ASCII
        }

        CommsProtocol currentProtocol = CommsProtocol.ASCII;

        public void UpdateEpoData(byte[] fileData) //TODO: try and use https://github.com/f5eng/mt3339-utils/blob/gps/epoloader as this fails.
        {
            this.failCount = 0;
            this.epoCount = 0;
            this.epoFileData = fileData;
            //this.flagAGPS = false;
            this.finalEpoPacketRequired = false;
            this.flagStop = false;
            //this.totEPOcnt = epoFileData.Length / SatSetSize; // = 32

            if (this.epoFileData.Length % 1920 != 0)
            {
                if (Debugger.IsAttached) { Console.WriteLine("EPO File is corrupt"); }
            }
            else
            {
                var b = UTF8Encoding.UTF8.GetBytes(ENTER_BINARY_MODE);
                outputDataWriter.WriteBytes(b); // GPS chipset from nmea- to bin-mode at 57600bps
                Thread.Sleep(500);
                //serialPort.Close();

                currentProtocol = CommsProtocol.Binary;

                serialPort.BaudRate = 57600;
                //serialPort.Open();

                this.SendEpoPacket();
                if (Debugger.IsAttached) { Console.WriteLine("Starting to send epo packets"); }
            }

        }

        private void SendEpoPacket()
        {
            if (this.flagStop)
            {
                if (Debugger.IsAttached) { Console.WriteLine("finished EPO file setup, going back to nema mode"); }
                this.finalEpoPacketRequired = false;
                this.flagStop = false;
                this.SetModeNMEA(9600);

                Thread.Sleep(1000);
                outputDataWriter.WriteString("$PMTK607*33\r\n");

                return;
            }

            if (this.finalEpoPacketRequired)
            {
                if (Debugger.IsAttached) { Console.WriteLine("sending the final epo packet"); }
                this.flagStop = true;
                this.epoCount = 65535; //0xffff

                this.GenerateEpoPacket(0, true);
                outputDataWriter.WriteBytes(this.epoPacket);

                return;
            }


            if (this.epoCount < 10)
            {
                if (Debugger.IsAttached) { Console.WriteLine("sending an epo packet"); }

                this.GenerateEpoPacket(180);
                outputDataWriter.WriteBytes(this.epoPacket);

                this.epoCount++;
            }
            else //generate second to last packet
            {
                int num = epoFileData.Length - currentFileDataIndex; //should be 120?!
                if (Debugger.IsAttached) { Console.WriteLine("got " + num + ". It should be 120... but is it?"); }
                if (num % (SatSetSize * 2) == 0) //is this check actually needed?
                {
                    this.GenerateEpoPacket(num);
                    outputDataWriter.WriteBytes(this.epoPacket);


                    this.epoCount++;
                }
                this.finalEpoPacketRequired = true;
            }

        }

        private void GenerateEpoPacket(int epoMaxCount, bool isFinalPacket = false)
        {
            epoPacket = new byte[191];
            this.epoPacket[0] = 0x04; this.epoPacket[1] = 0X24;             //Preamble
            this.epoPacket[2] = 0xBF; this.epoPacket[3] = 0x00;             //Length
            this.epoPacket[4] = 0xD2; this.epoPacket[5] = 0x02;             //Command: EPO Packet

            this.epoPacket[6] = (byte)(this.epoCount & 0xFF);               //Message ID part 1
            this.epoPacket[7] = (byte)((this.epoCount >> 8) & 0xFF);        //Message ID part 2

            if (!isFinalPacket)
            {
                int epoPacketIndex = 8;

                for (int j = currentFileDataIndex; j <= (epoMaxCount + currentFileDataIndex) - 1; j++)
                {
                    this.epoPacket[epoPacketIndex] = epoFileData[j];
                    epoPacketIndex++;
                    currentFileDataIndex++;
                }
            }
            else
            {
                this.finalEpoPacketRequired = true;
            }

            this.epoPacket[188] = this.GenerateChecksum(this.epoPacket);    //Checksum
            this.epoPacket[189] = 0x0D; this.epoPacket[190] = 0x0A;         //End word
        }


        private void SetModeNMEA(uint baudRate)
        {
            byte[] NemaPacket = new byte[14];
            NemaPacket[0] = 0x04; NemaPacket[1] = 0x24;                    // Preamble
            NemaPacket[2] = 0x0E; NemaPacket[3] = 0x00;                    // Length
            NemaPacket[4] = 0xFD; NemaPacket[5] = 0x00;                    // Command: Change UART packet protocol
            NemaPacket[6] = 0x00;                                          // PMTK protocol
            NemaPacket[7] = (byte)(baudRate & 0xFF);                       // Set UART baudrate (4 bytes)
            NemaPacket[8] = (byte)((baudRate >> 8) & 0xFF);
            NemaPacket[9] = (byte)((baudRate >> 16) & 0xFF);
            NemaPacket[10] = (byte)((baudRate >> 24) & 0xFF);
            NemaPacket[11] = GenerateChecksum(NemaPacket);                  // Checksum
            NemaPacket[12] = 0x0D; NemaPacket[13] = 0x0A;                   // End word

            outputDataWriter.WriteBytes(NemaPacket);
            //Thread.Sleep(500);
            //serialPort.Close();
            serialDevice.BaudRate = baudRate;
            currentProtocol = CommsProtocol.ASCII;
            //serialDevice.Open();
            if (Debugger.IsAttached) { Console.WriteLine("Set to NEMA mode"); }
        }

        byte GenerateChecksum(byte[] packet)
        {
            var xorByte = packet[2];                                        //Avoid the preamble
            int endIndex = packet.Length - 3;                               //Avoid the End word
            for (int i = 3; i <= endIndex; i++)
            {
                xorByte ^= packet[i];
            }
            return xorByte;
        }

        bool readInProgress = false;
        void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!readInProgress)
                {
                    readInProgress = true;

                    SerialDevice serDev = (SerialDevice)sender;

                    // Read the bytes and append to result
                    using (DataReader inputDataReader = new DataReader(serDev.InputStream))
                    {
                        inputDataReader.InputStreamOptions = InputStreamOptions.Partial;

                        uint bytesRead = inputDataReader.Load(serDev.BytesToRead);

                        if (bytesRead > 0)
                        {
                            byte[] receivedBytes = new byte[100];
                            int i = 0;
                            if (currentProtocol == CommsProtocol.Binary)
                            {
                                receivedBytes[i] = inputDataReader.ReadByte();
                                i++;
                            }
                            if (receivedBytes[0] == 0x04 | currentProtocol == CommsProtocol.ASCII)
                            {
                                receivedBytes[i] = inputDataReader.ReadByte();
                                if (receivedBytes[i] == 0x24)
                                {
                                    //Console.WriteLine("got packet start");
                                    i++;
                                    while (true)
                                    {
                                        //read until the endword is received
                                        receivedBytes[i] = inputDataReader.ReadByte();
                                        if (receivedBytes[i] == 0x0A)
                                        {
                                            if (currentProtocol == CommsProtocol.Binary)
                                            {
                                                //Console.WriteLine("got packet end");
                                                ParseACK(receivedBytes);
                                            }
                                            else
                                            {
                                                serialPort_LineReceived(new string(UTF8Encoding.UTF8.GetChars(receivedBytes)));
                                            }
                                            break;
                                        }
                                        i++;
                                    }
                                }
                            }
                        }

                    }
                    readInProgress = false;
                }
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached) { Console.WriteLine("binary mode: error receiving bytes, " + ex); }
            }
        }

        private void ParseACK(byte[] receivedBytes)
        {
            try
            {
                bool success = false;
                if ((receivedBytes[0] == (byte)0x04) & (receivedBytes[1] == (byte)0x24) &                // Preamble (4, 36)
                    (receivedBytes[2] == (byte)0x0C) & (receivedBytes[3] == (byte)0x00) &                // Response Length (12, 0)
                    (receivedBytes[4] == (byte)0x02) & (receivedBytes[5] == (byte)0x00) &                // Command ID (2, 0)
                    (receivedBytes[6] == (byte)(this.epoCount - 1 & 0xff) & (receivedBytes[7] == (byte)(this.epoCount - 1 >> 8))))// && ((millis()-startTime)<timeOut))  // LSB-MSB sequence
                {
                    if (receivedBytes[8] != 0x01) //the sent epo data was unsuccessful
                    {
                        this.SetModeNMEA(9600);
                        if (Debugger.IsAttached) { Console.WriteLine("EPO File is corrupt at packet" + (epoCount - 1) + ", exiting"); }
                        success = true;
                    }
                    else
                    {
                        if (Debugger.IsAttached) { Console.WriteLine("EPO packet successful, sending the next one"); }
                        this.SendEpoPacket();
                    }
                }
                if (!success)
                {
                    if (Debugger.IsAttached) { Console.WriteLine("Packet incorrect, ignoring"); }

                    this.failCount++;
                }
                if (this.failCount >= 20)
                {
                    this.SetModeNMEA(9600);
                    if (Debugger.IsAttached) { Console.WriteLine("EPO File upload has failed too many times, exiting."); }
                }

            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached) { Console.WriteLine(ex.ToString()); }
                this.SetModeNMEA(9600);
            }


        }

        #endregion

    }

}
