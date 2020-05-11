
This driver is for a MAX31865 PT100/PT1000 sensor board.
An example of one can be found at:
https://datasheets.maximintegrated.com/en/ds/MAX31865PMB1.pdf


The sample uses a MAX31865PMB1 with an STM32F769I-Discovery. It needs to be connected as follows:
| MAX31865PMB1 Pin | Function | STM32F769I-Discovery Pin |
| 1 | CS | D3 |
| 2 | MOSI| A5 |
| 3 | MISO | A4 |
| 4 | SCK | D6 |
| 5|  GND | GND |
| 6|  VCC | 3.3VDC |
| 7 | DR | D4 |
| 8 |	|	|
| 9 |	|	|
| 10 |	|	|	
| 11 | GND | GND |
| 12 | VCC | 3.3VDC |