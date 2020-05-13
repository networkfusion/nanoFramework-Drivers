//
// Copyright (c) 2020 The nanoFramework project contributors
// Portions Copyright (c) 2020 Robin Jones (NetworkFusion).  All rights reserved.
// See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;

namespace nanoFramework.Drivers.Spi
{
    public class Program
    {
        static AD840x _digitalPot;
        

        public static void Main()
        {
            Console.WriteLine("AD8403 Sample App!");

            //Using an STM32F769I-Discovery
            _digitalPot = new AD840x("SPI5", PinNumber('F', 6), PinNumber('J', 0));
            _digitalPot.Initialize();

            Test();

        }

        public static void Test()
        {
            _digitalPot.EnableOutputs();

            for ( ; ; )
            {
                // Loop through the four channels of the digital pot.
                for (uint channel = 0; channel < 4; channel++)
                {

                    // Change the resistance on this channel from min to max.

                    // Starting at 50 because the LED doesn't visibly change
                    // before that point.
                    for (uint level = 50; level < 255; level++)
                    {
                        _digitalPot.UpdateValue(channel, level);
                        Thread.Sleep(200);
                    }

                    // wait a bit at the top
                    Thread.Sleep(500);

                    // change the resistance on this channel from max to min:
                    for (uint level = 255; level > 50; level--)
                    {
                        _digitalPot.UpdateValue(channel, level);
                        Thread.Sleep(200);
                    }

                    // wait a bit at the bottom
                    Thread.Sleep(500);

                }
            }
        }


        private static int PinNumber(char port, byte pin)
        {
            if (port < 'A' || port > 'J')
                throw new ArgumentException();

            return ((port - 'A') * 16) + pin;
        }
    }
}
