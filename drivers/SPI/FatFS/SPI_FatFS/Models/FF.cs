using System;
using System.Text;

namespace SPI_FatFS
{
    public class FF
    {

        /*----------------------------------------------------------------------------/
        /  FatFs - Generic FAT Filesystem Module  R0.13b                              /
        /-----------------------------------------------------------------------------/
        /
        / Copyright (C) 2018, ChaN, all right reserved.
        /
        / FatFs module is an open source software. Redistribution and use of FatFs in
        / source and binary forms, with or without modification, are permitted provided
        / that the following condition is met:
        /
        / 1. Redistributions of source code must retain the above copyright notice,
        /    this condition and the following disclaimer.
        /
        / This software is provided by the copyright holder and contributors "AS IS"
        / and any warranties related to this software are DISCLAIMED.
        / The copyright owner or contributors be NOT LIABLE for any damages caused
        / by use of this software.
        /
        /----------------------------------------------------------------------------*/

        public static FF Current => Factory();

        private static FF instance;

        private static FF Factory()
        {
            if (instance == null)
            {
                instance = new FF();
            }
            return instance;
        }

        #region High level defines
        static int FF_VOLUMES = 1;
        static int FF_FS_EXFAT = 0;
        static int FF_FS_RPATH = 0;
        #endregion

        #region  Original C include file definition

        public const int FF_MAX_SS = 512;

        public class FATFS
        {

            public byte fs_type;       /* Filesystem type (0:N/A) */
            public byte pdrv;          /* Physical drive number */
            public byte n_fats;        /* Number of FATs (1 or 2) */
            public byte wflag;         /* win[] flag (b0:dirty) */
            public byte fsi_flag;      /* FSINFO flags (b7:disabled, b0:dirty) */
            public byte id;            /* Volume mount ID */
            public uint n_rootdir;     /* Number of root directory entries (FAT12/16) */
            public uint csize;         /* Cluster size [sectors] */
            public uint last_clst;     /* Last allocated cluster */
            public uint free_clst;     /* Number of free clusters */
            public uint n_fatent;      /* Number of FAT entries (number of clusters + 2) */
            public uint fsize;         /* Size of an FAT [sectors] */
            public uint volbase;       /* Volume base sector */
            public uint fatbase;       /* FAT base sector */
            public uint dirbase;       /* Root directory base sector/cluster */
            public uint database;      /* Data base sector */
            public uint winsect;       /* Current sector appearing in the win[] */
            public byte[] win;         /* Disk access window for Directory, FAT (and file data at tiny cfg) */

            public FATFS()
            {
                win = new byte[FF_MAX_SS];
            }
        }

        /* Object ID and allocation information (FFOBJID) */

        public class FFOBJID
        {
            public FATFS fs;           /* Pointer to the hosting volume of this object */
            public uint id;            /* Hosting volume mount ID */
            public byte attr;          /* Object attribute */
            public byte stat;          /* Object chain status (b1-0: =0:not contiguous, =2:contiguous, =3:flagmented in this session, b2:sub-directory stretched) */
            public uint sclust;        /* Object data start cluster (0:no cluster or root directory) */
            public uint objsize;       /* Object size (valid when sclust != 0) */

            public FFOBJID()
            {
                fs = new FATFS();
            }

            internal FFOBJID Clone(FATFS fs)
            {
                var clone = (FFOBJID)this.MemberwiseClone();
                clone.fs = fs;
                return clone;
            }
        }

        /* File object structure (FIL) */

        public class FIL
        {
            public FFOBJID obj;                 /* Object identifier (must be the 1st member to detect invalid object pointer) */
            public byte flag;                   /* File status flags */
            public byte err;                    /* Abort flag (error code) */
            public uint fptr;                   /* File read/write pointer (Zeroed on file open) */
            public uint clust;                  /* Current cluster of fpter (invalid when fptr is 0) */
            public uint sect;                   /* Sector number appearing in buf[] (0:invalid) */
            public byte[] buf;                  /* File private data read/write window */
            public uint dir_sect;               /* Sector number containing the directory entry (not used at exFAT) */
            public uint dir_ptrAsFsWinOffset;	/* Pointer to the directory entry in the win[] (not used at exFAT) */

            public FIL()
            {
                obj = new FFOBJID();
                buf = new byte[FF_MAX_SS];
            }
        }

        /* Directory object structure (DIR) */

        public class DIR
        {
            public FFOBJID obj;                 /* Object identifier */
            public uint dptr;                   /* Current read/write offset */
            public uint clust;                  /* Current cluster */
            public uint sect;                   /* Current sector (0:Read operation has terminated) */
            public uint dirAsFsWinOffset;	    // Changed: Offset from Fs.win to directory item       /* Pointer to the directory item in the win[] */
            public byte[] fn;                   /* SFN (in/out) {body[8],ext[3],status[1]} */

            public DIR()
            {
                fn = new byte[12];
                obj = new FFOBJID();
            }

            internal DIR Clone(FATFS fs)
            {
                var clone = new DIR
                {
                    obj = this.obj.Clone(fs),
                    dptr = this.dptr,
                    clust = this.clust,
                    sect = this.sect,
                    dirAsFsWinOffset = this.dirAsFsWinOffset,
                };
                Array.Copy(clone.fn, this.fn, fn.Length);
                return clone;
            }
        }


        /* File information structure (FILINFO) */

        public class FILINFO
        {

            public uint fsize;     /* File size */
            public uint fdate;     /* Modified date */
            public uint ftime;     /* Modified time */
            public byte fattrib;   /* File attribute */
            public byte[] fname;   /* File name */

            public FILINFO()
            {
                fname = new byte[12 + 1];
            }
        }

        /* File function return code (FRESULT) */

        public enum FRESULT
        {
            FR_OK = 0,              /* (0) Succeeded */
            FR_DISK_ERR,            /* (1) A hard error occurred in the low level disk I/O layer */
            FR_INT_ERR,             /* (2) Assertion failed */
            FR_NOT_READY,           /* (3) The physical drive cannot work */
            FR_NO_FILE,             /* (4) Could not find the file */
            FR_NO_PATH,             /* (5) Could not find the path */
            FR_INVALID_NAME,        /* (6) The path name format is invalid */
            FR_DENIED,              /* (7) Access denied due to prohibited access or directory full */
            FR_EXIST,               /* (8) Access denied due to prohibited access */
            FR_INVALID_OBJECT,      /* (9) The file/directory object is invalid */
            FR_WRITE_PROTECTED,     /* (10) The physical drive is write protected */
            FR_INVALID_DRIVE,       /* (11) The logical drive number is invalid */
            FR_NOT_ENABLED,         /* (12) The volume has no work area */
            FR_NO_FILESYSTEM,       /* (13) There is no valid FAT volume */
            FR_MKFS_ABORTED,        /* (14) The f_mkfs() aborted due to any problem */
            FR_TIMEOUT,             /* (15) Could not get a grant to access the volume within defined period */
            FR_LOCKED,              /* (16) The operation is rejected according to the file sharing policy */
            FR_NOT_ENOUGH_CORE,     /* (17) LFN working buffer could not be allocated */
            FR_TOO_MANY_OPEN_FILES, /* (18) Number of open files > FF_FS_LOCK */
            FR_INVALID_PARAMETER,   /* (19) Given parameter is invalid */

        }

        const int EOF = (-1);

        /*--------------------------------------------------------------*/
        /* Flags and offset address                                     */


        /* File access mode and open method flags (3rd argument of f_open) */
        public const byte FA_READ = 0x01;
        public const byte FA_WRITE = 0x02;
        public const byte FA_OPEN_EXISTING = 0x00;
        public const byte FA_CREATE_NEW = 0x04;
        public const byte FA_CREATE_ALWAYS = 0x08;
        public const byte FA_OPEN_ALWAYS = 0x10;
        public const byte FA_OPEN_APPEND = 0x30;

        /* Fast seek controls (2nd argument of f_lseek) */


        /* Format options (2nd argument of f_mkfs) */
        const byte FM_FAT = 0x01;
        const byte FM_FAT32 = 0x02;
        const byte FM_EXFAT = 0x04;
        const byte FM_ANY = 0x07;
        const byte FM_SFD = 0x08;

        /* Filesystem type (FATFS.fs_type) */
        const byte FS_FAT12 = 1;
        const byte FS_FAT16 = 2;
        const byte FS_FAT32 = 3;
        const byte FS_EXFAT = 4;

        /* File attribute bits for directory entry (FILINFO.fattrib) */
        public const byte AM_RDO = 0x01;   /* Read only */
        public const byte AM_HID = 0x02;   /* Hidden */
        public const byte AM_SYS = 0x04;   /* System */
        public const byte AM_DIR = 0x10;   /* Directory */
        public const byte AM_ARC = 0x20;   /* Archive */
        #endregion



        /* Character code support macros */
        static bool IsUpper(char c)
        {
            return ((c) >= 'A' && (c) <= 'Z');
        }

        static bool IsLower(char c)
        {
            return ((c) >= 'a' && (c) <= 'z');
        }

        static bool IsDigit(char c)
        {
            return ((c) >= '0' && (c) <= '9');
        }

        static bool IsSurrogate(char c)
        {
            return ((c) >= 0xD800 && (c) <= 0xDFFF);
        }

        static bool IsSurrogateH(char c)
        {
            return ((c) >= 0xD800 && (c) <= 0xDBFF);
        }

        static bool IsSurrogateL(char c)
        {
            return ((c) >= 0xDC00 && (c) <= 0xDFFF);
        }

        /* Additional file attribute bits for internal use */
        const byte AM_VOL = 0x08;   /* Volume label */
        const byte AM_LFN = 0x0F;   /* LFN entry */
        const byte AM_MASK = 0x3F;  /* Mask of defined bits */


        /* Additional file access control and file status flags for internal use */
        const byte FA_SEEKEND = 0x20;   /* Seek to end of the file on file open */
        const byte FA_MODIFIED = 0x40;  /* File has been modified */
        const byte FA_DIRTY = 0x80;     /* FIL.buf[] needs to be written-back */


        /* Name status flags in fn[11] */
        const byte NSFLAG = 11;     /* Index of the name status byte */
        const byte NS_LOSS = 0x01;  /* Out of 8.3 format */
        const byte NS_LFN = 0x02;   /* Force to create LFN entry */
        const byte NS_LAST = 0x04;  /* Last segment */
        const byte NS_BODY = 0x08;  /* Lower case flag (body) */
        const byte NS_EXT = 0x10;   /* Lower case flag (ext) */
        const byte NS_DOT = 0x20;   /* Dot entry */
        const byte NS_NOLFN = 0x40; /* Do not find LFN */
        const byte NS_NONAME = 0x80;/* Not followed */


        /* Limits and boundaries */
        const uint MAX_DIR = 0x200000;      /* Max size of FAT directory */
        const uint MAX_DIR_EX = 0x10000000; /* Max size of exFAT directory */
        const uint MAX_FAT12 = 0xFF5;       /* Max FAT12 clusters (differs from specs, but right for real DOS/Windows behavior) */
        const uint MAX_FAT16 = 0xFFF5;      /* Max FAT16 clusters (differs from specs, but right for real DOS/Windows behavior) */
        const uint MAX_FAT32 = 0x0FFFFFF5;  /* Max FAT32 clusters (not specified, practical limit) */
        const uint MAX_EXFAT = 0x7FFFFFFD;  /* Max exFAT clusters (differs from specs, implementation limit) */


        /* FatFs refers the FAT structure as simple byte array instead of structure member
        / because the C structure is not binary compatible between different platforms */

        const uint BS_JmpBoot = 0;          /* x86 jump instruction (3-byte) */
        const uint BS_OEMName = 3;          /* OEM name (8-byte) */
        const uint BPB_BytsPerSec = 11;     /* Sector size [byte] (WORD) */
        const uint BPB_SecPerClus = 13;     /* Cluster size [sector] (BYTE) */
        const uint BPB_RsvdSecCnt = 14;     /* Size of reserved area [sector] (WORD) */
        const uint BPB_NumFATs = 16;        /* Number of FATs (BYTE) */
        const uint BPB_RootEntCnt = 17;     /* Size of root directory area for FAT [entry] (WORD) */
        const uint BPB_TotSec16 = 19;       /* Volume size (16-bit) [sector] (WORD) */
        const uint BPB_Media = 21;          /* Media descriptor byte (BYTE) */
        const uint BPB_FATSz16 = 22;        /* FAT size (16-bit) [sector] (WORD) */
        const uint BPB_SecPerTrk = 24;      /* Number of sectors per track for int13h [sector] (WORD) */
        const uint BPB_NumHeads = 26;       /* Number of heads for int13h (WORD) */
        const uint BPB_HiddSec = 28;        /* Volume offset from top of the drive (DWORD) */
        const uint BPB_TotSec32 = 32;       /* Volume size (32-bit) [sector] (DWORD) */
        const uint BS_DrvNum = 36;          /* Physical drive number for int13h (BYTE) */
        const uint BS_NTres = 37;           /* WindowsNT error flag (BYTE) */
        const uint BS_BootSig = 38;         /* Extended boot signature (BYTE) */
        const uint BS_VolID = 39;           /* Volume serial number (DWORD) */
        const uint BS_VolLab = 43;          /* Volume label string (8-byte) */
        const uint BS_FilSysType = 54;      /* Filesystem type string (8-byte) */
        const uint BS_BootCode = 62;        /* Boot code (448-byte) */
        const uint BS_55AA = 510;           /* Signature word (WORD) */

        const uint BPB_FATSz32 = 36;        /* FAT32: FAT size [sector] (DWORD) */
        const uint BPB_ExtFlags32 = 40;     /* FAT32: Extended flags (WORD) */
        const uint BPB_FSVer32 = 42;        /* FAT32: Filesystem version (WORD) */
        const uint BPB_RootClus32 = 44;     /* FAT32: Root directory cluster (DWORD) */
        const uint BPB_FSInfo32 = 48;       /* FAT32: Offset of FSINFO sector (WORD) */
        const uint BPB_BkBootSec32 = 50;    /* FAT32: Offset of backup boot sector (WORD) */
        const uint BS_DrvNum32 = 64;        /* FAT32: Physical drive number for int13h (BYTE) */
        const uint BS_NTres32 = 65;         /* FAT32: Error flag (BYTE) */
        const uint BS_BootSig32 = 66;       /* FAT32: Extended boot signature (BYTE) */
        const uint BS_VolID32 = 67;         /* FAT32: Volume serial number (DWORD) */
        const uint BS_VolLab32 = 71;        /* FAT32: Volume label string (8-byte) */
        const uint BS_FilSysType32 = 82;    /* FAT32: Filesystem type string (8-byte) */
        const uint BS_BootCode32 = 90;      /* FAT32: Boot code (420-byte) */

        const uint BPB_ZeroedEx = 11;       /* exFAT: MBZ field (53-byte) */
        const uint BPB_VolOfsEx = 64;       /* exFAT: Volume offset from top of the drive [sector] (QWORD) */
        const uint BPB_TotSecEx = 72;       /* exFAT: Volume size [sector] (QWORD) */
        const uint BPB_FatOfsEx = 80;       /* exFAT: FAT offset from top of the volume [sector] (DWORD) */
        const uint BPB_FatSzEx = 84;        /* exFAT: FAT size [sector] (DWORD) */
        const uint BPB_DataOfsEx = 88;      /* exFAT: Data offset from top of the volume [sector] (DWORD) */
        const uint BPB_NumClusEx = 92;      /* exFAT: Number of clusters (DWORD) */
        const uint BPB_RootClusEx = 96;     /* exFAT: Root directory start cluster (DWORD) */
        const uint BPB_VolIDEx = 100;       /* exFAT: Volume serial number (DWORD) */
        const uint BPB_FSVerEx = 104;       /* exFAT: Filesystem version (WORD) */
        const uint BPB_VolFlagEx = 106;     /* exFAT: Volume flags (WORD) */
        const uint BPB_BytsPerSecEx = 108;  /* exFAT: Log2 of sector size in unit of byte (BYTE) */
        const uint BPB_SecPerClusEx = 109;  /* exFAT: Log2 of cluster size in unit of sector (BYTE) */
        const uint BPB_NumFATsEx = 110;     /* exFAT: Number of FATs (BYTE) */
        const uint BPB_DrvNumEx = 111;      /* exFAT: Physical drive number for int13h (BYTE) */
        const uint BPB_PercInUseEx = 112;   /* exFAT: Percent in use (BYTE) */
        const uint BPB_RsvdEx = 113;        /* exFAT: Reserved (7-byte) */
        const uint BS_BootCodeEx = 120;     /* exFAT: Boot code (390-byte) */

        const uint DIR_Name = 0;            /* Short file name (11-byte) */
        const uint DIR_Attr = 11;           /* Attribute (BYTE) */
        const uint DIR_NTres = 12;          /* Lower case flag (BYTE) */
        const uint DIR_CrtTime10 = 13;      /* Created time sub-second (BYTE) */
        const uint DIR_CrtTime = 14;        /* Created time (DWORD) */
        const uint DIR_LstAccDate = 18;     /* Last accessed date (WORD) */
        const uint DIR_FstClusHI = 20;      /* Higher 16-bit of first cluster (WORD) */
        const uint DIR_ModTime = 22;        /* Modified time (DWORD) */
        const uint DIR_FstClusLO = 26;      /* Lower 16-bit of first cluster (WORD) */
        const uint DIR_FileSize = 28;       /* File size (DWORD) */
        const uint LDIR_Ord = 0;            /* LFN: LFN order and LLE flag (BYTE) */
        const uint LDIR_Attr = 11;          /* LFN: LFN attribute (BYTE) */
        const uint LDIR_Type = 12;          /* LFN: Entry type (BYTE) */
        const uint LDIR_Chksum = 13;        /* LFN: Checksum of the SFN (BYTE) */
        const uint LDIR_FstClusLO = 26;     /* LFN: MBZ field (WORD) */
        const uint XDIR_Type = 0;           /* exFAT: Type of exFAT directory entry (BYTE) */
        const uint XDIR_NumLabel = 1;       /* exFAT: Number of volume label characters (BYTE) */
        const uint XDIR_Label = 2;          /* exFAT: Volume label (11-WORD) */
        const uint XDIR_CaseSum = 4;        /* exFAT: Sum of case conversion table (DWORD) */
        const uint XDIR_NumSec = 1;         /* exFAT: Number of secondary entries (BYTE) */
        const uint XDIR_SetSum = 2;         /* exFAT: Sum of the set of directory entries (WORD) */
        const uint XDIR_Attr = 4;           /* exFAT: File attribute (WORD) */
        const uint XDIR_CrtTime = 8;        /* exFAT: Created time (DWORD) */
        const uint XDIR_ModTime = 12;       /* exFAT: Modified time (DWORD) */
        const uint XDIR_AccTime = 16;       /* exFAT: Last accessed time (DWORD) */
        const uint XDIR_CrtTime10 = 20;     /* exFAT: Created time subsecond (BYTE) */
        const uint XDIR_ModTime10 = 21;     /* exFAT: Modified time subsecond (BYTE) */
        const uint XDIR_CrtTZ = 22;         /* exFAT: Created timezone (BYTE) */
        const uint XDIR_ModTZ = 23;         /* exFAT: Modified timezone (BYTE) */
        const uint XDIR_AccTZ = 24;         /* exFAT: Last accessed timezone (BYTE) */
        const uint XDIR_GenFlags = 33;      /* exFAT: General secondary flags (BYTE) */
        const uint XDIR_NumName = 35;       /* exFAT: Number of file name characters (BYTE) */
        const uint XDIR_NameHash = 36;      /* exFAT: Hash of file name (WORD) */
        const uint XDIR_ValidFileSize = 40; /* exFAT: Valid file size (QWORD) */
        const uint XDIR_FstClus = 52;       /* exFAT: First cluster of the file data (DWORD) */
        const uint XDIR_FileSize = 56;      /* exFAT: File/Directory size (QWORD) */

        const uint SZDIRE = 32;             /* Size of a directory entry */
        const byte DDEM = 0xE5;             /* Deleted directory entry mark set to DIR_Name[0] */
        const byte RDDEM = 0x05;            /* Replacement of the character collides with DDEM */
        const byte LLEF = 0x40;             /* Last long entry flag in LDIR_Ord */

        const uint FSI_LeadSig = 0;         /* FAT32 FSI: Leading signature (DWORD) */
        const uint FSI_StrucSig = 484;      /* FAT32 FSI: Structure signature (DWORD) */
        const uint FSI_Free_Count = 488;    /* FAT32 FSI: Number of free clusters (DWORD) */
        const uint FSI_Nxt_Free = 492;      /* FAT32 FSI: Last allocated cluster (DWORD) */

        const uint MBR_Table = 446;         /* MBR: Offset of partition table in the MBR */
        const uint SZ_PTE = 16;             /* MBR: Size of a partition table entry */
        const uint PTE_Boot = 0;            /* MBR PTE: Boot indicator */
        const uint PTE_StHead = 1;          /* MBR PTE: Start head */
        const uint PTE_StSec = 2;           /* MBR PTE: Start sector */
        const uint PTE_StCyl = 3;           /* MBR PTE: Start cylinder */
        const uint PTE_System = 4;          /* MBR PTE: System ID */
        const uint PTE_EdHead = 5;          /* MBR PTE: End head */
        const uint PTE_EdSec = 6;           /* MBR PTE: End sector */
        const uint PTE_EdCyl = 7;           /* MBR PTE: End cylinder */
        const uint PTE_StLba = 8;           /* MBR PTE: Start in LBA */
        const uint PTE_SizLba = 12;         /* MBR PTE: Size in LBA */

        /*--------------------------------------------------------------------------

           Module Private Work Area

        ---------------------------------------------------------------------------*/
        /* Remark: Variables defined here without initial value shall be guaranteed
        /  zero/null at start-up. If not, the linker option or start-up routine is
        /  not compliance with C standard. */

        /*--------------------------------*/
        /* File/Volume controls           */
        /*--------------------------------*/

        static FATFS[] FatFs = new FATFS[FF_VOLUMES];   /* Pointer to the filesystem objects (logical drives) */
        static byte Fsid;					            /* Filesystem mount ID */
        static string[] VolumeStr = { "RAM", "NAND", "CF", "SD", "SD2", "USB", "USB2", "USB3" }; /* Pre-defined volume ID */

        /* Disk Status Bits (DSTATUS) */
        const byte STA_NOINIT = 0x01;   /* Drive not initialized */
        const byte STA_NODISK = 0x02;   /* No medium in the drive */
        const byte STA_PROTECT = 0x04;	/* Write protected */

        /*--------------------------------------------------------------------------

           Module Private Functions

        ---------------------------------------------------------------------------*/


        /*-----------------------------------------------------------------------*/
        /* Load/Store multi-byte word in the FAT structure                       */
        /*-----------------------------------------------------------------------*/

        static uint ld_word(byte[] ptr, uint offs)	/*	 Load a 2-byte little-endian word */
        {

            uint rv;

            rv = ptr[1 + offs];
            rv = rv << 8 | ptr[0 + offs];
            return rv;
        }

        static uint ld_dword(byte[] ptr, uint offs)	/* Load a 4-byte little-endian word */
        {

            uint rv;

            rv = ptr[3 + offs];
            rv = rv << 8 | ptr[2 + offs];
            rv = rv << 8 | ptr[1 + offs];
            rv = rv << 8 | ptr[0 + offs];
            return rv;
        }

        static void st_word(ref byte[] ptr, uint offs, uint val)    /* Store a 2-byte word in little-endian */
        {
            ptr[0 + offs] = (byte)val; val >>= 8;
            ptr[1 + offs] = (byte)val;
        }

        static void st_dword(ref byte[] ptr, uint offs, uint val)  /* Store a 4-byte word in little-endian */
        {
            ptr[0 + offs] = (byte)val; val >>= 8;
            ptr[1 + offs] = (byte)val; val >>= 8;
            ptr[2 + offs] = (byte)val; val >>= 8;
            ptr[3 + offs] = (byte)val;
        }

        /* Copy memory to memory */
        static void mem_cpy(ref byte[] dst, byte[] src, uint cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                dst[i] = src[i];
            }
        }

        static void mem_cpy(ref byte[] dst, int dstOffset, byte[] src, uint cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                dst[i + dstOffset] = src[i];
            }
        }

        static void mem_cpy(ref byte[] dst, int dstOffset, byte[] src, int srcOffset, uint cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                dst[i + dstOffset] = src[i + srcOffset];
            }
        }


        /* Fill memory block */
        static void mem_set(ref byte[] dst, int val, uint cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                dst[i] = (byte)val;
            }
        }

        static void mem_set(ref byte[] dst, int dstOffset, int val, uint cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                dst[i + dstOffset] = (byte)val;
            }
        }

        /* Compare memory to memory */
        static int mem_cmp(byte[] dst, byte[] src, int cnt)
        {
            int dIndex = 0, sIndex = 0;
            byte d, s;
            int r = 0;
            do
            {
                d = dst[dIndex++]; s = src[sIndex++];
                r = d - s;
            }
            while (--cnt > 0 && (r == 0));
            return r;
        }

        /* Test if the character is DBC 1st byte */
        static int dbc_1st(byte c)
        {
            /* SBCS fixed code page */
            if (c != 0) return 0;	/* Always false */
            return 0;
        }

        /* Test if the character is DBC 2nd byte */
        static int dbc_2nd(byte c)
        {
            /* SBCS fixed code page */
            if (c != 0) return 0;	/* Always false */
            return 0;
        }

        const uint FF_FS_NORTC = 1;
        const uint FF_NORTC_MON = 1;
        const uint FF_NORTC_MDAY = 1;
        const uint FF_NORTC_YEAR = 2018;

        /* Check if chr is contained in the string */
        static int chk_chr(string str, byte chr)	/* NZ:contained, ZR:not contained */
        {
            for (int i = 0; i < str.Length; i++)
            {
                if ((byte)str[i] == chr)
                {
                    return 1;
                }
            }
            return 0;
        }

        uint GET_FATTIME() => ((uint)(FF_NORTC_YEAR - 1980) << 25 | (uint)FF_NORTC_MON << 21 | (uint)FF_NORTC_MDAY << 16);

        /*-----------------------------------------------------------------------*/
        /* Move/Flush disk access window in the filesystem object                */
        /*-----------------------------------------------------------------------*/

        static FRESULT sync_window( /* Returns FR_OK or FR_DISK_ERR */
            ref FATFS fs           /* Filesystem object */
        )
        {
            FRESULT res = FRESULT.FR_OK;


            if (fs.wflag > 0)
            {   /* Is the disk access window dirty */
                if (DiskIO.disk_write(fs.pdrv, fs.win, fs.winsect, 1) == DiskIO.DRESULT.RES_OK)
                {   /* Write back the window */
                    fs.wflag = 0;  /* Clear window dirty flag */
                    if (fs.winsect - fs.fatbase < fs.fsize)
                    {   /* Is it in the 1st FAT? */
                        if (fs.n_fats == 2) DiskIO.disk_write(fs.pdrv, fs.win, fs.winsect + fs.fsize, 1); /* Reflect it to 2nd FAT if needed */
                    }
                }
                else
                {
                    res = FRESULT.FR_DISK_ERR;
                }
            }
            return res;
        }


        static FRESULT move_window( /* Returns FR_OK or FR_DISK_ERR */
            ref FATFS fs,          /* Filesystem object */
            uint sector		/* Sector number to make appearance in the fs.win[] */
        )
        {
            FRESULT res = FRESULT.FR_OK;


            if (sector != fs.winsect)
            {   /* Window offset changed? */

                res = sync_window(ref fs);      /* Write-back changes */

                if (res == FRESULT.FR_OK)
                {
                    /* Fill sector window with new data */
                    if (DiskIO.disk_read(fs.pdrv, ref fs.win, sector, 1) != DiskIO.DRESULT.RES_OK)
                    {
                        sector = 0xFFFFFFFF;    /* Invalidate window if read data is not valid */
                        res = FRESULT.FR_DISK_ERR;
                    }
                    fs.winsect = sector;
                }
            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Synchronize filesystem and data on the storage                        */
        /*-----------------------------------------------------------------------*/

        static uint SS(FATFS fs) => FF_MAX_SS;

        static FRESULT sync_fs( /* Returns FR_OK or FR_DISK_ERR */
            ref FATFS fs       /* Filesystem object */
        )
        {
            FRESULT res;
            byte[] dummy = new byte[1];

            res = sync_window(ref fs);
            if (res == FRESULT.FR_OK)
            {
                if (fs.fs_type == FS_FAT32 && fs.fsi_flag == 1)
                {   /* FAT32: Update FSInfo sector if needed */
                    /* Create FSInfo structure */
                    mem_set(ref fs.win, 0, SS(fs));
                    st_word(ref fs.win, BS_55AA, 0xAA55);
                    st_dword(ref fs.win, FSI_LeadSig, 0x41615252);
                    st_dword(ref fs.win, FSI_StrucSig, 0x61417272);
                    st_dword(ref fs.win, FSI_Free_Count, fs.free_clst);
                    st_dword(ref fs.win, FSI_Nxt_Free, fs.last_clst);
                    /* Write it into the FSInfo sector */
                    fs.winsect = fs.volbase + 1;
                    DiskIO.disk_write(fs.pdrv, fs.win, fs.winsect, 1);
                    fs.fsi_flag = 0;
                }
                /* Make sure that no pending write process in the lower layer */
                if (DiskIO.disk_ioctl(fs.pdrv, DiskIO.CTRL_SYNC, ref dummy) != DiskIO.DRESULT.RES_OK) res = FRESULT.FR_DISK_ERR;
            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Get physical sector number from cluster number                        */
        /*-----------------------------------------------------------------------*/

        static uint clst2sect( /* !=0:Sector number, 0:Failed (invalid cluster#) */
            FATFS fs,      /* Filesystem object */
            uint clst      /* Cluster# to be converted */
        )
        {
            clst -= 2;      /* Cluster number is origin from 2 */
            if (clst >= fs.n_fatent - 2) return 0;     /* Is it invalid cluster number? */
            return fs.database + fs.csize * clst;     /* Start sector number of the cluster */
        }

        /*-----------------------------------------------------------------------*/
        /* FAT access - Read value of a FAT entry                                */
        /*-----------------------------------------------------------------------*/

        static uint get_fat(       /* 0xFFFFFFFF:Disk error, 1:Internal error, 2..0x7FFFFFFF:Cluster status */
            ref FFOBJID obj,   /* Corresponding object */
            uint clst      /* Cluster number to get the value */
        )
        {
            uint wc, bc;
            uint val;
            FATFS fs = obj.fs;


            if (clst < 2 || clst >= fs.n_fatent)
            {   /* Check if in valid range */
                val = 1;    /* Internal error */

            }
            else
            {
                val = 0xFFFFFFFF;   /* Default value falls on disk error */

                switch (fs.fs_type)
                {
                    case FS_FAT12:
                        bc = clst; bc += bc / 2;
                        if (move_window(ref fs, fs.fatbase + (bc / SS(fs))) != FRESULT.FR_OK) break;
                        wc = fs.win[bc++ % SS(fs)];        /* Get 1st byte of the entry */
                        if (move_window(ref fs, fs.fatbase + (bc / SS(fs))) != FRESULT.FR_OK) break;
                        wc |= ((uint)fs.win[bc % SS(fs)] << 8);    /* Merge 2nd byte of the entry */
                        val = ((clst & 1) > 1) ? (wc >> 4) : (wc & 0xFFF);    /* Adjust bit position */
                        break;

                    case FS_FAT16:
                        if (move_window(ref fs, fs.fatbase + (clst / (SS(fs) / 2))) != FRESULT.FR_OK) break;
                        val = ld_word(fs.win, clst * 2 % SS(fs));     /* Simple WORD array */
                        break;

                    case FS_FAT32:
                        if (move_window(ref fs, fs.fatbase + (clst / (SS(fs) / 4))) != FRESULT.FR_OK) break;
                        val = ld_dword(fs.win, clst * 4 % SS(fs)) & 0x0FFFFFFF;   /* Simple DWORD array but mask out upper 4 bits */
                        break;
                    default:
                        val = 1;    /* Internal error */
                        break;
                }
            }

            return val;
        }

        /*-----------------------------------------------------------------------*/
        /* FAT access - Change value of a FAT entry                              */
        /*-----------------------------------------------------------------------*/

        static FRESULT put_fat( /* FR_OK(0):succeeded, !=0:error */
            ref FATFS fs,      /* Corresponding filesystem object */
            uint clst,     /* FAT index number (cluster number) to be changed */
            uint val       /* New value to be set to the entry */
        )
        {
            uint bc;
            byte[] p = new byte[1];
            FRESULT res = FRESULT.FR_INT_ERR;


            if (clst >= 2 && clst < fs.n_fatent)
            {   /* Check if in valid range */
                switch (fs.fs_type)
                {
                    case FS_FAT12:
                        bc = (uint)clst; bc += bc / 2;  /* bc: byte offset of the entry */
                        res = move_window(ref fs, fs.fatbase + (bc / SS(fs)));
                        if (res != FRESULT.FR_OK) break;
                        p[0] = fs.win[bc++ % SS(fs)];
                        p[0] = (byte)(((clst & 1) > 1) ? ((p[0] & 0x0F) | ((byte)val << 4)) : (byte)val);     /* Put 1st byte */
                        fs.wflag = 1;
                        res = move_window(ref fs, fs.fatbase + (bc / SS(fs)));
                        if (res != FRESULT.FR_OK) break;
                        p[0] = fs.win[bc % SS(fs)];
                        p[0] = (byte)(((clst & 1) > 1) ? (byte)(val >> 4) : ((p[0] & 0xF0) | ((byte)(val >> 8) & 0x0F))); /* Put 2nd byte */
                        fs.wflag = 1;
                        break;

                    case FS_FAT16:
                        res = move_window(ref fs, fs.fatbase + (clst / (SS(fs) / 2)));
                        if (res != FRESULT.FR_OK) break;
                        st_word(ref fs.win, clst * 2 % SS(fs), (uint)val);    /* Simple WORD array */
                        fs.wflag = 1;
                        break;

                    case FS_FAT32:
                        res = move_window(ref fs, fs.fatbase + (clst / (SS(fs) / 4)));
                        if (res != FRESULT.FR_OK) break;
                        if (fs.fs_type != FS_EXFAT)
                        {
                            val = (val & 0x0FFFFFFF) | (ld_dword(fs.win, clst * 4 % SS(fs)) & 0xF0000000);
                        }
                        st_dword(ref fs.win, clst * 4 % SS(fs), val);
                        fs.wflag = 1;
                        break;
                }
            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* FAT handling - Remove a cluster chain                                 */
        /*-----------------------------------------------------------------------*/

        static FRESULT remove_chain(    /* FR_OK(0):succeeded, !=0:error */
            ref FFOBJID obj,       /* Corresponding object */
            uint clst,         /* Cluster to remove a chain from */
            uint pclst         /* Previous cluster of clst (0:entire chain) */
        )
        {
            FRESULT res = FRESULT.FR_OK;
            uint nxt;
            FATFS fs = obj.fs;

            if (clst < 2 || clst >= fs.n_fatent) return FRESULT.FR_INT_ERR;    /* Check if in valid range */

            /* Mark the previous cluster 'EOC' on the FAT if it exists */
            if (pclst != 0 && (fs.fs_type != FS_EXFAT || obj.stat != 2))
            {
                res = put_fat(ref fs, pclst, 0xFFFFFFFF);
                if (res != FRESULT.FR_OK) return res;
            }

            /* Remove the chain */
            do
            {
                nxt = get_fat(ref obj, clst);           /* Get cluster status */
                if (nxt == 0) break;                /* Empty cluster? */
                if (nxt == 1) return FRESULT.FR_INT_ERR;    /* Internal error? */
                if (nxt == 0xFFFFFFFF) return FRESULT.FR_DISK_ERR;  /* Disk error? */
                if (fs.fs_type != FS_EXFAT)
                {
                    res = put_fat(ref fs, clst, 0);     /* Mark the cluster 'free' on the FAT */
                    if (res != FRESULT.FR_OK) return res;
                }
                if (fs.free_clst < fs.n_fatent - 2)
                {   /* Update FSINFO */
                    fs.free_clst++;
                    fs.fsi_flag |= 1;
                }
                clst = nxt;                 /* Next cluster */
            } while (clst < fs.n_fatent);  /* Repeat while not the last link */
            return FRESULT.FR_OK;
        }

        /*-----------------------------------------------------------------------*/
        /* FAT handling - Stretch a chain or Create a new chain                  */
        /*-----------------------------------------------------------------------*/

        static uint create_chain(  /* 0:No free cluster, 1:Internal error, 0xFFFFFFFF:Disk error, >=2:New cluster# */
            ref FFOBJID obj,       /* Corresponding object */
            uint clst          /* Cluster# to stretch, 0:Create a new chain */
        )
        {
            uint cs, ncl, scl;
            FRESULT res;
            FATFS fs = obj.fs;


            if (clst == 0)
            {   /* Create a new chain */
                scl = fs.last_clst;                /* Suggested cluster to start to find */
                if (scl == 0 || scl >= fs.n_fatent) scl = 1;
            }
            else
            {
                /* Stretch a chain */
                cs = get_fat(ref obj, clst);            /* Check the cluster status */
                if (cs < 2) return 1;               /* Test for insanity */
                if (cs == 0xFFFFFFFF) return cs;    /* Test for disk error */
                if (cs < fs.n_fatent) return cs;   /* It is already followed by next cluster */
                scl = clst;                         /* Cluster to start to find */
            }
            if (fs.free_clst == 0) return 0;       /* No free cluster */
            {   /* On the FAT/FAT32 volume */
                ncl = 0;
                if (scl == clst)
                {                       /* Stretching an existing chain? */
                    ncl = scl + 1;                      /* Test if next cluster is free */
                    if (ncl >= fs.n_fatent) ncl = 2;
                    cs = get_fat(ref obj, ncl);             /* Get next cluster status */
                    if (cs == 1 || cs == 0xFFFFFFFF) return cs; /* Test for error */
                    if (cs != 0)
                    {                       /* Not free? */
                        cs = fs.last_clst;             /* Start at suggested cluster if it is valid */
                        if (cs >= 2 && cs < fs.n_fatent) scl = cs;
                        ncl = 0;
                    }
                }
                if (ncl == 0)
                {   /* The new cluster cannot be contiguous and find another fragment */
                    ncl = scl;  /* Start cluster */
                    for (; ; )
                    {
                        ncl++;                          /* Next cluster */
                        if (ncl >= fs.n_fatent)
                        {
                            /* Check wrap-around */
                            ncl = 2;
                            if (ncl > scl) return 0;    /* No free cluster found? */
                        }
                        cs = get_fat(ref obj, ncl);         /* Get the cluster status */
                        if (cs == 0) break;             /* Found a free cluster? */
                        if (cs == 1 || cs == 0xFFFFFFFF) return cs; /* Test for error */
                        if (ncl == scl) return 0;       /* No free cluster found? */
                    }
                }
                res = put_fat(ref fs, ncl, 0xFFFFFFFF);     /* Mark the new cluster 'EOC' */
                if (res == FRESULT.FR_OK && clst != 0)
                {
                    res = put_fat(ref fs, clst, ncl);       /* Link it from the previous one if needed */
                }
            }

            if (res == FRESULT.FR_OK)
            {
                /* Update FSINFO if function succeeded. */
                fs.last_clst = ncl;
                if (fs.free_clst <= fs.n_fatent - 2) fs.free_clst--;
                fs.fsi_flag |= 1;
            }
            else
            {
                ncl = (res == FRESULT.FR_DISK_ERR) ? 0xFFFFFFFF : 1;    /* Failed. Generate error status */
            }

            return ncl;     /* Return new cluster number or error status */
        }

        /*-----------------------------------------------------------------------*/
        /* Directory handling - Fill a cluster with zeros                        */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_clear(   /* Returns FR_OK or FR_DISK_ERR */
            ref FATFS fs,      /* Filesystem object */
            uint clst      /* Directory table to clear */
        )
        {
            uint sect;
            uint n, szb;
            byte[] ibuf;


            if (sync_window(ref fs) != FRESULT.FR_OK) return FRESULT.FR_DISK_ERR;   /* Flush disk access window */
            sect = clst2sect(fs, clst);     /* Top of the cluster */
            fs.winsect = sect;             /* Set window to top of the cluster */
            mem_set(ref fs.win, 0, SS(fs));    /* Clear window buffer */
            {
                ibuf = fs.win; szb = 1;    /* Use window buffer (many single-sector writes may take a time) */
                for (n = 0; n < fs.csize && DiskIO.disk_write(fs.pdrv, ibuf, sect + n, szb) == DiskIO.DRESULT.RES_OK; n += szb) ;   /* Fill the cluster with 0 */
            }
            return (n == fs.csize) ? FRESULT.FR_OK : FRESULT.FR_DISK_ERR;
        }

        /*-----------------------------------------------------------------------*/
        /* Directory handling - Set directory index                              */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_sdi( /* FR_OK(0):succeeded, !=0:error */
            ref DIR dp,        /* Pointer to directory object */
            uint ofs       /* Offset of directory table */
        )
        {
            uint csz, clst;
            FATFS fs = dp.obj.fs;


            if (ofs >= MAX_DIR || ofs % SZDIRE > 0)
            {   /* Check range of offset and alignment */
                return FRESULT.FR_INT_ERR;
            }
            dp.dptr = ofs;             /* Set current offset */
            clst = dp.obj.sclust;      /* Table start cluster (0:root) */
            if (clst == 0 && fs.fs_type >= FS_FAT32)
            {   /* Replace cluster# 0 with root cluster# */
                clst = fs.dirbase;
            }

            if (clst == 0)
            {   /* Static table (root-directory on the FAT volume) */
                if (ofs / SZDIRE >= fs.n_rootdir) return FRESULT.FR_INT_ERR;   /* Is index out of range? */
                dp.sect = fs.dirbase;
            }
            else
            {
                /* Dynamic table (sub-directory or root-directory on the FAT32/exFAT volume) */
                csz = (uint)fs.csize * SS(fs);    /* Bytes per cluster */
                while (ofs >= csz)
                {
                    /* Follow cluster chain */
                    clst = get_fat(ref dp.obj, clst);             /* Get next cluster */
                    if (clst == 0xFFFFFFFF) return FRESULT.FR_DISK_ERR; /* Disk error */
                    if (clst < 2 || clst >= fs.n_fatent) return FRESULT.FR_INT_ERR;    /* Reached to end of table or internal error */
                    ofs -= csz;
                }
                dp.sect = clst2sect(fs, clst);
            }
            dp.clust = clst;                   /* Current cluster# */
            if (dp.sect == 0) return FRESULT.FR_INT_ERR;
            dp.sect += ofs / SS(fs);           /* Sector# of the directory entry */
            dp.dirAsFsWinOffset = (ofs % SS(fs)); // New: fs.win offset to the entry /* Pointer to the entry in the win[] */

            return FRESULT.FR_OK;
        }


        /*-----------------------------------------------------------------------*/
        /* Directory handling - Move directory table index next                  */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_next(    /* FR_OK(0):succeeded, FR_NO_FILE:End of table, FR_DENIED:Could not stretch */
            ref DIR dp,                /* Pointer to the directory object */
            int stretch             /* 0: Do not stretch table, 1: Stretch table if needed */
        )
        {
            uint ofs, clst;
            FATFS fs = dp.obj.fs;


            ofs = dp.dptr + SZDIRE;    /* Next entry */
            if (dp.sect == 0 || ofs >= MAX_DIR) return FRESULT.FR_NO_FILE;    /* Report EOT when offset has reached max value */

            if (ofs % SS(fs) == 0)
            {   /* Sector changed? */
                dp.sect++;             /* Next sector */

                if (dp.clust == 0)
                {   /* Static table */
                    if (ofs / SZDIRE >= fs.n_rootdir)
                    {   /* Report EOT if it reached end of static table */
                        dp.sect = 0; return FRESULT.FR_NO_FILE;
                    }
                }
                else
                {                   /* Dynamic table */
                    if ((ofs / SS(fs) & (fs.csize - 1)) == 0)
                    {   /* Cluster changed? */
                        clst = get_fat(ref dp.obj, dp.clust);        /* Get next cluster */
                        if (clst <= 1) return FRESULT.FR_INT_ERR;           /* Internal error */
                        if (clst == 0xFFFFFFFF) return FRESULT.FR_DISK_ERR; /* Disk error */
                        if (clst >= fs.n_fatent)
                        {
                            /* It reached end of dynamic table */
                            if (stretch == 0)
                            {
                                /* If no stretch, report EOT */
                                dp.sect = 0; return FRESULT.FR_NO_FILE;
                            }
                            clst = create_chain(ref dp.obj, dp.clust);   /* Allocate a cluster */
                            if (clst == 0) return FRESULT.FR_DENIED;            /* No free cluster */
                            if (clst == 1) return FRESULT.FR_INT_ERR;           /* Internal error */
                            if (clst == 0xFFFFFFFF) return FRESULT.FR_DISK_ERR; /* Disk error */
                            if (dir_clear(ref fs, clst) != FRESULT.FR_OK) return FRESULT.FR_DISK_ERR;   /* Clean up the stretched table */
                        }
                        dp.clust = clst;       /* Initialize data for new cluster */
                        dp.sect = clst2sect(fs, clst);
                    }
                }
            }
            dp.dptr = ofs;                     /* Current entry */
            dp.dirAsFsWinOffset = ofs % SS(fs);   // New: Offset from fs.win to entry /* Pointer to the entry in the win[] */

            return FRESULT.FR_OK;
        }

        /*-----------------------------------------------------------------------*/
        /* Directory handling - Reserve a block of directory entries             */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_alloc(   /* FR_OK(0):succeeded, !=0:error */
            ref DIR dp,                /* Pointer to the directory object */
            uint nent               /* Number of contiguous entries to allocate */
        )
        {
            FRESULT res;
            uint n;
            FATFS fs = dp.obj.fs;


            res = dir_sdi(ref dp, 0);
            if (res == FRESULT.FR_OK)
            {
                n = 0;
                do
                {
                    res = move_window(ref fs, dp.sect);
                    if (res != FRESULT.FR_OK) break;

                    if (fs.win[dp.dirAsFsWinOffset + DIR_Name] == DDEM || fs.win[dp.dirAsFsWinOffset + DIR_Name] == 0)    // HB: Check if this works
                    {
                        if (++n == nent) break; /* A block of contiguous free entries is found */
                    }
                    else
                    {
                        n = 0;                  /* Not a blank entry. Restart to search */
                    }
                    res = dir_next(ref dp, 1);
                } while (res == FRESULT.FR_OK); /* Next entry with table stretch enabled */
            }

            if (res == FRESULT.FR_NO_FILE) res = FRESULT.FR_DENIED; /* No directory entry to allocate */
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* FAT: Directory handling - Load/Store start cluster number             */
        /*-----------------------------------------------------------------------*/

        static uint ld_clust(   /* Returns the top cluster value of the SFN entry */
            FATFS fs,			/* Pointer to the fs object */
            byte[] dir		    /* Pointer to the key entry */
        )
        {

            uint cl;

            cl = ld_word(dir, DIR_FstClusLO);
            if (fs.fs_type == FS_FAT32)
            {
                cl |= (uint)ld_word(dir, DIR_FstClusHI) << 16;
            }

            return cl;
        }

        static uint ld_clust(   /* Returns the top cluster value of the SFN entry */
            FATFS fs,           /* Pointer to the fs object */
            byte[] buff,          /* Pointer to the key entry */
            uint buffOffset      /* Offset into buff where to set cluster value */
        )
        {

            uint cl;

            cl = ld_word(buff, buffOffset + DIR_FstClusLO);
            if (fs.fs_type == FS_FAT32)
            {
                cl |= (uint)ld_word(buff, buffOffset + DIR_FstClusHI) << 16;
            }

            return cl;
        }



        static void st_clust(
            ref FATFS fs,  /* Pointer to the fs object */
            uint winDirOffset,  /* Pointer to the key entry */
            uint cl	/* Value to be set */
        )
        {
            st_word(ref fs.win, winDirOffset + DIR_FstClusLO, (uint)cl);
            if (fs.fs_type == FS_FAT32)
            {
                st_word(ref fs.win, winDirOffset + DIR_FstClusHI, (uint)(cl >> 16));
            }
        }



        /*-----------------------------------------------------------------------*/
        /* Directory handling - Find an object in the directory                  */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_find(    /* FR_OK(0):succeeded, !=0:error */
            ref DIR dp                 /* Pointer to the directory object with the file name */
        )
        {
            FRESULT res;
            FATFS fs = dp.obj.fs;
            byte c;

            res = dir_sdi(ref dp, 0);           /* Rewind directory object */
            if (res != FRESULT.FR_OK) return res;

            /* On the FAT/FAT32 volume */
            do
            {
                res = move_window(ref fs, dp.sect);
                if (res != FRESULT.FR_OK) break;
                c = fs.win[dp.dirAsFsWinOffset + DIR_Name]; // HB: Test this
                if (c == 0) { res = FRESULT.FR_NO_FILE; break; }    /* Reached to end of table */


                dp.obj.attr = (byte)(fs.win[dp.dirAsFsWinOffset + DIR_Attr] & AM_MASK); // HB: Test this
                byte[] fsFilename = new byte[11];
                Array.Copy(fs.win, (int)dp.dirAsFsWinOffset, fsFilename, 0, 11);
                if (((fs.win[dp.dirAsFsWinOffset + DIR_Attr] & AM_VOL) == 0) && mem_cmp(fsFilename, dp.fn, 11) == 0) break;  /* Is it a valid entry? */

                res = dir_next(ref dp, 0);  /* Next entry */
            } while (res == FRESULT.FR_OK);

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Read an object from the directory                                     */
        /*-----------------------------------------------------------------------*/

        public FRESULT dir_read_file(ref DIR dp) => dir_read(ref dp, 0);
        public FRESULT dir_read_label(ref DIR dp) => dir_read(ref dp, 1);

        static FRESULT dir_read(
            ref DIR dp,         /* Pointer to the directory object */
            int vol             /* Filtered by 0:file/directory or 1:volume label */
        )
        {
            FRESULT res = FRESULT.FR_NO_FILE;
            FATFS fs = dp.obj.fs;
            byte a, c;

            while (dp.sect > 0)
            {
                res = move_window(ref fs, dp.sect);
                if (res != FRESULT.FR_OK) break;
                c = fs.win[dp.dirAsFsWinOffset + DIR_Name];  /* Test for the entry type */
                if (c == 0)
                {
                    res = FRESULT.FR_NO_FILE; break; /* Reached to end of the directory */
                }


                dp.obj.attr = a = (byte)(fs.win[dp.dirAsFsWinOffset + DIR_Attr] & AM_MASK); /* Get attribute */

                if (c != DDEM && c != '.' && a != AM_LFN && (((a & ~AM_ARC) == AM_VOL) ? 1 : 0) == vol)   // HB : Changed
                {   /* Is it a valid entry? */
                    break;
                }


                res = dir_next(ref dp, 0);      /* Next entry */
                if (res != FRESULT.FR_OK) break;
            }

            if (res != FRESULT.FR_OK) dp.sect = 0;     /* Terminate the read operation on error or EOT */
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Register an object to the directory                                   */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_register(    /* FR_OK:succeeded, FR_DENIED:no free entry or too many SFN collision, FR_DISK_ERR:disk error */
            ref DIR dp                     /* Target directory with object name to be created */
        )
        {
            FRESULT res;
            FATFS fs = dp.obj.fs;

            res = dir_alloc(ref dp, 1);     /* Allocate an entry for SFN */

            /* Set SFN entry */
            if (res == FRESULT.FR_OK)
            {
                res = move_window(ref fs, dp.sect);
                if (res == FRESULT.FR_OK)
                {
                    mem_set(ref fs.win, (int)dp.dirAsFsWinOffset, 0, SZDIRE);    /* Clean the entry */
                    mem_cpy(ref fs.win, (int)(dp.dirAsFsWinOffset + DIR_Name), dp.fn, 11);    /* Put SFN */

                    fs.wflag = 1;
                }
            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Remove an object from the directory                                   */
        /*-----------------------------------------------------------------------*/

        static FRESULT dir_remove(  /* FR_OK:Succeeded, FR_DISK_ERR:A disk error */
            ref DIR dp                 /* Directory object pointing the entry to be removed */
        )
        {
            FRESULT res;
            FATFS fs = dp.obj.fs;

            res = move_window(ref fs, dp.sect);
            if (res == FRESULT.FR_OK)
            {
                fs.win[dp.dirAsFsWinOffset + DIR_Name] = DDEM;   /* Mark the entry 'deleted'.*/
                fs.wflag = 1;
            }
            return res;
        }


        /*-----------------------------------------------------------------------*/
        /* Get file information from directory entry                             */
        /*-----------------------------------------------------------------------*/

        static void get_fileinfo(
            DIR dp,            /* Pointer to the directory object */
            ref FILINFO fno        /* Pointer to the file information to be filled */
        )
        {
            uint si, di;
            byte c;
            FATFS fs = dp.obj.fs;

            fno.fname[0] = 0;          /* Invalidate file info */
            if (dp.sect == 0) return;  /* Exit if read pointer has reached end of directory */


            si = di = 0;
            while (si < 11)
            {
                /* Copy name body and extension */
                c = (byte)fs.win[dp.dirAsFsWinOffset + (si++)];
                if (c == ' ') continue;     /* Skip padding spaces */
                if (c == RDDEM) c = DDEM;   /* Restore replaced DDEM character */
                if (si == 9) fno.fname[di++] = (byte)'.';  /* Insert a . if extension is exist */
                fno.fname[di++] = c;
            }
            fno.fname[di] = 0;


            fno.fattrib = fs.win[dp.dirAsFsWinOffset + DIR_Attr];                   /* Attribute */
            fno.fsize = ld_dword(fs.win, dp.dirAsFsWinOffset + DIR_FileSize);      /* Size */
            fno.ftime = ld_word(fs.win, dp.dirAsFsWinOffset + DIR_ModTime + 0);    /* Time */
            fno.fdate = ld_word(fs.win, dp.dirAsFsWinOffset + DIR_ModTime + 2);    /* Date */
        }

        /*-----------------------------------------------------------------------*/
        /* Pick a top segment and create the object name in directory form       */
        /*-----------------------------------------------------------------------*/

        static FRESULT create_name( /* FR_OK: successful, FR_INVALID_NAME: could not create */
            ref DIR dp,				/* Pointer to the directory object */
            byte[] path,			/* Pointer to start of the path string */
            ref uint pathIndex      // Current offset in path (all before offset has been evaluated already)
        )
        {

            byte c, d;
            byte[] sfn;
            uint ni, si, i;

            /* Create file name in directory form */
            sfn = dp.fn;
            mem_set(ref sfn, ' ', 11);
            si = i = 0; ni = 8;
            for (; ; )
            {
                c = (byte)path[pathIndex + (si++)];             /* Get a byte */
                if (c <= ' ') break;            /* Break if end of the path name */
                if (c == '/' || c == '\\')
                {   /* Break if a separator is found */
                    while (path[pathIndex + si] == '/' || path[pathIndex + si] == '\\') si++;   /* Skip duplicated separator if exist */
                    break;
                }
                if (c == '.' || i >= ni)
                {       /* End of body or field overflow? */
                    if (ni == 11 || c != '.') return FRESULT.FR_INVALID_NAME;   /* Field overflow or invalid dot? */
                    i = 8; ni = 11;             /* Enter file extension field */
                    continue;
                }
                if (dbc_1st(c) > 0)
                {               /* Check if it is a DBC 1st byte */
                    d = (byte)path[pathIndex + (si++)];         /* Get 2nd byte */
                    if ((dbc_2nd(d) == 0) || i >= ni - 1) return FRESULT.FR_INVALID_NAME;   /* Reject invalid DBC */
                    sfn[i++] = c;
                    sfn[i++] = d;
                }
                else
                {
                    /* SBC */
                    if (chk_chr(@"\ * +,:;<=>\?[]|", c) > 0) return FRESULT.FR_INVALID_NAME;    /* Reject illegal chrs for SFN */
                    if (IsLower((char)c)) c -= 0x20;    /* To upper */
                    sfn[i++] = c;
                }
            }

            pathIndex = pathIndex + si;                     /* Return pointer to the next segment */
            if (i == 0) return FRESULT.FR_INVALID_NAME;     /* Reject nul string */

            if (sfn[0] == DDEM) sfn[0] = RDDEM; /* If the first character collides with DDEM, replace it with RDDEM */
            sfn[NSFLAG] = (c <= (byte)' ') ? NS_LAST : (byte)0;     /* Set last segment flag if end of the path */

            return FRESULT.FR_OK;

        }

        /*-----------------------------------------------------------------------*/
        /* Follow a file path                                                    */
        /*-----------------------------------------------------------------------*/

        static FRESULT follow_path( /* FR_OK(0): successful, !=0: error code */
            ref DIR dp,					/* Directory object to return last directory and found object */
            byte[] path,			/* Full-path string to find a file or directory */
            ref uint pathIndex      // Current offset in path (all before offset has been evaluated already)
        )
        {

            FRESULT res;
            byte ns;
            FATFS fs = dp.obj.fs;


            while (path[pathIndex] == '/' || path[pathIndex] == '\\') pathIndex++;  /* Strip heading separator */
            dp.obj.sclust = 0;                  /* Start from root directory */

            if (path[pathIndex] < ' ')
            {
                /* Null path name is the origin directory itself */
                dp.fn[NSFLAG] = NS_NONAME;
                res = dir_sdi(ref dp, 0);
            }
            else
            {
                /* Follow path */
                for (; ; )
                {
                    res = create_name(ref dp, path, ref pathIndex); /* Get a segment name of the path */
                    if (res != FRESULT.FR_OK) break;
                    res = dir_find(ref dp);             /* Find an object with the segment name */
                    ns = dp.fn[NSFLAG];
                    if (res != FRESULT.FR_OK)
                    {               /* Failed to find the object */
                        if (res == FRESULT.FR_NO_FILE)
                        {    /* Object is not found */
                            if ((ns & NS_LAST) == 0) res = FRESULT.FR_NO_PATH;  /* Adjust error code if not last segment */
                        }
                        break;
                    }
                    if ((ns & NS_LAST) > 0) break;          /* Last segment matched. Function completed. */
                                                            /* Get into the sub-directory */
                    if ((dp.obj.attr & AM_DIR) == 0)
                    {       /* It is not a sub-directory and cannot follow */
                        res = FRESULT.FR_NO_PATH; break;
                    }
                    dp.obj.sclust = ld_clust(fs, fs.win.SubArray(dp.dptr % SS(fs)));	/* Open next directory */ // HB: Check
                }
            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Get logical drive number from path name                               */
        /*-----------------------------------------------------------------------*/

        static int get_ldnumber(	/* Returns logical drive number (-1:invalid drive number or null pointer) */
            byte[] path,		/* Pointer to pointer to the path name */
            ref uint pathIndex
        )
        {

            uint tp, tt;
            char tc, c;
            int i, vol = -1;
            char[] sp;
            uint spIndex = 0;

            tt = tp = pathIndex;
            if (path.Length == 0) return vol; /* Invalid path name? */
            do
            {
                tc = (char)path[tt++];
            }
            while (tc >= '!' && tc != ':'); /* Find a colon in the path */

            if (tc == ':')
            {
                /* DOS/Windows style volume ID? */
                i = FF_VOLUMES;
                if (IsDigit((char)path[tp]) && tp + 2 == tt)
                {   /* Is there a numeric volume ID + colon? */
                    i = (int)path[tp] - '0';   /* Get the LD number */
                }
                else
                {
                    i = 0;
                    do
                    {
                        sp = VolumeStr[i].ToCharArray(); tp = pathIndex;    /* This string volume ID and path name */
                        do
                        {
                            /* Compare the volume ID with path name */
                            c = sp[spIndex++]; tc = (char)path[tp++];
                            if (IsLower(c)) c -= (char)0x20;
                            if (IsLower(tc)) tc -= (char)0x20;
                        } while (c > 0 && c == tc);
                    } while ((c > 0 || tp != tt) && ++i < FF_VOLUMES);  /* Repeat for each id until pattern match */
                }

                if (i < FF_VOLUMES)
                {
                    /* If a volume ID is found, get the drive number and strip it */
                    vol = i;		/* Drive number */
                    pathIndex = tt;     /* Snip the drive prefix off */
                }
                return vol;
            }
            /* No drive prefix is found */
            vol = 0;        /* Default drive is 0 */
            return vol;		/* Return the default drive */
        }

        /*-----------------------------------------------------------------------*/
        /* Load a sector and check if it is an FAT VBR                           */
        /*-----------------------------------------------------------------------*/

        static byte check_fs(   /* 0:FAT, 1:exFAT, 2:Valid BS but not FAT, 3:Not a BS, 4:Disk error */
            ref FATFS fs,          /* Filesystem object */
            uint sect          /* Sector# (lba) to load and check if it is an FAT-VBR or not */
        )
        {
            fs.wflag = 0; fs.winsect = 0xFFFFFFFF;        /* Invalidate window */
            if (move_window(ref fs, sect) != FRESULT.FR_OK) return 4;   /* Load boot record */

            if (ld_word(fs.win, BS_55AA) != 0xAA55) return 3; /* Check boot record signature (always here regardless of the sector size) */

            if (fs.win[BS_JmpBoot] == 0xE9 || fs.win[BS_JmpBoot] == 0xEB || fs.win[BS_JmpBoot] == 0xE8)
            {   /* Valid JumpBoot code? */
                if (mem_cmp(fs.win.Slice(BS_FilSysType, 3), Encoding.UTF8.GetBytes("FAT"), 3) == 0) return 0;      /* Is it an FAT VBR? */
                if (mem_cmp(fs.win.Slice(BS_FilSysType32, 5), Encoding.UTF8.GetBytes("FAT32"), 5) == 0) return 0;  /* Is it an FAT32 VBR? */
            }
            return 2;   /* Valid BS but not FAT */
        }

        /*-----------------------------------------------------------------------*/
        /* Determine logical drive number and mount the volume if needed         */
        /*-----------------------------------------------------------------------*/

        static FRESULT find_volume(	/* FR_OK(0): successful, !=0: an error occurred */

            ref byte[] path,            /* Pointer to pointer to the path name (drive number) */
            ref FATFS rfs,              /* Pointer to pointer to the found filesystem object */
            byte mode,					/* !=0: Check write protection for write access */
            string busId,
            int csPin
        )
        {

            byte fmt;
            byte[] pt;
            int vol;
            byte stat;
            uint bsect, fasize, tsect, sysect, nclst, szbfat;
            uint[] br = new uint[4];
            uint nrsv;
            FATFS fs;
            uint i;
            uint pathIndex = 0;


            /* Get logical drive number */
            rfs = null;
            vol = get_ldnumber(path, ref pathIndex);
            if (vol < 0) return FRESULT.FR_INVALID_DRIVE;

            /* Check if the filesystem object is valid or not */
            fs = FatFs[vol];                    /* Get pointer to the filesystem object */
            if (fs == null) return FRESULT.FR_NOT_ENABLED;      /* Is the filesystem object available? */

            rfs = fs;							/* Return pointer to the filesystem object */

            mode &= (byte)(~FA_READ & 0xff);                /* Desired access mode, write access or not */
            if (fs.fs_type != 0)
            {               /* If the volume has been mounted */
                stat = DiskIO.disk_status(fs.pdrv);
                if ((stat & STA_NOINIT) == 0)
                {
                    /* and the physical drive is kept initialized */
                    if (mode > 0 && (stat & STA_PROTECT) > 0)
                    {   /* Check write protection if needed */
                        return FRESULT.FR_WRITE_PROTECTED;
                    }
                    return FRESULT.FR_OK;               /* The filesystem object is valid */
                }
            }

            /* The filesystem object is not valid. */
            /* Following code attempts to mount the volume. (analyze BPB and initialize the filesystem object) */

            fs.fs_type = 0;                 /* Clear the filesystem object */
            fs.pdrv = (byte)vol;              /* Bind the logical drive and a physical drive */
            stat = DiskIO.disk_initialize(fs.pdrv, busId, csPin); /* Initialize the physical drive */
            if ((stat & STA_NOINIT) > 0)
            {           /* Check if the initialization succeeded */
                return FRESULT.FR_NOT_READY;            /* Failed to initialize due to no medium or hard error */
            }
            if (mode > 0 && (stat & STA_PROTECT) > 0)
            { /* Check disk write protection if needed */
                return FRESULT.FR_WRITE_PROTECTED;
            }


            /* Find an FAT partition on the drive. Supports only generic partitioning rules, FDISK and SFD. */
            bsect = 0;
            fmt = check_fs(ref fs, bsect);          /* Load sector 0 and check if it is an FAT-VBR as SFD */
            if (fmt == 2 || (fmt < 2 && (byte)(vol) != 0))
            {   /* Not an FAT-VBR or forced partition number */
                for (i = 0; i < 4; i++)
                {       /* Get partition offset */
                    pt = fs.win.SubArray(MBR_Table + i * SZ_PTE);
                    br[i] = (pt[PTE_System] > 0) ? ld_dword(pt, PTE_StLba) : 0;
                }
                i = (byte)(vol);                    /* Partition number: 0:auto, 1-4:forced */
                if (i != 0) i--;
                do
                {                           /* Find an FAT volume */
                    bsect = br[i];
                    fmt = (bsect > 0) ? check_fs(ref fs, bsect) : (byte)3;  /* Check the partition */
                } while ((byte)(vol) == 0 && fmt >= 2 && ++i < 4);
            }
            if (fmt == 4) return FRESULT.FR_DISK_ERR;       /* An error occured in the disk I/O layer */
            if (fmt >= 2) return FRESULT.FR_NO_FILESYSTEM;  /* No FAT volume is found */

            /* An FAT volume is found (bsect). Following code initializes the filesystem object */

            if (ld_word(fs.win, BPB_BytsPerSec) != SS(fs)) return FRESULT.FR_NO_FILESYSTEM; /* (BPB_BytsPerSec must be equal to the physical sector size) */

            fasize = ld_word(fs.win, BPB_FATSz16);      /* Number of sectors per FAT */
            if (fasize == 0) fasize = ld_dword(fs.win, BPB_FATSz32);
            fs.fsize = fasize;

            fs.n_fats = fs.win[BPB_NumFATs];                /* Number of FATs */
            if (fs.n_fats != 1 && fs.n_fats != 2) return FRESULT.FR_NO_FILESYSTEM;  /* (Must be 1 or 2) */
            fasize *= fs.n_fats;                            /* Number of sectors for FAT area */

            fs.csize = fs.win[BPB_SecPerClus];          /* Cluster size */
            if (fs.csize == 0 || (fs.csize & (fs.csize - 1)) > 0) return FRESULT.FR_NO_FILESYSTEM;  /* (Must be power of 2) */

            fs.n_rootdir = ld_word(fs.win, BPB_RootEntCnt); /* Number of root directory entries */
            if (fs.n_rootdir % (SS(fs) / SZDIRE) > 0) return FRESULT.FR_NO_FILESYSTEM;  /* (Must be sector aligned) */

            tsect = ld_word(fs.win, BPB_TotSec16);      /* Number of sectors on the volume */
            if (tsect == 0) tsect = ld_dword(fs.win, BPB_TotSec32);

            nrsv = ld_word(fs.win, BPB_RsvdSecCnt);     /* Number of reserved sectors */
            if (nrsv == 0) return FRESULT.FR_NO_FILESYSTEM;         /* (Must not be 0) */

            /* Determine the FAT sub type */
            sysect = nrsv + fasize + fs.n_rootdir / (SS(fs) / SZDIRE);  /* RSV + FAT + DIR */
            if (tsect < sysect) return FRESULT.FR_NO_FILESYSTEM;    /* (Invalid volume size) */
            nclst = (tsect - sysect) / fs.csize;            /* Number of clusters */
            if (nclst == 0) return FRESULT.FR_NO_FILESYSTEM;        /* (Invalid volume size) */
            fmt = 0;
            if (nclst <= MAX_FAT32) fmt = FS_FAT32;
            if (nclst <= MAX_FAT16) fmt = FS_FAT16;
            if (nclst <= MAX_FAT12) fmt = FS_FAT12;
            if (fmt == 0) return FRESULT.FR_NO_FILESYSTEM;

            /* Boundaries and Limits */
            fs.n_fatent = nclst + 2;                        /* Number of FAT entries */
            fs.volbase = bsect;                         /* Volume start sector */
            fs.fatbase = bsect + nrsv;                  /* FAT start sector */
            fs.database = bsect + sysect;                   /* Data start sector */
            if (fmt == FS_FAT32)
            {
                if (ld_word(fs.win, BPB_FSVer32) != 0) return FRESULT.FR_NO_FILESYSTEM; /* (Must be FAT32 revision 0.0) */
                if (fs.n_rootdir != 0) return FRESULT.FR_NO_FILESYSTEM; /* (BPB_RootEntCnt must be 0) */
                fs.dirbase = ld_dword(fs.win, BPB_RootClus32);   /* Root directory start cluster */
                szbfat = fs.n_fatent * 4;                   /* (Needed FAT size) */
            }
            else
            {
                if (fs.n_rootdir == 0) return FRESULT.FR_NO_FILESYSTEM; /* (BPB_RootEntCnt must not be 0) */
                fs.dirbase = fs.fatbase + fasize;           /* Root directory start sector */
                szbfat = (fmt == FS_FAT16) ?				/* (Needed FAT size) */

                    fs.n_fatent * 2 : fs.n_fatent * 3 / 2 + (fs.n_fatent & 1);
            }
            if (fs.fsize < (szbfat + (SS(fs) - 1)) / SS(fs)) return FRESULT.FR_NO_FILESYSTEM;   /* (BPB_FATSz must not be less than the size needed) */


            /* Get FSInfo if available */
            fs.last_clst = fs.free_clst = 0xFFFFFFFF;       /* Initialize cluster allocation information */
            fs.fsi_flag = 0x80;

            if (fmt == FS_FAT32             /* Allow to update FSInfo only if BPB_FSInfo32 == 1 */
                && ld_word(fs.win, BPB_FSInfo32) == 1
                && move_window(ref fs, bsect + 1) == FRESULT.FR_OK)
            {
                fs.fsi_flag = 0;
                if (ld_word(fs.win, BS_55AA) == 0xAA55  /* Load FSInfo data if available */
                    && ld_dword(fs.win, FSI_LeadSig) == 0x41615252
                    && ld_dword(fs.win, FSI_StrucSig) == 0x61417272)
                {
                    fs.free_clst = ld_dword(fs.win, FSI_Free_Count);
                    fs.last_clst = ld_dword(fs.win, FSI_Nxt_Free);
                }
            }



            fs.fs_type = fmt;       /* FAT sub-type */
            fs.id = ++Fsid;     /* Volume mount ID */


            return FRESULT.FR_OK;
        }


        /*-----------------------------------------------------------------------*/
        /* Check if the file/directory object is valid or not                    */
        /*-----------------------------------------------------------------------*/

        static FRESULT validate(    /* Returns FR_OK or FR_INVALID_OBJECT */
            ref FFOBJID obj,           /* Pointer to the FFOBJID, the 1st member in the FIL/DIR object, to check validity */
            ref FATFS rfs             /* Pointer to pointer to the owner filesystem object to return */
        )
        {
            FRESULT res = FRESULT.FR_INVALID_OBJECT;


            if (obj != null && obj.fs != null && obj.fs.fs_type > 0 && obj.id == obj.fs.id)
            {   /* Test if the object is valid */

                if ((DiskIO.disk_status(obj.fs.pdrv) & STA_NOINIT) == 0)
                { /* Test if the phsical drive is kept initialized */
                    res = FRESULT.FR_OK;
                }

            }
            rfs = (res == FRESULT.FR_OK) ? obj.fs : null;    /* Corresponding filesystem object */
            return res;
        }



        /*---------------------------------------------------------------------------

           Public Functions (FatFs API)

        ----------------------------------------------------------------------------*/



        /*-----------------------------------------------------------------------*/
        /* Mount/Unmount a Logical Drive                                         */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_mount(
            ref FATFS fs,		/* Pointer to the filesystem object (NULL:unmount)*/
            string path,        /* Logical drive number to be mounted/unmounted */
            byte opt,			/* Mode option 0:Do not mount (delayed mount), 1:Mount immediately */
            string busId,
            int csPin
        )
        {
            FATFS cfs;
            int vol;
            FRESULT res;
            byte[] rp;
            uint rpIndex = 0;

            /* Generate zer terminated byte array from filename */
            rp = path.ToNullTerminatedByteArray();

            /* Get logical drive number */
            vol = get_ldnumber(rp, ref rpIndex);
            if (vol < 0) return FRESULT.FR_INVALID_DRIVE;

            cfs = FatFs[vol];                   /* Pointer to fs object */

            if (cfs != null)
            {
                cfs.fs_type = 0;                /* Clear old fs object */
            }

            if (fs != null)
            {
                fs.fs_type = 0;                 /* Clear new fs object */
            }

            FatFs[vol] = fs;                    /* Register new fs object */

            if (opt == 0) return FRESULT.FR_OK;         /* Do not mount now, it will be mounted later */

            res = find_volume(ref rp, ref fs, 0, busId, csPin);       /* Force mount the volume */

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Open or Create a File                                                 */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_open(
            ref FIL fp,		/* Pointer to the blank file object */
            string fullFilename,    /* Pointer to the file name */
            byte mode,		/* Access mode and file open mode flags */
            string busId,
            int csPin
        )
        {
            FRESULT res;
            DIR dj = new DIR();
            FATFS fs = null;
            byte[] path;
            uint pathIndex = 0;

            uint dw, cl, bcs, clst, sc;
            uint ofs;

            if (fp == null) return FRESULT.FR_INVALID_OBJECT;
            path = fullFilename.ToNullTerminatedByteArray();

            /* Get logical drive number */
            mode &= (byte)(FA_READ | FA_WRITE | FA_CREATE_ALWAYS | FA_CREATE_NEW | FA_OPEN_ALWAYS | FA_OPEN_APPEND);
            res = find_volume(ref path, ref fs, mode, busId, csPin);
            if (res == FRESULT.FR_OK)
            {
                dj.obj.fs = fs;

                res = follow_path(ref dj, path, ref pathIndex);   /* Follow the file path */

                if (res == FRESULT.FR_OK)
                {
                    if ((dj.fn[NSFLAG] & NS_NONAME) > 0)
                    {   /* Origin directory itself? */
                        res = FRESULT.FR_INVALID_NAME;
                    }

                }
                /* Create or Open a file */
                if ((mode & (byte)(FA_CREATE_ALWAYS | FA_OPEN_ALWAYS | FA_CREATE_NEW)) > 0)
                {
                    if (res != FRESULT.FR_OK)
                    {
                        /* No file, create new */
                        if (res == FRESULT.FR_NO_FILE)
                        {
                            /* There is no file to open, create a new entry */

                            res = dir_register(ref dj);

                        }
                        mode |= FA_CREATE_ALWAYS;       /* File is created */
                    }
                    else
                    {                               /* Any object with the same name is already existing */
                        if ((dj.obj.attr & (byte)(AM_RDO | AM_DIR)) > 0)
                        {   /* Cannot overwrite it (R/O or DIR) */
                            res = FRESULT.FR_DENIED;
                        }
                        else
                        {
                            if ((mode & FA_CREATE_NEW) > 0) res = FRESULT.FR_EXIST;   /* Cannot create as new file */
                        }
                    }
                    if (res == FRESULT.FR_OK && (mode & FA_CREATE_ALWAYS) > 0)
                    {
                        /* Set directory entry initial state */
                        cl = ld_clust(fs, fs.win.SubArray(dj.dirAsFsWinOffset));          /* Get current cluster chain */
                        st_dword(ref fs.win, dj.dirAsFsWinOffset + DIR_CrtTime, GET_FATTIME());  /* Set created time */
                        fs.win[dj.dirAsFsWinOffset + DIR_Attr] = AM_ARC;          /* Reset attribute */
                        st_clust(ref fs, dj.dirAsFsWinOffset, 0);            /* Reset file allocation info */
                        st_dword(ref fs.win, dj.dirAsFsWinOffset + DIR_FileSize, 0);
                        fs.wflag = 1;
                        if (cl != 0)
                        {                       /* Remove the cluster chain if exist */
                            dw = fs.winsect;
                            res = remove_chain(ref dj.obj, cl, 0);
                            if (res == FRESULT.FR_OK)
                            {
                                res = move_window(ref fs, dw);
                                fs.last_clst = cl - 1;     /* Reuse the cluster hole */
                            }
                        }
                    }
                }
                else
                {   /* Open an existing file */
                    if (res == FRESULT.FR_OK)
                    {
                        /* Is the object existing? */
                        if ((dj.obj.attr & AM_DIR) > 0)
                        {       /* File open against a directory */
                            res = FRESULT.FR_NO_FILE;
                        }
                        else
                        {
                            if ((mode & FA_WRITE) > 0 && (dj.obj.attr & AM_RDO) > 0)
                            { /* Write mode open against R/O file */
                                res = FRESULT.FR_DENIED;
                            }
                        }
                    }
                }
                if (res == FRESULT.FR_OK)
                {
                    if ((mode & FA_CREATE_ALWAYS) > 0) mode |= FA_MODIFIED;   /* Set file change flag if created or overwritten */
                    fp.dir_sect = fs.winsect;         /* Pointer to the directory entry */
                    fp.dir_ptrAsFsWinOffset = dj.dirAsFsWinOffset;
                }


                if (res == FRESULT.FR_OK)
                {
                    {
                        fp.obj.sclust = ld_clust(fs, fs.win.SubArray(dj.dirAsFsWinOffset));                  /* Get object allocation info */
                        fp.obj.objsize = ld_dword(fs.win, dj.dirAsFsWinOffset + DIR_FileSize);
                    }
                    fp.obj.fs = fs;        /* Validate the file object */
                    fp.obj.id = fs.id;
                    fp.flag = mode;        /* Set file access mode */
                    fp.err = 0;            /* Clear error flag */
                    fp.sect = 0;           /* Invalidate current data sector */
                    fp.fptr = 0;           /* Set file pointer top of the file */


                    mem_set(ref fp.buf, 0, FF_MAX_SS); /* Clear sector buffer */

                    if ((mode & FA_SEEKEND) > 0 && fp.obj.objsize > 0)
                    {   /* Seek to end of file if FA_OPEN_APPEND is specified */
                        fp.fptr = fp.obj.objsize;         /* Offset to seek */
                        bcs = (uint)fs.csize * SS(fs);    /* Cluster size in byte */
                        clst = fp.obj.sclust;              /* Follow the cluster chain */
                        for (ofs = fp.obj.objsize; res == FRESULT.FR_OK && ofs > bcs; ofs -= bcs)
                        {
                            clst = get_fat(ref fp.obj, clst);
                            if (clst <= 1) res = FRESULT.FR_INT_ERR;
                            if (clst == 0xFFFFFFFF) res = FRESULT.FR_DISK_ERR;
                        }
                        fp.clust = clst;
                        if (res == FRESULT.FR_OK && (ofs % SS(fs)) > 0)
                        {   /* Fill sector buffer if not on the sector boundary */
                            if ((sc = clst2sect(fs, clst)) == 0)
                            {
                                res = FRESULT.FR_INT_ERR;
                            }
                            else
                            {
                                fp.sect = sc + (uint)(ofs / SS(fs));

                                if (DiskIO.disk_read(fs.pdrv, ref fp.buf, fp.sect, 1) != DiskIO.DRESULT.RES_OK) res = FRESULT.FR_DISK_ERR;

                            }
                        }
                    }
                }
            }

            if (res != FRESULT.FR_OK) fp.obj.fs = null;   /* Invalidate file object on error */

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Read File                                                             */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_read(
            ref FIL fp,    /* Pointer to the file object */
            ref byte[] buff, /* Pointer to data buffer */
            uint btr,   /* Number of bytes to read */
            ref uint br    /* Pointer to number of bytes read */
        )
        {
            FRESULT res;
            FATFS fs = null;
            uint clst, sect;
            uint remain;
            uint rcnt, cc, csect;
            uint bufIndex = 0;


            br = 0;    /* Clear read byte counter */
            res = validate(ref fp.obj, ref fs);              /* Check validity of the file object */
            if (res != FRESULT.FR_OK || (res = (FRESULT)fp.err) != FRESULT.FR_OK) return res;   /* Check validity */
            if ((fp.flag & FA_READ) == 0) return FRESULT.FR_DENIED; /* Check access mode */
            remain = fp.obj.objsize - fp.fptr;
            if (btr > remain) btr = remain;       /* Truncate btr by remaining bytes */

            for (; btr > 0;                             /* Repeat until btr bytes read */
                btr -= rcnt, br += rcnt, bufIndex += rcnt, fp.fptr += rcnt)
            {
                if (fp.fptr % SS(fs) == 0)
                {
                    /* On the sector boundary? */
                    csect = (uint)(fp.fptr / SS(fs) & (fs.csize - 1));    /* Sector offset in the cluster */
                    if (csect == 0)
                    {
                        /* On the cluster boundary? */
                        if (fp.fptr == 0)
                        {           /* On the top of the file? */
                            clst = fp.obj.sclust;      /* Follow cluster chain from the origin */
                        }
                        else
                        {
                            /* Middle or end of the file */
                            clst = get_fat(ref fp.obj, fp.clust);    /* Follow cluster chain on the FAT */

                        }
                        if (clst < 2)
                        {
                            fp.err = (byte)res;
                            return res;
                        }
                        if (clst == 0xFFFFFFFF)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }
                        fp.clust = clst;               /* Update current cluster */
                    }
                    sect = clst2sect(fs, fp.clust);    /* Get current sector */
                    if (sect == 0)
                    {
                        fp.err = (byte)FRESULT.FR_INT_ERR;
                        return res;
                    }
                    sect += csect;
                    cc = btr / SS(fs);                  /* When remaining bytes >= sector size, */
                    if (cc > 0)
                    {
                        /* Read maximum contiguous sectors directly */
                        if (csect + cc > fs.csize)
                        {   /* Clip at cluster boundary */
                            cc = fs.csize - csect;
                        }
                        var bytesToRead = cc * SS(fs);
                        var tempBuf = new byte[bytesToRead];
                        if (DiskIO.disk_read(fs.pdrv, ref tempBuf, sect, cc) != DiskIO.DRESULT.RES_OK)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }
                        // HB: Copy buffer directly into result buffer
                        mem_cpy(ref buff, (int)bufIndex, tempBuf, bytesToRead);

                        /* Replace one of the read sectors with cached data if it contains a dirty sector */
                        if ((fp.flag & FA_DIRTY) > 0 && fp.sect - sect < cc)
                        {
                            mem_cpy(ref buff, (int)((fp.sect - sect) * SS(fs)), fp.buf, SS(fs));
                        }

                        rcnt = SS(fs) * cc;             /* Number of bytes transferred */
                        continue;
                    }

                    if (fp.sect != sect)
                    {
                        /* Load data sector if not in cache */
                        if ((fp.flag & FA_DIRTY) > 0)
                        {
                            /* Write-back dirty sector cache */
                            if (DiskIO.disk_write(fs.pdrv, fp.buf, fp.sect, 1) != DiskIO.DRESULT.RES_OK)
                            {
                                fp.err = (byte)FRESULT.FR_DISK_ERR;
                                return res;
                            }
                            fp.flag &= (byte)(~FA_DIRTY & 0xff);
                        }

                        /* Fill sector cache */
                        if (DiskIO.disk_read(fs.pdrv, ref fp.buf, sect, 1) != DiskIO.DRESULT.RES_OK)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }
                    }
                    fp.sect = sect;
                }
                rcnt = SS(fs) - (uint)fp.fptr % SS(fs);    /* Number of bytes left in the sector */
                if (rcnt > btr) rcnt = btr;                 /* Clip it by btr if needed */

                mem_cpy(ref buff, (int)bufIndex, fp.buf, (int)(fp.fptr % SS(fs)), rcnt);  /* Extract partial sector */

            }

            return FRESULT.FR_OK;
        }



        /*-----------------------------------------------------------------------*/
        /* Write File                                                            */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_write(
            ref FIL fp,			/* Pointer to the file object */
            byte[] buff,   /* Pointer to the data to be written */
            uint btw,           /* Number of bytes to write */
            ref uint bw			/* Pointer to number of bytes written */
        )
        {

            FRESULT res;
            FATFS fs = null;
            uint clst, sect;
            uint wcnt, cc, csect;
            uint buffIndex = 0;


            bw = 0; /* Clear write byte counter */
            res = validate(ref fp.obj, ref fs);         /* Check validity of the file object */
            if (res != FRESULT.FR_OK || (res = (FRESULT)fp.err) != FRESULT.FR_OK) return res;   /* Check validity */
            if ((fp.flag & FA_WRITE) == 0) return FRESULT.FR_DENIED;    /* Check access mode */

            /* Check fptr wrap-around (file size cannot reach 4 GiB at FAT volume) */
            if ((fs.fs_type != FS_EXFAT) && (fp.fptr + btw) < fp.fptr)
            {
                btw = (uint)(0xFFFFFFFF - fp.fptr);
            }

            for (; btw > 0;                         /* Repeat until all data written */
                btw -= wcnt, bw += wcnt, buffIndex += wcnt, fp.fptr += wcnt, fp.obj.objsize = (fp.fptr > fp.obj.objsize) ? fp.fptr : fp.obj.objsize)
            {
                if (fp.fptr % SS(fs) == 0)
                {       /* On the sector boundary? */
                    csect = (uint)(fp.fptr / SS(fs)) & (fs.csize - 1);  /* Sector offset in the cluster */
                    if (csect == 0)
                    {               /* On the cluster boundary? */
                        if (fp.fptr == 0)
                        {
                            /* On the top of the file? */
                            clst = fp.obj.sclust;   /* Follow from the origin */
                            if (clst == 0)
                            {
                                /* If no cluster is allocated, */
                                clst = create_chain(ref fp.obj, 0);   /* create a new cluster chain */
                            }
                        }
                        else
                        {
                            /* On the middle or end of the file */
                            clst = create_chain(ref fp.obj, fp.clust);  /* Follow or stretch cluster chain on the FAT */
                        }
                        if (clst == 0) break;       /* Could not allocate a new cluster (disk full) */
                        if (clst == 1)
                        {
                            fp.err = (byte)FRESULT.FR_INT_ERR;
                            return res;
                        }
                        if (clst == 0xFFFFFFFF)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }
                        fp.clust = clst;            /* Update current cluster */
                        if (fp.obj.sclust == 0) fp.obj.sclust = clst;   /* Set start cluster if the first write */
                    }
                    if ((fp.flag & FA_DIRTY) > 0)
                    {     /* Write-back sector cache */
                        if (DiskIO.disk_write(fs.pdrv, fp.buf, fp.sect, 1) != DiskIO.DRESULT.RES_OK)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }
                        fp.flag &= (byte)(~FA_DIRTY & 0xff);
                    }
                    sect = clst2sect(fs, fp.clust); /* Get current sector */
                    if (sect == 0)
                    {
                        fp.err = (byte)FRESULT.FR_INT_ERR;
                        return res;
                    }
                    sect += csect;
                    cc = btw / SS(fs);              /* When remaining bytes >= sector size, */
                    if (cc > 0)
                    {
                        /* Write maximum contiguous sectors directly */
                        if (csect + cc > fs.csize)
                        {   /* Clip at cluster boundary */
                            cc = fs.csize - csect;
                        }
                        var bytesToRead = cc * SS(fs);
                        var tempBuf = new byte[bytesToRead];
                        Array.Copy(buff, (int)buffIndex, tempBuf, 0, (int)bytesToRead);
                        if (DiskIO.disk_write(fs.pdrv, tempBuf, sect, cc) != DiskIO.DRESULT.RES_OK)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }

                        if (fp.sect - sect < cc)
                        {
                            /* Refill sector cache if it gets invalidated by the direct write */
                            mem_cpy(ref fp.buf, 0, buff, (int)(buffIndex + ((fp.sect - sect) * SS(fs))), SS(fs));
                            fp.flag &= (byte)(~FA_DIRTY & 0xff);
                        }
                        wcnt = SS(fs) * cc;     /* Number of bytes transferred */
                        continue;
                    }
                    if (fp.sect != sect &&      /* Fill sector cache with file data */
                        fp.fptr < fp.obj.objsize &&
                        DiskIO.disk_read(fs.pdrv, ref fp.buf, sect, 1) != DiskIO.DRESULT.RES_OK)
                    {
                        fp.err = (byte)FRESULT.FR_DISK_ERR;
                        return res;
                    }
                    fp.sect = sect;
                }
                wcnt = SS(fs) - fp.fptr % SS(fs);   /* Number of bytes left in the sector */
                if (wcnt > btw) wcnt = btw;					/* Clip it by btw if needed */
                mem_cpy(ref fp.buf, (int)(fp.fptr % SS(fs)), buff, (int)buffIndex, wcnt);  /* Fit data to the sector */
                fp.flag |= FA_DIRTY;
            }

            fp.flag |= FA_MODIFIED;             /* Set file change flag */

            return FRESULT.FR_OK;
        }

        /*-----------------------------------------------------------------------*/
        /* Synchronize the File                                                  */
        /*-----------------------------------------------------------------------*/

        FRESULT f_sync(
            ref FIL fp     /* Pointer to the file object */
        )
        {
            FRESULT res;
            FATFS fs = null;
            uint tm;
            uint dir_ptrAsFsWinOffset; // dir;


            res = validate(ref fp.obj, ref fs);  /* Check validity of the file object */
            if (res == FRESULT.FR_OK)
            {
                if ((fp.flag & FA_MODIFIED) > 0)
                {
                    /* Is there any change to the file? */

                    if ((fp.flag & FA_DIRTY) > 0)
                    {   /* Write-back cached data if needed */
                        if (DiskIO.disk_write(fs.pdrv, fp.buf, fp.sect, 1) != DiskIO.DRESULT.RES_OK) return FRESULT.FR_DISK_ERR;
                        fp.flag &= (byte)(~FA_DIRTY & 0xff);
                    }

                    /* Update the directory entry */
                    tm = GET_FATTIME();             /* Modified time */

                    {
                        res = move_window(ref fs, fp.dir_sect);
                        if (res == FRESULT.FR_OK)
                        {
                            dir_ptrAsFsWinOffset = fp.dir_ptrAsFsWinOffset;
                            fs.win[dir_ptrAsFsWinOffset + DIR_Attr] |= AM_ARC;                        /* Set archive attribute to indicate that the file has been changed */
                            st_clust(ref fp.obj.fs, dir_ptrAsFsWinOffset, fp.obj.sclust);      /* Update file allocation information  */
                            st_dword(ref fs.win, dir_ptrAsFsWinOffset + DIR_FileSize, fp.obj.objsize);   /* Update file size */
                            st_dword(ref fs.win, dir_ptrAsFsWinOffset + DIR_ModTime, tm);                /* Update modified time */
                            st_word(ref fs.win, dir_ptrAsFsWinOffset + DIR_LstAccDate, 0);
                            fs.wflag = 1;
                            res = sync_fs(ref fs);                  /* Restore it to the directory */
                            fp.flag &= (byte)(~FA_MODIFIED & 0xff);
                        }
                    }
                }
            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Close File                                                            */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_close(
            ref FIL fp     /* Pointer to the file object to be closed */
        )
        {
            FRESULT res;
            FATFS fs = null;


            res = f_sync(ref fp);                   /* Flush cached data */
            if (res == FRESULT.FR_OK)

            {
                res = validate(ref fp.obj, ref fs);  /* Lock volume */
                if (res == FRESULT.FR_OK)
                {

                    fp.obj.fs = null; /* Invalidate file object */


                }
            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Seek File Read/Write Pointer                                          */
        /*-----------------------------------------------------------------------*/

        FRESULT f_lseek(
            ref FIL fp,        /* Pointer to the file object */
            ref uint ofs     /* File pointer from top of file */
        )
        {
            FRESULT res;
            FATFS fs = null;
            uint clst, bcs, nsect;
            uint ifptr;


            res = validate(ref fp.obj, ref fs);      /* Check validity of the file object */
            if (res == FRESULT.FR_OK) res = (FRESULT)fp.err;

            if (res != FRESULT.FR_OK) return res;

            /* Normal Seek */
            {
                if (ofs > fp.obj.objsize && (fp.flag & FA_WRITE) == 0)
                {   /* In read-only mode, clip offset with the file size */
                    ofs = fp.obj.objsize;
                }
                ifptr = fp.fptr;
                fp.fptr = nsect = 0;
                if (ofs > 0)
                {
                    bcs = (uint)fs.csize * SS(fs);    /* Cluster size (byte) */
                    if (ifptr > 0 &&
                        (ofs - 1) / bcs >= (ifptr - 1) / bcs)
                    {   /* When seek to same or following cluster, */
                        fp.fptr = (ifptr - 1) & ~(uint)(bcs - 1);   /* start from the current cluster */
                        ofs -= fp.fptr;
                        clst = fp.clust;
                    }
                    else
                    {
                        /* When seek to back cluster, */
                        clst = fp.obj.sclust;                  /* start from the first cluster */

                        if (clst == 0)
                        {                       /* If no cluster chain, create a new chain */
                            clst = create_chain(ref fp.obj, 0);
                            if (clst == 1)
                            {
                                fp.err = (byte)FRESULT.FR_INT_ERR;
                                return res;
                            }
                            if (clst == 0xFFFFFFFF)
                            {
                                fp.err = (byte)FRESULT.FR_DISK_ERR;
                                return res;
                            }
                            fp.obj.sclust = clst;
                        }

                        fp.clust = clst;
                    }
                    if (clst != 0)
                    {
                        while (ofs > bcs)
                        {                       /* Cluster following loop */
                            ofs -= bcs; fp.fptr += bcs;

                            if ((fp.flag & FA_WRITE) > 0)
                            {           /* Check if in write mode or not */
                                if (FF_FS_EXFAT > 0 && fp.fptr > fp.obj.objsize)
                                {
                                    /* No FAT chain object needs correct objsize to generate FAT value */
                                    fp.obj.objsize = fp.fptr;
                                    fp.flag |= FA_MODIFIED;
                                }
                                clst = create_chain(ref fp.obj, clst);    /* Follow chain with forceed stretch */
                                if (clst == 0)
                                {               /* Clip file size in case of disk full */
                                    ofs = 0; break;
                                }
                            }
                            else

                            {
                                clst = get_fat(ref fp.obj, clst); /* Follow cluster chain if not in write mode */
                            }
                            if (clst == 0xFFFFFFFF)
                            {
                                fp.err = (byte)FRESULT.FR_DISK_ERR;
                                return res;
                            }
                            if (clst <= 1 || clst >= fs.n_fatent)
                            {
                                fp.err = (byte)FRESULT.FR_INT_ERR;
                                return res;
                            }
                            fp.clust = clst;
                        }
                        fp.fptr += ofs;
                        if ((ofs % SS(fs)) > 0)
                        {
                            nsect = clst2sect(fs, clst);    /* Current sector */
                            if (nsect == 0)
                            {
                                fp.err = (byte)FRESULT.FR_INT_ERR;
                                return res;
                            }
                            nsect += (uint)(ofs / SS(fs));
                        }
                    }
                }
                if (fp.fptr > fp.obj.objsize)
                {   /* Set file change flag if the file size is extended */
                    fp.obj.objsize = fp.fptr;
                    fp.flag |= FA_MODIFIED;
                }
                if ((fp.fptr % SS(fs)) > 0 && nsect != fp.sect)
                {   /* Fill sector cache if needed */

                    if ((fp.flag & FA_DIRTY) > 0)
                    {           /* Write-back dirty sector cache */
                        if (DiskIO.disk_write(fs.pdrv, fp.buf, fp.sect, 1) != DiskIO.DRESULT.RES_OK)
                        {
                            fp.err = (byte)FRESULT.FR_DISK_ERR;
                            return res;
                        }
                        fp.flag &= (byte)(~FA_DIRTY & 0xff);
                    }

                    if (DiskIO.disk_read(fs.pdrv, ref fp.buf, nsect, 1) != DiskIO.DRESULT.RES_OK) /* Fill sector cache */
                    {
                        fp.err = (byte)FRESULT.FR_DISK_ERR;
                        return res;
                    }

                    fp.sect = nsect;
                }
            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Create a Directory Object                                             */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_opendir(
            ref DIR dp,			/* Pointer to directory object to create */
            byte[] path,	/* Pointer to the directory path */
            string busId,
            int csPin
        )
        {

            FRESULT res;
            FATFS fs = null;
            uint pathIndex = 0;

            if (dp == null) return FRESULT.FR_INVALID_OBJECT;

            /* Get logical drive */
            res = find_volume(ref path, ref fs, 0, busId, csPin);
            if (res == FRESULT.FR_OK)
            {
                dp.obj.fs = fs;
                res = follow_path(ref dp, path, ref pathIndex);         /* Follow the path to the directory */
                if (res == FRESULT.FR_OK)
                {                       /* Follow completed */
                    if ((dp.fn[NSFLAG] & NS_NONAME) == 0)
                    {   /* It is not the origin directory itself */
                        if ((dp.obj.attr & AM_DIR) > 0)
                        {       /* This object is a sub-directory */
                            {
                                dp.obj.sclust = ld_clust(fs, fs.win, dp.dirAsFsWinOffset); /* Get object allocation info */
                            }
                        }
                        else
                        {
                            /* This object is a file */
                            res = FRESULT.FR_NO_PATH;
                        }
                    }
                    if (res == FRESULT.FR_OK)
                    {
                        dp.obj.id = fs.id;
                        res = dir_sdi(ref dp, 0);           /* Rewind directory */
                    }
                }
                if (res == FRESULT.FR_NO_FILE) res = FRESULT.FR_NO_PATH;
            }
            if (res != FRESULT.FR_OK) dp.obj.fs = null;     /* Invalidate the directory object if function faild */

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Close Directory                                                       */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_closedir(
            ref DIR dp     /* Pointer to the directory object to be closed */
        )
        {
            FRESULT res;
            FATFS fs = null;


            res = validate(ref dp.obj, ref fs);  /* Check validity of the file object */
            if (res == FRESULT.FR_OK)
            {

                dp.obj.fs = null; /* Invalidate directory object */

            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Read Directory Entries in Sequence                                    */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_readdir(
            ref DIR dp,            /* Pointer to the open directory object */
            ref FILINFO fno        /* Pointer to file information to return */
        )
        {
            FRESULT res;
            FATFS fs = null;

            res = validate(ref dp.obj, ref fs);  /* Check validity of the directory object */
            if (res == FRESULT.FR_OK)
            {
                if (fno == null)
                {
                    res = dir_sdi(ref dp, 0);           /* Rewind the directory object */
                }
                else
                {

                    res = dir_read_file(ref dp);        /* Read an item */
                    if (res == FRESULT.FR_NO_FILE) res = FRESULT.FR_OK; /* Ignore end of directory */
                    if (res == FRESULT.FR_OK)
                    {
                        /* A valid entry is found */
                        get_fileinfo(dp, ref fno);      /* Get the object information */
                        res = dir_next(ref dp, 0);      /* Increment index for next */
                        if (res == FRESULT.FR_NO_FILE) res = FRESULT.FR_OK; /* Ignore end of directory now */
                    }

                }
            }
            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Get File Status                                                       */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_stat(
            string fullFilename,  /* Pointer to the file path */
            ref FILINFO fno,		/* Pointer to file information to return */
            string busId,
            int csPin
        )
        {
            FRESULT res;
            DIR dj = new DIR();
            byte[] path;
            uint pathIndex = 0;

            path = fullFilename.ToNullTerminatedByteArray();

            /* Get logical drive */
            res = find_volume(ref path, ref dj.obj.fs, 0, busId, csPin);
            if (res == FRESULT.FR_OK)
            {
                res = follow_path(ref dj, path, ref pathIndex); /* Follow the file path */
                if (res == FRESULT.FR_OK)
                {               /* Follow completed */
                    if ((dj.fn[NSFLAG] & NS_NONAME) > 0)
                    {   /* It is origin directory */
                        res = FRESULT.FR_INVALID_NAME;
                    }
                    else
                    {
                        /* Found an object */
                        if (fno != null) get_fileinfo(dj, ref fno);
                    }
                }
            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Get Number of Free Clusters                                           */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_getfree(
            string driveNum,  /* Logical drive number */
            ref uint nclst,     /* Pointer to a variable to return number of free clusters */
            ref FATFS fatfs,		/* Pointer to return pointer to corresponding filesystem object */
            string busId,
            int csPin
        )
        {

            FRESULT res;
            FATFS fs = null;
            uint nfree, clst, sect, stat;
            uint i;
            FFOBJID obj = new FFOBJID();

            byte[] path = driveNum.ToNullTerminatedByteArray();

            /* Get logical drive */
            res = find_volume(ref path, ref fs, 0, busId, csPin);
            if (res == FRESULT.FR_OK)
            {
                fatfs = fs;             /* Return ptr to the fs object */
                                        /* If free_clst is valid, return it without full FAT scan */
                if (fs.free_clst <= fs.n_fatent - 2)
                {
                    nclst = fs.free_clst;
                }
                else
                {
                    /* Scan FAT to obtain number of free clusters */
                    nfree = 0;
                    if (fs.fs_type == FS_FAT12)
                    {   /* FAT12: Scan bit field FAT entries */
                        clst = 2; obj.fs = fs;
                        do
                        {
                            stat = get_fat(ref obj, clst);
                            if (stat == 0xFFFFFFFF) { res = FRESULT.FR_DISK_ERR; break; }
                            if (stat == 1) { res = FRESULT.FR_INT_ERR; break; }
                            if (stat == 0) nfree++;
                        } while (++clst < fs.n_fatent);
                    }
                    else
                    {
                        {   /* FAT16/32: Scan WORD/DWORD FAT entries */
                            clst = fs.n_fatent; /* Number of entries */
                            sect = fs.fatbase;      /* Top of the FAT */
                            i = 0;                  /* Offset in the sector */
                            do
                            {   /* Counts numbuer of entries with zero in the FAT */
                                if (i == 0)
                                {
                                    res = move_window(ref fs, sect++);
                                    if (res != FRESULT.FR_OK) break;
                                }
                                if (fs.fs_type == FS_FAT16)
                                {
                                    if (ld_word(fs.win, i) == 0) nfree++;
                                    i += 2;
                                }
                                else
                                {
                                    if ((ld_dword(fs.win, i) & 0x0FFFFFFF) == 0) nfree++;
                                    i += 4;
                                }
                                i %= SS(fs);
                            } while (--clst > 0);
                        }
                    }

                    nclst = nfree;            /* Return the free clusters */
                    fs.free_clst = nfree;   /* Now free_clst is valid */
                    fs.fsi_flag |= 1;       /* FAT32: FSInfo is to be updated */
                }
            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Truncate File                                                         */
        /*-----------------------------------------------------------------------*/

        FRESULT f_truncate(
            ref FIL fp     /* Pointer to the file object */
        )
        {
            FRESULT res;
            FATFS fs = null;
            uint ncl;


            res = validate(ref fp.obj, ref fs);  /* Check validity of the file object */
            if (res != FRESULT.FR_OK || (res = (FRESULT)fp.err) != FRESULT.FR_OK) return res;
            if ((fp.flag & FA_WRITE) == 0) return FRESULT.FR_DENIED;    /* Check access mode */

            if (fp.fptr < fp.obj.objsize)
            {   /* Process when fptr is not on the eof */
                if (fp.fptr == 0)
                {   /* When set file size to zero, remove entire cluster chain */
                    res = remove_chain(ref fp.obj, fp.obj.sclust, 0);
                    fp.obj.sclust = 0;
                }
                else
                {
                    /* When truncate a part of the file, remove remaining clusters */
                    ncl = get_fat(ref fp.obj, fp.clust);
                    res = FRESULT.FR_OK;
                    if (ncl == 0xFFFFFFFF) res = FRESULT.FR_DISK_ERR;
                    if (ncl == 1) res = FRESULT.FR_INT_ERR;
                    if (res == FRESULT.FR_OK && ncl < fs.n_fatent)
                    {
                        res = remove_chain(ref fp.obj, ncl, fp.clust);
                    }
                }
                fp.obj.objsize = fp.fptr; /* Set file size to current read/write point */
                fp.flag |= FA_MODIFIED;

                if (res == FRESULT.FR_OK && (fp.flag & FA_DIRTY) > 0)
                {
                    if (DiskIO.disk_write(fs.pdrv, fp.buf, fp.sect, 1) != DiskIO.DRESULT.RES_OK)
                    {
                        res = FRESULT.FR_DISK_ERR;
                    }
                    else
                    {
                        fp.flag &= (byte)(~FA_DIRTY & 0xff);
                    }
                }

                if (res != FRESULT.FR_OK)
                {
                    fp.err = (byte)res;
                    return res;
                }
            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Delete a File/Directory                                               */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_unlink(
            string fullFilename,		/* Pointer to the file or directory path */
            string busId,
            int csPin
        )
        {

            FRESULT res;
            DIR dj = new DIR();
            DIR sdj = new DIR();
            uint dclst = 0;
            FATFS fs = null;
            byte[] path;
            uint pathIndex = 0;

            path = fullFilename.ToNullTerminatedByteArray();

            /* Get logical drive */
            res = find_volume(ref path, ref fs, FA_WRITE, busId, csPin);
            if (res == FRESULT.FR_OK)
            {
                dj.obj.fs = fs;
                res = follow_path(ref dj, path, ref pathIndex);     /* Follow the file path */
                if (FF_FS_RPATH > 0 && res == FRESULT.FR_OK && (dj.fn[NSFLAG] & NS_DOT) > 0)
                {
                    res = FRESULT.FR_INVALID_NAME;          /* Cannot remove dot entry */
                }

                if (res == FRESULT.FR_OK)
                {                   /* The object is accessible */
                    if ((dj.fn[NSFLAG] & NS_NONAME) > 0)
                    {
                        res = FRESULT.FR_INVALID_NAME;      /* Cannot remove the origin directory */
                    }
                    else
                    {
                        if ((dj.obj.attr & AM_RDO) > 0)
                        {
                            res = FRESULT.FR_DENIED;        /* Cannot remove R/O object */
                        }
                    }
                    if (res == FRESULT.FR_OK)
                    {

                        dclst = ld_clust(fs, fs.win, dj.dirAsFsWinOffset);

                        if ((dj.obj.attr & AM_DIR) > 0)
                        {           /* Is it a sub-directory? */

                            {
                                sdj.obj.fs = fs;                /* Open the sub-directory */
                                sdj.obj.sclust = dclst;
                                res = dir_sdi(ref sdj, 0);
                                if (res == FRESULT.FR_OK)
                                {
                                    res = dir_read_file(ref sdj);           /* Test if the directory is empty */
                                    if (res == FRESULT.FR_OK) res = FRESULT.FR_DENIED;  /* Not empty? */
                                    if (res == FRESULT.FR_NO_FILE) res = FRESULT.FR_OK; /* Empty? */
                                }
                            }
                        }
                    }
                    if (res == FRESULT.FR_OK)
                    {
                        res = dir_remove(ref dj);           /* Remove the directory entry */
                        if (res == FRESULT.FR_OK && dclst != 0)
                        {   /* Remove the cluster chain if exist */

                            res = remove_chain(ref dj.obj, dclst, 0);

                        }
                        if (res == FRESULT.FR_OK) res = sync_fs(ref fs);
                    }
                }

            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Create a Directory                                                    */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_mkdir(

            string fullFilename,		/* Pointer to the directory path */
            string busId,
            int csPin
        )
        {

            FRESULT res;
            DIR dj = new DIR();
            FATFS fs = null;
            uint dirAsFsWinOffset;
            uint dcl, pcl, tm;
            byte[] path;
            uint pathIndex = 0;

            path = fullFilename.ToNullTerminatedByteArray();

            /* Get logical drive */
            res = find_volume(ref path, ref fs, FA_WRITE, busId, csPin);
            if (res == FRESULT.FR_OK)
            {
                dj.obj.fs = fs;
                res = follow_path(ref dj, path, ref pathIndex);         /* Follow the file path */
                if (res == FRESULT.FR_OK) res = FRESULT.FR_EXIST;       /* Any object with same name is already existing */
                if (FF_FS_RPATH > 0 && res == FRESULT.FR_NO_FILE && (dj.fn[NSFLAG] & NS_DOT) > 0)
                {
                    res = FRESULT.FR_INVALID_NAME;
                }
                if (res == FRESULT.FR_NO_FILE)
                {               /* Can create a new directory */
                    dcl = create_chain(ref dj.obj, 0);     /* Allocate a cluster for the new directory table */
                    dj.obj.objsize = (uint)fs.csize * SS(fs);
                    res = FRESULT.FR_OK;
                    if (dcl == 0) res = FRESULT.FR_DENIED;      /* No space to allocate a new cluster */
                    if (dcl == 1) res = FRESULT.FR_INT_ERR;
                    if (dcl == 0xFFFFFFFF) res = FRESULT.FR_DISK_ERR;
                    if (res == FRESULT.FR_OK) res = sync_window(ref fs);    /* Flush FAT */
                    tm = GET_FATTIME();
                    if (res == FRESULT.FR_OK)
                    {
                        /* Initialize the new directory table */
                        res = dir_clear(ref fs, dcl);       /* Clean up the new table */
                        if (res == FRESULT.FR_OK && (FF_FS_EXFAT == 0 || fs.fs_type != FS_EXFAT))
                        {
                            /* Create dot entries (FAT only) */
                            dirAsFsWinOffset = 0;
                            mem_set(ref fs.win, (int)(dirAsFsWinOffset + DIR_Name), ' ', 11);   /* Create "." entry */
                            fs.win[dirAsFsWinOffset + DIR_Name] = (byte)'.';
                            fs.win[dirAsFsWinOffset + DIR_Attr] = AM_DIR;
                            st_dword(ref fs.win, dirAsFsWinOffset + DIR_ModTime, tm);
                            st_clust(ref fs, dirAsFsWinOffset, dcl);
                            mem_cpy(ref fs.win, (int)(dirAsFsWinOffset + SZDIRE), fs.win, (int)dirAsFsWinOffset, SZDIRE); /* Create ".." entry */
                            fs.win[dirAsFsWinOffset + SZDIRE + 1] = (byte)'.';
                            pcl = dj.obj.sclust;
                            st_clust(ref fs, dirAsFsWinOffset + SZDIRE, pcl);
                            fs.wflag = 1;
                        }
                    }
                    if (res == FRESULT.FR_OK)
                    {
                        res = dir_register(ref dj); /* Register the object to the directoy */
                    }
                    if (res == FRESULT.FR_OK)
                    {

                        dirAsFsWinOffset = dj.dirAsFsWinOffset;
                        st_dword(ref fs.win, dirAsFsWinOffset + DIR_ModTime, tm);    /* Created time */
                        st_clust(ref fs, dirAsFsWinOffset, dcl);             /* Table start cluster */
                        fs.win[dirAsFsWinOffset + DIR_Attr] = AM_DIR;               /* Attribute */
                        fs.wflag = 1;

                        if (res == FRESULT.FR_OK)
                        {
                            res = sync_fs(ref fs);
                        }
                    }
                    else
                    {
                        remove_chain(ref dj.obj, dcl, 0);       /* Could not register, remove cluster chain */
                    }
                }

            }

            return res;
        }

        /*-----------------------------------------------------------------------*/
        /* Rename a File/Directory                                               */
        /*-----------------------------------------------------------------------*/

        public FRESULT f_rename(
            string oldFullFilename,	/* Pointer to the object name to be renamed */
            string newFullFilename,	/* Pointer to the new name */
            string busId,
            int csPin
        )
        {

            FRESULT res;
            DIR djo = new DIR();
            DIR djn = new DIR();
            FATFS fs = null;
            byte[] buf = new byte[SZDIRE];
            uint dw;
            byte[] path_old;
            byte[] path_new;
            uint pathOldIndex = 0;
            uint pathNewIndex = 0;

            uint dirAsFsWinOffset;

            path_old = oldFullFilename.ToNullTerminatedByteArray();
            path_new = newFullFilename.ToNullTerminatedByteArray();

            get_ldnumber(path_new, ref pathNewIndex);                        /* Snip the drive number of new name off */
            res = find_volume(ref path_old, ref fs, FA_WRITE, busId, csPin);  /* Get logical drive of the old object */
            if (res == FRESULT.FR_OK)
            {
                djo.obj.fs = fs;
                res = follow_path(ref djo, path_old, ref pathOldIndex);     /* Check old object */
                if (res == FRESULT.FR_OK && (djo.fn[NSFLAG] & (NS_DOT | NS_NONAME)) > 0) res = FRESULT.FR_INVALID_NAME; /* Check validity of name */

                if (res == FRESULT.FR_OK)
                {                       /* Object to be renamed is found */

                    {
                        /* At FAT/FAT32 volume */
                        mem_cpy(ref buf, 0, fs.win, (int)djo.dirAsFsWinOffset, SZDIRE);          /* Save directory entry of the object */
                        djn = djo.Clone(fs);
                        res = follow_path(ref djn, path_new, ref pathNewIndex);     /* Make sure if new object name is not in use */
                        if (res == FRESULT.FR_OK)
                        {
                            /* Is new name already in use by any other object? */
                            res = (djn.obj.sclust == djo.obj.sclust && djn.dptr == djo.dptr) ? FRESULT.FR_NO_FILE : FRESULT.FR_EXIST;
                        }
                        if (res == FRESULT.FR_NO_FILE)
                        {               /* It is a valid path and no name collision */
                            res = dir_register(ref djn);            /* Register the new entry */
                            if (res == FRESULT.FR_OK)
                            {
                                dirAsFsWinOffset = djn.dirAsFsWinOffset;                    /* Copy directory entry of the object except name */
                                mem_cpy(ref fs.win, (int)(dirAsFsWinOffset + 13), buf, 13, SZDIRE - 13);
                                fs.win[dirAsFsWinOffset + DIR_Attr] = buf[DIR_Attr];
                                if ((fs.win[dirAsFsWinOffset + DIR_Attr] & AM_DIR) == 0) fs.win[dirAsFsWinOffset + DIR_Attr] |= AM_ARC; /* Set archive attribute if it is a file */
                                fs.wflag = 1;
                                if ((fs.win[dirAsFsWinOffset + DIR_Attr] & AM_DIR) > 0 && djo.obj.sclust != djn.obj.sclust)
                                {   /* Update .. entry in the sub-directory if needed */
                                    dw = clst2sect(fs, ld_clust(fs, fs.win, dirAsFsWinOffset));
                                    if (dw == 0)
                                    {
                                        res = FRESULT.FR_INT_ERR;
                                    }
                                    else
                                    {
                                        /* Start of critical section where an interruption can cause a cross-link */
                                        res = move_window(ref fs, dw);
                                        dirAsFsWinOffset = SZDIRE * 1;  /* Ptr to .. entry */
                                        if (res == FRESULT.FR_OK && fs.win[dirAsFsWinOffset + 1] == '.')
                                        {
                                            st_clust(ref fs, dirAsFsWinOffset, djn.obj.sclust);
                                            fs.wflag = 1;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (res == FRESULT.FR_OK)
                    {
                        res = dir_remove(ref djo);      /* Remove old entry */
                        if (res == FRESULT.FR_OK)
                        {
                            res = sync_fs(ref fs);
                        }
                    }
                }
            }
            return res;
        }
    }
}