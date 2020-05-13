AD840x Digital Potentiometer Control

This example controls an Analog Devices AD8403 digital potentiometer.
The AD8403 has 4 potentiometer channels. Each channel's pins are labeled
A - connect this to voltage
W - this is the pot's wiper, which changes when you set it
B - connect this to ground.

The AD8403 is SPI-compatible. To command it you send two bytes.
The first byte is the channel number starting at zero: (0 - 3)
The second byte is the resistance value for the channel: (0 - 255).

The AD8403 also contains shutdown (SHDN) and reset (RS) pins.  The shutdown pin disables ALL of the potentiometers when taken low and enables ALL pots when high. 
The values of the pots can be adjusted while shutdown is active. The reset pin sets all of the pots back to their center value when taken low.
Reset may or may not be useful for your particular application and should be controlled seperately from the driver. In this circuit reset is simply tied to +5v to keep it inactive.

The circuit:
* All A pins of AD8403 connected to +5V
* All B pins of AD8403 connected to ground
* An LED and a 220-ohm resisor in series connected from each W pin to ground

|Device Pin (dependent on device) | Function | Target Pin |
| -- | -- | -- |
| VDD | VDD | 3.3v / 5v |
| DGND | GND | GND |
| RS | RESET |+3.3v / +5v |
| SHDN | SHUTDOWN | D4 |
| CS | CS/SS | D3 |
| SDI | SDI/MOSI | A5 |
| CLK | CLK/SCK | D6 |
