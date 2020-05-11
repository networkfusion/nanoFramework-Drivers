//https://datasheets.maximintegrated.com/en/ds/MAX31865PMB1.pdf
//1	3.3v -> 3.3v
//2
//3 irq -> dr -> D4
//4
//5	
//6	cs -> cs -> D3
//7	mosi -> sdi -> A5
//8	miso -> sdo -> A4
//9	sck -> sclk  -> D6
//10 gnd -> gnd


using System;
using System.Threading;
using NetworkFusion.Drivers;

namespace MAX31865_Sample
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

                //    var trunkatedTemp = System.Math.Truncate((temperature * 100) / 100);
                //    if (temperature > -150 && temperature < 150) // on startup the sensor can show -248 before it has been initialised properly ?! if the ADC is not attached it will read more 855
                Console.WriteLine($"Fault Status: {GetFaultStatus()}, config: {GetCurrentConfig()}");
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
