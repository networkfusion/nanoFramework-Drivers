
//
// Copyright (c) 2020 The nanoFramework project contributors
// Portions Copyright (c) 2020 Robin Jones (NetworkFusion).  All rights reserved.
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Drivers.Spi.MAX31865
{

using System;

    using System.Threading;
    using Windows.Devices.Spi;
    using Windows.Devices.Gpio;

    /// <summary>
    /// A MAX31865 driver for nanoFramework
    /// </summary>
    public class MAX31865
    {
        private bool _initialized;
        private GpioPin _irqPin;
        private SpiDevice _spiDevice;
        private Timer FaultScanner;
        private byte _config;

        public delegate void FaultEventHandler(MAX31865 sender, byte DataByte);
        public event FaultEventHandler FaultEvent;
        public delegate void DataReadyEventHandler(MAX31865 sender, float Data);
        public event DataReadyEventHandler DataReadyFahrenheitEvent;
        public event DataReadyEventHandler DataReadyCelsiusEvent;

        enum Command
        {
            READ = 0x00,
            WRITE = 0x80
        }

        ///<Summary>
        /// Config Bits
        ///</Summary>
        public enum ConfigValues
        {
            //D7
            VBIAS_ON = 0x80,
            VBIAS_OFF = 0x00,
            //D6
            CONV_MODE_AUTO = 0x40,
            CONV_MODE_OFF = 0x00,
            //D5
            ONE_SHOT_ON = 0x20,
            ONE_SHOT_OFF = 0x00,
            //D4
            THREE_WIRE = 0x10,
            TWO_WIRE = 0x00,
            FOUR_WIRE = 0x00,
            //D2&3
            FAULT_DETECT_FINISH_WITH_MANUAL_DELAY = 0x0C,
            FAULT_DETECT_RUN_WITH_MANUAL_DELAY = 0x08,
            FAULT_DETECT_WITH_AUTO_DELAY = 0x04,
            //D1
            FAULT_CLEAR_STATE = 0x02,
            //D0
            FILTER_50Hz = 0x01,
            FILTER_60Hz = 0x00
        }

        public enum ConfigSettings
        {
            VBIAS = 0x80,
            CONV_MODE = 0x40,
            ONE_SHOT = 0x20,
            WIRE_TYPE = 0x10,
            FLT_DETECT = 0x0C,
            FAULT_CLR = 0x02,
            FILTER = 0x01
        }

        enum FaultBits
        {
            RTD_HI_THRESH = 0x80,
            RTD_LO_THRESH = 0x40,
            REF_IN_HI = 0x20,
            FORCE_OPEN_REFIN = 0x10,
            FORCE_OPEN_RTDIN = 0x08,
            UNDERVOLT = 0x04
        }

        public enum Register
        {
            CONFIG = 0x00,
            RTD_MSB = 0x01,
            RTD_LSB = 0x02,
            HI_FLT_THRESH_MSB = 0x03,
            HI_FLT_THRESH_LSB = 0x04,
            LO_FLT_THRESH_MSB = 0x05,
            LO_FLT_THRESH_LSB = 0x06,
            FLT_STATUS = 0x07
        }

        public enum SensorType : int
        {
            PT100 = 100,
            PT1000 = 1000
        }

        public float ReferenceReistor { get; private set; }
        public SensorType Sensor { get; private set; }


        /// <summary>
        /// Opens the SPI connection and control pin
        /// </summary>
        /// <param name="spiBus">The SPI bus</param>
        /// <param name="csPin">Chip Enable(CE)/Select(CS) pin</param>
        public MAX31865(string spiBus, int csPin)
        {
            // Chip Select : Active Low
            // Clock : Active High, Data clocked in on rising edge
            var connectionSettings = new SpiConnectionSettings(csPin);
            connectionSettings.DataBitLength = 8;
            connectionSettings.ClockFrequency = 4 * 1000 * 1000; //- max 5MHz
            connectionSettings.BitOrder = DataBitOrder.MSB;
            connectionSettings.Mode = SpiMode.Mode1; //supports 1 and 3
            connectionSettings.SharingMode = SpiSharingMode.Shared;

            // create SPI device for Max31865
            _spiDevice = SpiDevice.FromId(spiBus, connectionSettings);

        }


        /// <summary>
        ///   Initializes the driver according to your sensor</param>
        ///   <param name="irqPin"> IRQ (Data Ready) pin as a Socket.Pin</param>
        ///   <param name="config"> The configuration of the sensor</param>
        ///   <param name="referenceResistor">The reference resistor value (e.g. 400ohm for PT100, 4000ohm for PT1000)</param>
        ///   <param name="sensor">The type of RTD used</param>
        /// </summary>
        public void Initialize(int irqPin, byte config, float referenceResistor = 400, SensorType sensor = SensorType.PT100)
        {

            _irqPin = GpioController.GetDefault().OpenPin(irqPin);
            _irqPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            _irqPin.ValueChanged += _irqPin_ValueChanged;


            _initialized = true;

            _config = config;

            ResetConfig();

            ReferenceReistor = referenceResistor;
            Sensor = sensor;
        }


        private void _irqPin_ValueChanged(object sender, GpioPinValueChangedEventArgs e)
        {
            if (DataReadyFahrenheitEvent != null)
                DataReadyFahrenheitEvent(this, GetTemperatureFahrenheit());
            if (DataReadyCelsiusEvent != null)
                DataReadyCelsiusEvent(this, GetTemperatureCelsius());
        }

        /// <summary>
        /// Reset config back to original value
        /// </summary>
        public void ResetConfig()
        {
            Console.WriteLine("Reset Config: From:" + GetRegister(0x00).ToString("X") + " To:" + _config.ToString("X"));

            SetRegister(0x00, _config);
        }

        /// <summary>
        /// Put system into manual "Normally Off" Mode
        /// Requires a One Shot command to get temperature
        /// </summary>
        public void SetConvToManual()
        {
            byte OldValue = (byte)GetRegister(0x00);
            byte NewValue = (byte)((~(byte)ConfigSettings.CONV_MODE & OldValue) | (byte)ConfigValues.CONV_MODE_OFF);

            Console.WriteLine("Set Manual: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));
  
            SetRegister(0x00, (byte)NewValue);
        }

        /// <summary>
        /// Reads temperature once
        /// </summary>
        public bool ExecuteOneShot()
        {
            //Make sure we are not running a fault scan
            if ((GetRegister(0x00) & 0x0C) == 0)
            {
                byte OldValue = (byte)GetRegister(0x00);
                byte NewValue = (byte)((byte)ConfigSettings.ONE_SHOT | OldValue);

                Console.WriteLine("One Shot: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));

                SetRegister(0x00, (byte)NewValue);
                return true;
            }
            return false;

        }

        /// <summary>
        /// Put system into auto "Normally On" Mode
        /// Will read temperature at the filter frequency
        /// </summary>
        public bool SetConvToAuto()
        {
            //Make sure we are not running a fault scan
            if ((GetRegister(0x00) & 0x0C) == 0)
            {
                byte OldValue = (byte)GetRegister(0x00);
                byte NewValue = (byte)((~(byte)ConfigSettings.CONV_MODE & OldValue) | (byte)ConfigValues.CONV_MODE_AUTO | (byte)ConfigValues.VBIAS_ON);

                Console.WriteLine("Set Auto: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));

                SetRegister(0x00, (byte)NewValue);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clear faults
        /// After reset, it sets the mode back to previous state (Auto/Manual)
        /// </summary>
        public void ClearFaults()
        {
            byte OldValue = GetRegister(0x00);
            byte NewValue = (byte)((OldValue & 0xD3) | 0x02); //Everything by D5,D3 and D2...plus the falut clear bit

            Console.WriteLine("Clear Faults: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));

            SetRegister(0x00, NewValue);
            if ((OldValue & 0x40) > 0) SetConvToAuto();
        }

        /// <summary>
        /// Run an automatic fault scan
        /// </summary>
        public void RunAutoFltScan()
        {
            byte OldValue = GetRegister(0x00);
            //Write 100x010x by keeping existing values for ...x...x and adding 0x84
            byte NewValue = (byte)((OldValue & 0x11) | 0x84); //Everything by D5,D3 and D2...plus the falut clear bit

            Console.WriteLine("Run Fault Scan: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));

            SetRegister(0x00, NewValue);
            while ((GetRegister(0x00) & 0x0C) > 0) ;
            byte FaultByte = GetRegister((byte)Register.FLT_STATUS);
            if (FaultByte > 0)
                if (FaultEvent != null)
                    FaultEvent(this, FaultByte);
        }


        void FaultScanner_Callback(object state)
        {
            RunAutoFltScan();
        }

        private void CheckIsInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Initialize method needs to be called before this call");
            }
        }

        public void WriteConfigBit(MAX31865.ConfigSettings Setting, MAX31865.ConfigValues Value)
        {
            byte OldValue = (byte)GetRegister(0x00);
            byte NewValue = (byte)((~(byte)Setting & OldValue) | (byte)Value);

            Console.WriteLine("Set Config Bit: Old:" + OldValue.ToString("X") + " New:" + NewValue.ToString("X"));

            SetRegister(0x00, (byte)NewValue);
        }

        public void EnableFaultScanner(int interval)
        {
            FaultScanner = new Timer(FaultScanner_Callback, null, interval, interval);
        }

        public void DisableFaultScanner()
        {
            FaultScanner.Change(Timeout.Infinite, Timeout.Infinite);
            //FaultScanner.Dispose(); //will not calling this allow it to be recreated??? or is it just using resources???
        }

        private int GetADCRegisterData()
        {
            //Shift MSB to the left 8 bits)
            int RTDVala = (int)(GetRegister(0x01) << 8);
            int RTDValb = (int)(GetRegister(0x02));
            if ((GetRegister(0x02) & 0x01) > 0)
                if (FaultEvent != null)
                    FaultEvent(this, GetRegister((byte)Register.FLT_STATUS));
            //FaultEvent(this, );
            //Merge bytes
            return (RTDVala | RTDValb);
        }

        public float GetResistance()
        {
            const float x = 32768; //System.Math.Pow(2, 15); = 32768 so we will use the constant for speed!
            var adcValue = (GetADCRegisterData() >> 1); //shift the value by 1 bit as the lsb is an error flag
            return ((adcValue * ReferenceReistor) / x); //MAX31865 has a 15bit resolution
        }

        public float GetTemperatureCelsius()
        {
            const float RTD_ALPHA = 3.90802e-3F; //ITS90 = 3.9080e-3
            const float RTD_BETA = -5.802e-7F; //ITS90 = -5.870
            const float a2 = 2.0F * RTD_BETA;
            const float bSq = RTD_ALPHA * RTD_ALPHA;

            float c = 1.0F - GetResistance() / (int)Sensor; //100 for PT100, 1000 for PT1000
            float d = bSq - 2.0F * a2 * c;
            return (-RTD_ALPHA + System.Math.Sqrt(d)) / a2;
        }

        public float GetTemperatureFahrenheit()
        {
            //Convert C to F
            return (GetTemperatureCelsius() * (9 / 5)) + 32;
        }

        /// <summary>
        ///   Executes a command
        /// </summary>
        /// <param name = "command">Command</param>
        /// <param name = "address">Register to write to</param>
        /// <param name = "data">Data to write</param>
        /// <returns>Response byte array. First byte is the status register</returns>
        public byte[] WriteBlock(byte command, byte address, byte[] data)
        {
            CheckIsInitialized();

            // Create SPI Buffers with Size of Data + 1 (For Command)
            var writeBuffer = new byte[data.Length + 1];
            var readBuffer = new byte[data.Length + 1];

            // Add command and address to SPI buffer
            writeBuffer[0] = (byte)(command | address);

            // Add data to SPI buffer
            Array.Copy(data, 0, writeBuffer, 1, data.Length);

            // Do SPI Read/Write
            _spiDevice.TransferFullDuplex(writeBuffer, readBuffer);

            // Return ReadBuffer
            return readBuffer;
        }

        /// <summary>
        ///   Write an entire Register
        /// </summary>
        /// <param name = "register">Register to write to</param>
        /// <param name = "value">Value to be set</param>
        /// <returns>Response byte. Register value after write</returns>
        public byte SetRegister(byte register, byte value)
        {
            CheckIsInitialized();
            Execute((byte)Command.WRITE, register, new byte[] { value });
            return value;// GetRegister(register);
        }

        /// <summary>
        ///   Get an entire Register
        /// </summary>
        /// <param name = "register">Register to read</param>
        /// <returns>Response byte. Register value</returns>
        public byte GetRegister(byte register)
        {
            CheckIsInitialized();
            var read = Execute((byte)Command.READ, register, new byte[1]);
            var result = new byte[read.Length - 1];
            Array.Copy(read, 1, result, 0, result.Length);
            return read[1];
        }


        /// <summary>
        ///   Executes a command (for details see module datasheet)
        /// </summary>
        /// <param name = "command">Command</param>
        /// <param name = "address">Register to write to</param>
        /// <param name = "data">Data to write</param>
        /// <returns>Response byte array. First byte is the status register</returns>
        public byte[] Execute(byte command, byte address, byte[] data)
        {
            CheckIsInitialized();

            // Create SPI Buffers with Size of Data + 1 (For Command)
            var writeBuffer = new byte[data.Length + 1];
            var readBuffer = new byte[data.Length + 1];

            // Add command and address to SPI buffer
            writeBuffer[0] = (byte)(command | address);

            // Add data to SPI buffer
            Array.Copy(data, 0, writeBuffer, 1, data.Length);

            // Do SPI Read/Write
            _spiDevice.TransferFullDuplex(writeBuffer, readBuffer);

            // Return ReadBuffer
            return readBuffer;
        }
    }

}
