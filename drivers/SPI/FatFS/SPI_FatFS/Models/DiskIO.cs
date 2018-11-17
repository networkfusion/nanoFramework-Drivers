using Windows.Devices.Gpio;
using System;
using System.Threading;

namespace SPI_FatFS
{
    static class DiskIO
    {
        /*------------------------------------------------------------------------/
        /  Foolproof MMCv3/SDv1/SDv2 (in SPI mode) control module
        /-------------------------------------------------------------------------/
        /
        /  Copyright (C) 2013, ChaN, all right reserved.
        /
        / * This software is a free software and there is NO WARRANTY.
        / * No restriction on use. You can use, modify and redistribute it for
        /   personal, non-profit or commercial products UNDER YOUR RESPONSIBILITY.
        / * Redistributions of source code must retain the above copyright notice.
        /
        /-------------------------------------------------------------------------/
          Features and Limitations:

          * Easy to Port Bit-banging SPI
            It uses only four GPIO pins. No complex peripheral needs to be used.

          * Platform Independent
            You need to modify only a few macros to control the GPIO port.

          * Low Speed
            The data transfer rate will be several times slower than hardware SPI.

          * No Media Change Detection
            Application program needs to perform a f_mount() after media change.

        /-------------------------------------------------------------------------*/


        #region Hardware specific code
        // GPIO
        public static GpioPin chipSelectPin;
        public static string BusId;

        public static void InitializeCpuIO(string busId, GpioPin csPin)
        {
            chipSelectPin = csPin;
            BusId = busId;

            if (chipSelectPin == null)
            {
                chipSelectPin = GpioController.GetDefault().OpenPin(chipSelectPin.PinNumber);
                chipSelectPin.SetDriveMode(GpioPinDriveMode.Output);
                chipSelectPin.Write(GpioPinValue.High);
            }

            Spi.InitSpi(BusId, chipSelectPin);     /* Initialize ports to control MMC */
        }

        #endregion

        #region Original C include file definition

        /* Results of Disk Functions */
        public enum DRESULT
        {
            RES_OK = 0,     /* 0: Successful */
            RES_ERROR,      /* 1: R/W Error */
            RES_WRPRT,      /* 2: Write Protected */
            RES_NOTRDY,     /* 3: Not Ready */
            RES_PARERR      /* 4: Invalid Parameter */
        }


        /* Disk Status Bits (DSTATUS) */
        const byte STA_NOINIT = 0x01;   /* Drive not initialized */
        const byte STA_NODISK = 0x02;   /* No medium in the drive */
        const byte STA_PROTECT = 0x04;  /* Write protected */


        /* Command code for disk_ioctrl fucntion */

        /* Generic command (Used by FatFs) */
        public const byte CTRL_SYNC = 0;   /* Complete pending write process (needed at _FS_READONLY == 0) */
        public const byte GET_SECTOR_COUNT = 1;    /* Get media size (needed at _USE_MKFS == 1) */
        public const byte GET_SECTOR_SIZE = 2; /* Get sector size (needed at _MAX_SS != _MIN_SS) */
        public const byte GET_BLOCK_SIZE = 3;  /* Get erase block size (needed at _USE_MKFS == 1) */
        public const byte CTRL_TRIM = 4;   /* Inform device that the data on the block of sectors is no longer used (needed at _USE_TRIM == 1) */


        /* MMC card type flags (MMC_GET_TYPE) */
        const byte CT_MMC = 0x01;       /* MMC ver 3 */
        const byte CT_SD1 = 0x02;       /* SD ver 1 */
        const byte CT_SD2 = 0x04;       /* SD ver 2 */
        const byte CT_SDC = (CT_SD1 | CT_SD2);	/* SD */
        const byte CT_BLOCK = 0x08;     /* Block addressing */

        #endregion


        /*-------------------------------------------------------------------------*/
        /* Platform dependent converted macros and functions needed to be modified */
        /*-------------------------------------------------------------------------*/

        static void dly_us(uint n) /* Delay n microseconds (avr-gcc -Os) */
        {
            if (n < 1000)
            {
                Thread.Sleep(1);
            }
            else
            {
                Thread.Sleep((int)n / 1000);
            }
        }

        /*--------------------------------------------------------------------------

           Module Private Functions

        ---------------------------------------------------------------------------*/

        /* MMC/SD command (SPI mode) */
        const byte CMD0 = (0);      /* GO_IDLE_STATE */
        const byte CMD1 = (1);      /* SEND_OP_COND */
        const byte ACMD41 = (0x80 + 41);    /* SEND_OP_COND (SDC) */
        const byte CMD8 = (8);      /* SEND_IF_COND */
        const byte CMD9 = (9);      /* SEND_CSD */
        const byte CMD10 = (10);        /* SEND_CID */
        const byte CMD12 = (12);        /* STOP_TRANSMISSION */
        const byte CMD13 = (13);        /* SEND_STATUS */
        const byte ACMD13 = (0x80 + 13);    /* SD_STATUS (SDC) */
        const byte CMD16 = (16);        /* SET_BLOCKLEN */
        const byte CMD17 = (17);        /* READ_SINGLE_BLOCK */
        const byte CMD18 = (18);        /* READ_MULTIPLE_BLOCK */
        const byte CMD23 = (23);        /* SET_BLOCK_COUNT */
        const byte ACMD23 = (0x80 + 23);    /* SET_WR_BLK_ERASE_COUNT (SDC) */
        const byte CMD24 = (24);    /* WRITE_BLOCK */
        const byte CMD25 = (25);    /* WRITE_MULTIPLE_BLOCK */
        const byte CMD32 = (32);    /* ERASE_ER_BLK_START */
        const byte CMD33 = (33);        /* ERASE_ER_BLK_END */
        const byte CMD38 = (38);        /* ERASE */
        const byte CMD55 = (55);        /* APP_CMD */
        const byte CMD58 = (58);		/* READ_OCR */


        static byte Stat = STA_NOINIT;  /* Disk status */

        static byte CardType;          /* b0:MMC, b1:SDv1, b2:SDv2, b3:Block addressing */



        /*-----------------------------------------------------------------------*/
        /* Transmit bytes to the card                                            */
        /*-----------------------------------------------------------------------*/

        static void xmit_mmc(
            byte[] buff,    /* Data to be sent */
            uint bc		    /* Number of bytes to send */
            )
        {
            byte d;
            int bufIndex = 0;
            do
            {
                d = buff[bufIndex++]; /* Get a byte to be sent */
                Spi.XmitSpi(d);
            } while (bufIndex < bc);
        }



        /*-----------------------------------------------------------------------*/
        /* Receive bytes from the card                                           */
        /*-----------------------------------------------------------------------*/

        static void rcvr_mmc(
            ref byte[] buff,    /* Pointer to read buffer */
            uint bc		        /* Number of bytes to receive */
        )
        {
            int rxIndex = 0;
            do
            {
                buff[rxIndex++] = Spi.RcvSpi(); /* Store a received byte */
            }
            while (--bc > 0);
        }



        /*-----------------------------------------------------------------------*/
        /* Wait for card ready                                                   */
        /*-----------------------------------------------------------------------*/

        static int wait_ready()	/* 1:OK, 0:Timeout */
        {
            byte[] d = new byte[1];
            uint tmr;


            for (tmr = 5000; tmr > 0; tmr--)
            {   /* Wait for ready in timeout of 500ms */
                rcvr_mmc(ref d, 1);
                if (d[0] == 0xFF) break;
                dly_us(100);
            }

            return tmr > 1 ? 1 : 0;
        }



        /*-----------------------------------------------------------------------*/
        /* Deselect the card and release SPI bus                                 */
        /*-----------------------------------------------------------------------*/

        static void deselect()
        {
            chipSelectPin.Write(GpioPinValue.High);
            Spi.RcvSpi();	/* Dummy clock (force DO hi-z for multiple slave SPI) */
        }



        /*-----------------------------------------------------------------------*/
        /* Select the card and wait for ready                                    */
        /*-----------------------------------------------------------------------*/

        static int select()	/* 1:OK, 0:Timeout */
        {
            /* Set CS# low */
            chipSelectPin.Write(GpioPinValue.Low);
            Spi.RcvSpi();                   /* Dummy clock (force DO enabled) */
            if (wait_ready() > 0) return 1; /* Wait for card ready */

            deselect();
            return 0;			            /* Failed */
        }



        /*-----------------------------------------------------------------------*/
        /* Receive a data packet from the card                                   */
        /*-----------------------------------------------------------------------*/

        static int rcvr_datablock( /* 1:OK, 0:Failed */
            ref byte[] buff,    /* Data buffer to store received data */
            uint btr			/* Byte count */
        )
        {
            byte[] d = new byte[2];
            uint tmr;


            for (tmr = 1000; tmr > 0; tmr--)
            {   /* Wait for data packet in timeout of 100ms */
                rcvr_mmc(ref d, 1);
                if (d[0] != 0xFF) break;
                dly_us(100);
            }
            if (d[0] != 0xFE) return 0;     /* If not valid data token, return with error */

            rcvr_mmc(ref buff, btr);            /* Receive the data block into buffer */
            rcvr_mmc(ref d, 2);                 /* Discard CRC */

            return 1;						/* Return with success */
        }



        /*-----------------------------------------------------------------------*/
        /* Send a data packet to the card                                        */
        /*-----------------------------------------------------------------------*/

        static int xmit_datablock(  /* 1:OK, 0:Failed */
            byte[] buff,        /* 512 byte data block to be transmitted */
            byte token			/* Data/Stop token */
        )
        {
            byte[] d = new byte[2];


            if (wait_ready() == 0) return 0;

            d[0] = token;
            xmit_mmc(d, 1);             /* Xmit a token */
            if (token != 0xFD)
            {
                /* Is it data token? */
                xmit_mmc(buff, 512);    /* Xmit the 512 byte data block to MMC */
                rcvr_mmc(ref d, 2);         /* Xmit dummy CRC (0xFF,0xFF) */
                rcvr_mmc(ref d, 1);         /* Receive data response */
                if ((d[0] & 0x1F) != 0x05)  /* If not accepted, return with error */
                    return 0;
            }

            return 1;
        }



        /*-----------------------------------------------------------------------*/
        /* Send a command packet to the card                                     */
        /*-----------------------------------------------------------------------*/

        static byte send_cmd(      /* Returns command response (bit7==1:Send failed)*/
            byte cmd,       /* Command byte */
            uint arg		/* Argument */
        )
        {
            byte n;
            byte[] d = new byte[1];
            byte[] buf = new byte[6];


            if ((cmd & 0x80) > 0)
            {
                /* ACMD<n> is the command sequense of CMD55-CMD<n> */
                cmd &= 0x7F;
                n = send_cmd(CMD55, 0);
                if (n > 1) return n;
            }

            /* Select the card and wait for ready except to stop multiple block read */
            if (cmd != CMD12)
            {
                deselect();
                if (select() == 0) return 0xFF;
            }

            /* Send a command packet */
            buf[0] = (byte)(0x40 | cmd);            /* Start + Command index */
            buf[1] = (byte)(arg >> 24);     /* Argument[31..24] */
            buf[2] = (byte)(arg >> 16);     /* Argument[23..16] */
            buf[3] = (byte)(arg >> 8);      /* Argument[15..8] */
            buf[4] = (byte)arg;             /* Argument[7..0] */
            n = 0x01;                       /* Dummy CRC + Stop */
            if (cmd == CMD0) n = 0x95;      /* (valid CRC for CMD0(0)) */
            if (cmd == CMD8) n = 0x87;      /* (valid CRC for CMD8(0x1AA)) */
            buf[5] = n;
            xmit_mmc(buf, 6);

            /* Receive command response */
            if (cmd == CMD12) rcvr_mmc(ref d, 1);   /* Skip a stuff byte when stop reading */
            n = 10;                                 /* Wait for a valid response in timeout of 10 attempts */
            do
                rcvr_mmc(ref d, 1);
            while ((d[0] & 0x80) > 0 && --n > 0);

            return d[0];			/* Return with the response value */
        }



        /*--------------------------------------------------------------------------

           Public Functions

        ---------------------------------------------------------------------------*/


        /*-----------------------------------------------------------------------*/
        /* Get Disk Status                                                       */
        /*-----------------------------------------------------------------------*/

        public static byte disk_status(
            byte drv			/* Drive number (always 0) */
        )
        {
            if (drv > 0) return STA_NOINIT;

            return Stat;
        }



        /*-----------------------------------------------------------------------*/
        /* Initialize Disk Drive                                                 */
        /*-----------------------------------------------------------------------*/

        public static byte disk_initialize(
            byte drv,		/* Physical drive nmuber (0) */
            string busId,
            GpioPin csPin
        )
        {
            byte n, ty, cmd;
            byte[] buf = new byte[4];
            uint tmr;
            byte s;


            if (drv > 0) return STA_NOINIT;

            dly_us(10000);          /* 10ms */
            InitializeCpuIO(busId, csPin);


            for (n = 10; n > 0; n--) rcvr_mmc(ref buf, 1);  /* Apply 80 dummy clocks and the card gets ready to receive command */

            ty = 0;
            if (send_cmd(CMD0, 0) == 1)
            {
                /* Enter Idle state */
                if (send_cmd(CMD8, 0x1AA) == 1)
                {
                    /* SDv2? */
                    rcvr_mmc(ref buf, 4);                           /* Get trailing return value of R7 resp */
                    if (buf[2] == 0x01 && buf[3] == 0xAA)
                    {
                        /* The card can work at vdd range of 2.7-3.6V */
                        for (tmr = 1000; tmr > 0; tmr--)
                        {
                            /* Wait for leaving idle state (ACMD41 with HCS bit) */
                            if (send_cmd(ACMD41, 0x0001 << 30) == 0) break;
                            dly_us(1000);
                        }
                        if (tmr > 0 && send_cmd(CMD58, 0) == 0)
                        {
                            /* Check CCS bit in the OCR */
                            rcvr_mmc(ref buf, 4);
                            ty = ((buf[0] & 0x40) > 0) ? (byte)(CT_SD2 | CT_BLOCK) : CT_SD2;  /* SDv2 */
                        }
                    }
                }
                else
                {
                    /* SDv1 or MMCv3 */
                    if (send_cmd(ACMD41, 0) <= 1)
                    {
                        ty = CT_SD1; cmd = ACMD41;  /* SDv1 */
                    }
                    else
                    {
                        ty = CT_MMC; cmd = CMD1;    /* MMCv3 */
                    }
                    for (tmr = 1000; tmr > 0; tmr--)
                    {
                        /* Wait for leaving idle state */
                        if (send_cmd(cmd, 0) == 0) break;
                        dly_us(1000);
                    }
                    if (tmr == 0 || send_cmd(CMD16, 512) != 0)  /* Set R/W block length to 512 */
                        ty = 0;
                }
            }
            CardType = ty;
            s = (ty > 0) ? (byte)0 : STA_NOINIT;
            Stat = s;

            deselect();

            return s;
        }



        /*-----------------------------------------------------------------------*/
        /* Read Sector(s)                                                        */
        /*-----------------------------------------------------------------------*/

        public static DRESULT disk_read(
            byte drv,           /* Physical drive nmuber (0) */
            ref byte[] buff,    /* Pointer to the data buffer to store read data */
            uint sector,        /* Start sector number (LBA) */
            uint count			/* Sector count (1..128) */
        )
        {
            byte cmd;
            int bufIndex;
            byte[] workBuf = new byte[512];

            if ((disk_status(drv) & STA_NOINIT) > 0) return DRESULT.RES_NOTRDY;
            if ((CardType & CT_BLOCK) == 0) sector *= 512;  /* Convert LBA to byte address if needed */

            cmd = count > 1 ? CMD18 : CMD17;            /*  READ_MULTIPLE_BLOCK : READ_SINGLE_BLOCK */
            if (send_cmd(cmd, sector) == 0)
            {
                bufIndex = 0;
                do
                {
                    if (rcvr_datablock(ref workBuf, 512) == 0) break;
                    Array.Copy(workBuf, 0, buff, bufIndex, 512);
                    bufIndex += 512;
                } while (--count > 0);
                if (cmd == CMD18) send_cmd(CMD12, 0);   /* STOP_TRANSMISSION */
            }
            deselect();

            return count > 0 ? DRESULT.RES_ERROR : DRESULT.RES_OK;
        }



        /*-----------------------------------------------------------------------*/
        /* Write Sector(s)                                                       */
        /*-----------------------------------------------------------------------*/

        public static DRESULT disk_write(
            byte drv,           /* Physical drive nmuber (0) */
            byte[] buff,   /* Pointer to the data to be written */
            uint sector,       /* Start sector number (LBA) */
            uint count			/* Sector count (1..128) */
        )
        {
            byte[] workBuf = new byte[12];
            int bufIndex;

            if ((disk_status(drv) & STA_NOINIT) > 0) return DRESULT.RES_NOTRDY;
            if ((CardType & CT_BLOCK) == 0) sector *= 512;  /* Convert LBA to byte address if needed */

            if (count == 1)
            {
                /* Single block write */
                if ((send_cmd(CMD24, sector) == 0)  /* WRITE_BLOCK */
                    && xmit_datablock(buff, 0xFE) > 0)
                    count = 0;
            }
            else
            {
                /* Multiple block write */
                if ((CardType & CT_SDC) > 0) send_cmd(ACMD23, count);
                if (send_cmd(CMD25, sector) == 0)
                {
                    /* WRITE_MULTIPLE_BLOCK */
                    bufIndex = 0;
                    do
                    {
                        Array.Copy(buff, bufIndex, workBuf, 0, 512);
                        if (xmit_datablock(workBuf, 0xFC) == 0) break;
                        bufIndex += 512;
                    } while (--count > 0);
                    if (xmit_datablock(null, 0xFD) == 0)   /* STOP_TRAN token */
                        count = 1;
                }
            }
            deselect();

            return count > 0 ? DRESULT.RES_ERROR : DRESULT.RES_OK;
        }


        /*-----------------------------------------------------------------------*/
        /* Miscellaneous Functions                                               */
        /*-----------------------------------------------------------------------*/

        public static DRESULT disk_ioctl(
            byte drv,       /* Physical drive nmuber (0) */
            byte ctrl,      /* Control code */
            ref byte[] buff	/* Buffer to send/receive control data */
        )
        {
            DRESULT res;
            byte n;
            byte[] csd = new byte[16];
            uint cs;


            if ((disk_status(drv) & STA_NOINIT) > 0) return DRESULT.RES_NOTRDY;   /* Check if card is in the socket */

            res = DRESULT.RES_ERROR;
            switch (ctrl)
            {
                case CTRL_SYNC:     /* Make sure that no pending write process */
                    if (select() > 0) res = DRESULT.RES_OK;
                    break;

                case GET_SECTOR_COUNT:  /* Get number of sectors on the disk (DWORD) */
                    if ((send_cmd(CMD9, 0) == 0) && rcvr_datablock(ref csd, 16) > 0)
                    {
                        if ((csd[0] >> 6) == 1)
                        {   /* SDC ver 2.00 */
                            cs = csd[9] + ((uint)csd[8] << 8) + ((uint)(csd[7] & 63) << 16) + 1;
                            var numberOfSectors = cs << 10;
                            buff[0] = (byte)(numberOfSectors >> 24);
                            buff[1] = (byte)(numberOfSectors >> 16);
                            buff[2] = (byte)(numberOfSectors >> 8);
                            buff[3] = (byte)numberOfSectors;
                        }
                        else
                        {
                            /* SDC ver 1.XX or MMC */
                            n = (byte)((csd[5] & 15) + ((csd[10] & 128) >> 7) + ((csd[9] & 3) << 1) + 2);
                            cs = (uint)((csd[8] >> 6) + ((uint)csd[7] << 2) + ((uint)(csd[6] & 3) << 10) + 1);
                            var numberOfSectors = cs << (n - 9);
                            buff[0] = (byte)(numberOfSectors >> 24);
                            buff[1] = (byte)(numberOfSectors >> 16);
                            buff[2] = (byte)(numberOfSectors >> 8);
                            buff[3] = (byte)numberOfSectors;
                        }
                        res = DRESULT.RES_OK;
                    }
                    break;

                case GET_BLOCK_SIZE:    /* Get erase block size in unit of sector (DWORD) */
                    buff[0] = 128;
                    res = DRESULT.RES_OK;
                    break;
                default:
                    res = DRESULT.RES_PARERR;
                    break;
            }

            deselect();

            return res;
        }

    }
}