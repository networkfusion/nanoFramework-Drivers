using Windows.Devices.Spi;
using Windows.Devices.Gpio;

namespace SPI.FatFS
{
    static class Spi
    {
        static SpiDevice device = null;

        /* usi.S: Initialize MMC control ports */
        public static void InitSpi(string busId, GpioPin chipSelectPin)
        {
            if (device == null)
            {
                var settings = new SpiConnectionSettings(chipSelectPin.PinNumber)   // The slave's select pin. Not used. CS is controlled by by GPIO pin
                {
                    Mode = SpiMode.Mode0,
                    ClockFrequency = 15 * 1000 * 1000,       //15 Mhz
                    DataBitLength = 8,
                };
                device = SpiDevice.FromId(busId, settings);
            }

        }

        /* usi.S: Send a byte to the MMC */
        public static void XmitSpi(byte d)
        {
            byte[] writeBuf = { d };
            device.Write(writeBuf);
        }

        /* usi.S: Send a 0xFF to the MMC and get the received byte */
        public static byte RcvSpi()
        {
            byte[] writeBuf = { 0xff };
            byte[] readBuf = { 0x00 };

            device.TransferFullDuplex(writeBuf, readBuf);
            return readBuf[0];
        }

    }
}