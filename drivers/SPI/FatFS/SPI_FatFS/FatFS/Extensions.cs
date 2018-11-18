using Windows.Devices.Gpio;
using System;
using System.Text;
using static SPI.FatFS.FF;

namespace SPI.FatFS
{
    public static class Extensions
    {
        public static void ToHi(this GpioPin pin)
        {
            pin.Write(GpioPinValue.High);
        }

        public static void ToLow(this GpioPin pin)
        {
            pin.Write(GpioPinValue.Low);
        }

        public static bool IsHi(this GpioPin pin)
        {
            return pin.Read() == GpioPinValue.High;
        }

        public static byte[] Slice(this byte[] arr, uint indexFrom, uint count)
        {
            uint length = count;
            var result = new byte[length];
            Array.Copy(arr, (int)indexFrom, result, 0, (int)length);

            return result;
        }

        public static byte[] SubArray(this byte[] arr, uint indexFrom)
        {
            int length = arr.Length - (int)indexFrom;
            var result = new byte[length];
            Array.Copy(arr, (int)indexFrom, result, 0, (int)length);

            return result;
        }

        public static byte[] ToNullTerminatedByteArray(this string str)
        {
            var arr = Encoding.UTF8.GetBytes(str);
            var result = new byte[arr.Length + 1];
            result[result.Length - 1] = 0;
            Array.Copy(arr, result, arr.Length);
            return result;
        }

        public static byte[] ToByteArray(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static string ToStringNullTerminationRemoved(this byte[] buf)
        {
            var value = Encoding.UTF8.GetString(buf,0, buf.Length);
            return value.TrimEnd('\0');
        }

        public static void ThrowIfError(this FRESULT res)
        {
            string msg;
            if (res != FRESULT.FR_OK)
            {
                switch (res)
                {
                    case FRESULT.FR_OK:
                        msg = "FRESULT.FR_OK";
                        break;
                    case FRESULT.FR_DISK_ERR:
                        msg = "FRESULT.FR_DISK_ERR";
                        break;
                    case FRESULT.FR_INT_ERR:
                        msg = "FRESULT.FR_INT_ERR";
                        break;
                    case FRESULT.FR_NOT_READY:
                        msg = "FRESULT.FR_NOT_READY";
                        break;
                    case FRESULT.FR_NO_FILE:
                        msg = "FRESULT.FR_NO_FILE";
                        break;
                    case FRESULT.FR_NO_PATH:
                        msg = "FRESULT.FR_NO_PATH";
                        break;
                    case FRESULT.FR_INVALID_NAME:
                        msg = "FRESULT.FR_INVALID_NAME";
                        break;
                    case FRESULT.FR_DENIED:
                        msg = "FRESULT.FR_DENIED";
                        break;
                    case FRESULT.FR_EXIST:
                        msg = "FRESULT.FR_EXIST";
                        break;
                    case FRESULT.FR_INVALID_OBJECT:
                        msg = "FRESULT.FR_INVALID_OBJECT";
                        break;
                    case FRESULT.FR_WRITE_PROTECTED:
                        msg = "FRESULT.FR_WRITE_PROTECTED";
                        break;
                    case FRESULT.FR_INVALID_DRIVE:
                        msg = "FRESULT.FR_INVALID_DRIVE";
                        break;
                    case FRESULT.FR_NOT_ENABLED:
                        msg = "FRESULT.FR_NOT_ENABLED";
                        break;
                    case FRESULT.FR_NO_FILESYSTEM:
                        msg = "FRESULT.FR_NO_FILESYSTEM";
                        break;
                    case FRESULT.FR_MKFS_ABORTED:
                        msg = "FRESULT.FR_MKFS_ABORTED";
                        break;
                    case FRESULT.FR_TIMEOUT:
                        msg = "FRESULT.FR_TIMEOUT";
                        break;
                    case FRESULT.FR_LOCKED:
                        msg = "FRESULT.FR_LOCKED";
                        break;
                    case FRESULT.FR_NOT_ENOUGH_CORE:
                        msg = "FRESULT.FR_NOT_ENOUGH_CORE";
                        break;
                    case FRESULT.FR_TOO_MANY_OPEN_FILES:
                        msg = "FRESULT.FR_TOO_MANY_OPEN_FILES";
                        break;
                    case FRESULT.FR_INVALID_PARAMETER:
                        msg = "FRESULT.FR_INVALID_PARAMETER";
                        break;
                    default:
                        msg = "FRESULT.UNDEFINED";
                        break;
                }
                throw new ApplicationException($"Error: {msg}");
            }
        }
    }
}