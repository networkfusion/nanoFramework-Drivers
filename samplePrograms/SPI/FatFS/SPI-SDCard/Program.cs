using System;
using System.Collections;
using System.Text;
using System.Threading;
using FatFsFez;
using Windows.Devices.Gpio;
using static SPI_FatFS.FF;
using static SPI_FatFS.DiskIO;

namespace SPI_SDCard
{
    class Program
    {
        DiskIO.SPIBusId = <SET YOUR BUS ID HERE>;
        DiskIO.ChipSelectPin = <SET YOUR CHIP SELECT PIN HERE>;

        // c# port of FatFs: http://elm-chan.org/fsw/ff/00index_e.html

        static FATFS fs = new FATFS();        /* FatFs work area needed for each volume */
        static FIL Fil = new FIL();           /* File object needed for each open file */

        static uint bw = 0;
        static FRESULT res;

        public static void Main()
        {
            Console.WriteLine("Start");
            GpioPin led = GpioController.GetDefault().OpenPin(<Enter your LED PIN>);
            led.SetDriveMode(GpioPinDriveMode.Output);
            led.Write(GpioPinValue.Low);


            try
            {
                MountDrive();
                DeleteFileExample();
                CreateDirectoriesExample();
                CreateFileExample();
                ReadFileExample();
                RenameFileExample();
                FileExistsExample();
                ListDirectoryExample();

                GetFreeSpaceExample();

                Console.WriteLine("Done");
                while (true)
                {
                    led.Write(GpioPinValue.High);
                    Thread.Sleep(100);
                    led.Write(GpioPinValue.Low);
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Thread.Sleep(Timeout.Infinite);
        }

        static void MountDrive()
        {
            res = Ff.Current.f_mount(ref fs, "", 1);     /* Give a work area to the default drive */
            res.ThrowIfError();

            Console.WriteLine("Drive successfully mounted");
        }

        static void CreateFileExample()
        {


            if ((res = Ff.Current.f_open(ref Fil, "/sub1/File1.txt", FA_WRITE | FA_CREATE_ALWAYS)) == Ff.FRESULT.FR_OK)
            {   /* Create a file */
                Random rnd = new Random();
                var payload = $"File contents is: It works ({rnd.Next()})!".ToByteArray();
                res = Ff.Current.f_write(ref Fil, payload, (uint)payload.Length, ref bw);    /* Write data to the file */
                res.ThrowIfError();

                res = Ff.Current.f_close(ref Fil);   /* Close the file */
                res.ThrowIfError();
            }
            else
            {
                res.ThrowIfError();
            }

            Console.WriteLine("File successfully created");
        }

        static void ReadFileExample()
        {

            if (Ff.Current.f_open(ref Fil, "/sub1/File1.txt", FA_READ) == Ff.FRESULT.FR_OK)
            {   /* Create a file */

                var newPayload = new byte[5000];
                res = Ff.Current.f_read(ref Fil, ref newPayload, 5000, ref bw);    /* Read data from file */
                res.ThrowIfError();

                var msg = Encoding.UTF8.GetString(newPayload, 0, (int)bw);
                Console.WriteLine($"{msg}");

                res = Ff.Current.f_close(ref Fil);                              /* Close the file */
                res.ThrowIfError();
            }

            Console.WriteLine("File successfully read");
        }

        static void DeleteFileExample()
        {
            res = Ff.Current.f_unlink("/sub1/File2.txt");     /* Give a work area to the default drive */
            res.ThrowIfError();

            Console.WriteLine("File successfully deleted");
        }

        static void ListDirectoryExample()
        {


            res = Ff.Current.f_mount(ref fs, "", 1);
            res.ThrowIfError();

            res = Scan_Files("/");
            res.ThrowIfError();

            Console.WriteLine("Directories successfully listed");
        }

        private static FRESULT Scan_Files(string path)
        {
            FRESULT res;
            FILINFO fno = new FILINFO();
            DIR dir = new DIR();
            byte[] buff = new byte[256];
            buff = path.ToNullTerminatedByteArray();

            res = Ff.Current.f_opendir(ref dir, buff);                      /* Open the directory */
            if (res == FRESULT.FR_OK)
            {
                for (; ; )
                {
                    res = Ff.Current.f_readdir(ref dir, ref fno);           /* Read a directory item */
                    if (res != FRESULT.FR_OK || fno.fname[0] == 0) break;   /* Break on error or end of dir */
                    if ((fno.fattrib & AM_DIR) > 0 && !((fno.fattrib & AM_SYS) > 0 || (fno.fattrib & AM_HID) > 0))
                    {
                        /* It is a directory */
                        var newpath = path + "/" + fno.fname.ToStringNullTerminationRemoved();
                        Console.WriteLine($"Directory: {path}/{fno.fname.ToStringNullTerminationRemoved()}");
                        res = Scan_Files(newpath);                    /* Enter the directory */
                        if (res != FRESULT.FR_OK) break;
                    }
                    else
                    {
                        /* It is a file. */
                        Console.WriteLine($"File: {path}/{fno.fname.ToStringNullTerminationRemoved()}");
                    }
                }
                Ff.Current.f_closedir(ref dir);
            }

            return res;
        }

        static void CreateDirectoriesExample()
        {

            res = Ff.Current.f_mkdir("sub1");
            if (res != FRESULT.FR_EXIST) res.ThrowIfError();


            res = Ff.Current.f_mkdir("sub1/sub2");
            if (res != FRESULT.FR_EXIST) res.ThrowIfError();

            res = Ff.Current.f_mkdir("sub1/sub2/sub3");
            if (res != FRESULT.FR_EXIST) res.ThrowIfError();

            Console.WriteLine("Directories successfully created");
        }

        static void FileExistsExample()
        {

            FILINFO fno = new FILINFO();

            res = Ff.Current.f_stat("/sub1/File2.txt", ref fno);
            switch (res)
            {

                case Ff.FRESULT.FR_OK:
                    Console.WriteLine($"Size: {fno.fsize}");
                    Console.WriteLine(String.Format("Timestamp: {0}/{1}/{2}, {3}:{4}",
                           (fno.fdate >> 9) + 1980, fno.fdate >> 5 & 15, fno.fdate & 31,
                           fno.ftime >> 11, fno.ftime >> 5 & 63));
                    Console.WriteLine(String.Format("Attributes: {0}{1}{2}{3}{4}",
                           (fno.fattrib & AM_DIR) > 0 ? 'D' : '-',
                           (fno.fattrib & AM_RDO) > 0 ? 'R' : '-',
                           (fno.fattrib & AM_HID) > 0 ? 'H' : '-',
                           (fno.fattrib & AM_SYS) > 0 ? 'S' : '-',
                           (fno.fattrib & AM_ARC) > 0 ? 'A' : '-'));
                    break;

                case Ff.FRESULT.FR_NO_FILE:
                    Console.WriteLine("File does not exist");
                    break;

                default:
                    Console.WriteLine($"An error occured. {res.ToString()}");
                    break;
            }
        }

        static void GetFreeSpaceExample()
        {

            uint fre_clust = 0;
            uint fre_sect, tot_sect;

            /* Get volume information and free clusters of drive 1 */
            res = Ff.Current.f_getfree("0:", ref fre_clust, ref fs);
            if (res != FRESULT.FR_OK)
            {
                Console.WriteLine($"An error occured. {res.ToString()}");
                return;
            };

            /* Get total sectors and free sectors */
            tot_sect = (fs.n_fatent - 2) * fs.csize;
            fre_sect = fre_clust * fs.csize;

            /* Print the free space (assuming 512 bytes/sector) */
            Console.WriteLine(String.Format("{0} KB total drive space\n{1} KB available", tot_sect / 2, fre_sect / 2));
        }

        static void RenameFileExample()
        {
            /* Rename an object in the default drive */
            res = Ff.Current.f_rename("/sub1/File1.txt", "/sub1/File2.txt");
            res.ThrowIfError();

            Console.WriteLine("File successfully renamed");
        }
    }
}