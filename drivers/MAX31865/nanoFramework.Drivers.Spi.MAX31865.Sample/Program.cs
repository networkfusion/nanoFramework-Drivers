//https://datasheets.maximintegrated.com/en/ds/MAX31865PMB1.pdf
// this example uses the STM32F769I-Discovery as an example. It needs to be connected as follows:
//1 cs      -> D3
//2	mosi    -> A5
//3	miso    -> A4
//4 sck     -> D6
//5 gnd     -> gnd
//6 vcc     -> 3.3v
//7 dr      -> D4
//8
//9	
//10	
//11 gnd    -> gnd
//12 vcc    -> 3.3v


using System;
using System.Threading;

namespace nanoFramework.Drivers.Spi.MAX31865.Sample
{
    public class Program
    {
        private static MAX31865 MAX31865_Instance;

        public static byte config = (byte)(
            (byte)MAX31865.ConfigValues.VBIAS_ON |
            (byte)MAX31865.ConfigValues.TWO_WIRE | //with default sensor, but should be 3 or 4wire depending on jumpers
            (byte)MAX31865.ConfigValues.FAULT_CLEAR_STATE |
            (byte)MAX31865.ConfigValues.FILTER_60Hz);


        public static void Main()
        {
            Console.WriteLine("MAX31865 Driver Demo.");

            MAX31865_Instance = new MAX31865("SPI5", PinNumber('F', 6));
            MAX31865_Instance.Initialize(PinNumber('J', 0), config, 400);


            PollingSenario();
            //EventSenario()
        }


        public static void PollingSenario()
        {
            MAX31865_Instance.SetConvToManual();

            var i = 0;

            Console.WriteLine("PRT Data:");

            for ( ; ; )
            {
                ExecuteOneshot();

                //note: on startup the sensor can show -248 before it has been initialised properly ?! if the ADC is not attached it will read more 855
                //    if (temperature > -150 && temperature < 150) 
                Console.WriteLine($"Fault Status: {GetFaultStatus()}, config: {GetCurrentConfig()}");
                // note: reading a sensor past their default accuracy is not really helpful, for production, it would would be wise to use something like:
                //    var trunkatedTemp = System.Math.Truncate((GetTemperature() * 100) / 100);
                Console.WriteLine($"{i++}: temperature: {GetTemperature()}, resistance: {GetResistance()}");

                Thread.Sleep(15000); //15 seconds is about right to stop self heating from occuring on the sensor
            }
        }


        public static void EventSenario()
        {

            MAX31865_Instance.SetConvToAuto(); //Auto is 50 or 60hz, do we really want to read that many times per second?

            MAX31865_Instance.EnableFaultScanner(1000);
            MAX31865_Instance.FaultEvent += MAX31865_Instance_FaultEvent;

            MAX31865_Instance.DataReadyCelsiusEvent += MAX31865_Instance_DataReadyCelEvent;

            Thread.Sleep(Timeout.Infinite);


        }


        public static float GetResistance()
        {

            return MAX31865_Instance.GetResistance();
        }


        public static float GetTemperature()
        {

            return MAX31865_Instance.GetTemperatureCelsius();
        }


        public static string GetFaultStatus()
        {
            return MAX31865_Instance.GetRegister(0x07).ToString("X");
        }


        public static string GetCurrentConfig()
        {
            return MAX31865_Instance.GetRegister(0x00).ToString("X");
        }


        public static void ExecuteOneshot()
        {
            MAX31865_Instance.ExecuteOneShot();
        }


        public static void MAX31865_Instance_DataReadyCelEvent(MAX31865 sender, float Data)
        {
            Console.WriteLine("Temperature: " + GetTemperature() + "c ");
        }


        public static void MAX31865_Instance_FaultEvent(MAX31865 sender, byte FaultByte)
        {
            Console.WriteLine("Fault: " + FaultByte.ToString("X"));
            MAX31865_Instance.ClearFaults();
        }


        private static int PinNumber(char port, byte pin)
        {
            if (port < 'A' || port > 'J')
                throw new ArgumentException();

            return ((port - 'A') * 16) + pin;
        }
    }
}
