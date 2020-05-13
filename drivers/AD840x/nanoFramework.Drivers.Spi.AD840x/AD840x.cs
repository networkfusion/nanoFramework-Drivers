//
// Copyright (c) 2020 The nanoFramework project contributors
// Portions Copyright (c) 2020 Robin Jones (NetworkFusion).  All rights reserved.
// See LICENSE file in the project root for full license information.
//

using Windows.Devices.Spi;
using Windows.Devices.Gpio;

namespace nanoFramework.Drivers.Spi
{
    /// <summary>
    /// AD840x series Digital Potentiometer driver for nanoFramework
    /// Compatible with the AD8400, AD8402, AD8403
    /// <seealso cref="https://www.analog.com/media/en/technical-documentation/data-sheets/AD8400_8402_8403.pdf"/>
    /// </summary>
    public class AD840x
    {
        private readonly SpiDevice _spiDevice;
        private readonly GpioPin _shutdownPin;

        /// <summary>
        /// The Channel Number
        /// </summary>
        public enum Channel : byte
        {
            one = 0x00,
            two = 0x01,
            three = 0x02,
            four = 0x03
        }

        /// <summary>
        /// Opens the SPI connection and control pins
        /// </summary>
        /// <param name="spiBus">The SPI Bus</param>
        /// <param name="csPin">Chip Enable(CE)/Select(CS) pin</param>
        /// <param name="shutdownPin">The Shutdown Pin</param>
        public AD840x(string spiBus, int csPin, int shutdownPin)
        {
            // set the shutDownPin as an output
            _shutdownPin = GpioController.GetDefault().OpenPin(shutdownPin);
            _shutdownPin.SetDriveMode(GpioPinDriveMode.OutputOpenSourcePullDown);

            // create SPI device
            var connectionSettings = new SpiConnectionSettings(csPin);
            connectionSettings.DataBitLength = 8;
            connectionSettings.ClockFrequency = 1 * 1000 * 1000;
            connectionSettings.BitOrder = DataBitOrder.MSB;
            connectionSettings.Mode = SpiMode.Mode0;
            connectionSettings.SharingMode = SpiSharingMode.Shared;

            _spiDevice = SpiDevice.FromId(spiBus, connectionSettings);
        }

        /// <summary>
        /// Initializes the device to zero and shuts down the output
        /// </summary>
        public void Initialize()
        {
            //Shutdown the pots to start with
            DisableOutputs();

            //Set all pots to zero as a starting point
            for (uint channel = 0; channel < 4; channel++)
            {
                UpdateValue(channel, 0);
            }

        }

        /// <summary>
        /// Enables the outputs
        /// </summary>
        public void EnableOutputs()
        {
            _shutdownPin.Write(GpioPinValue.High);
        }

        /// <summary>
        /// Dissables the outputs
        /// </summary>
        public void DisableOutputs()
        {
            _shutdownPin.Write(GpioPinValue.Low);
        }


        /// <summary>
        /// Sends a new value to a channel
        /// </summary>
        /// <param name="channel">The channel number</param>
        /// <param name="value">The value 0-255</param>
        public void UpdateValue(uint channel, uint value)
        {
            if (channel > 3)
            {
                throw new System.Exception("Unsupported Channel");
            }

            if (value > 255)
            {
                value = 255;
            }

            _spiDevice.Write(new byte[] { (byte)channel, (byte)value });
        }

        /// <summary>
        /// Sends a new value to a channel
        /// </summary>
        /// <param name="channel">The channel</param>
        /// <param name="value">The value 0-255</param>
        public void SendValue(Channel channel, uint value)
        {
            if (value > 255)
            {
                value = 255;
            }

            _spiDevice.Write(new byte[] { (byte)channel, (byte)value });
        }
    }
}
