using System;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Text;
using System.Collections.Generic;




namespace DMXServer
{

    public class MainClass {
        static bool verbose = false; //If the Server should output verbose messages
        static bool stayUp = false; //If the Server should quit if all Clients disconnect
        static bool show_help = false; //Helper flag for showing the help text (-h)
        static string pipeName = ""; //The name of the namedPipe to listen for
        static List<int> blockedChannels = new List<int>(); //Contains all channels that are blocked for any reason (Effects, etc...)
        public static void Main(string[] args){
            //Commandline arguments
            int nPos = -1;
            string argsT;
            try {
                for (int i = 0; i < args.Length; i++) {
                argsT = args[i];

                switch (argsT) {
                    case "-n":
                        nPos = i+1;
                        break;
                    case "-v":
                        verbose = true;
                        break;
                    case "-s":
                        stayUp = true;
                        break;
                    case "-h":
                        show_help = true;
                        break;
                    }
                }

                //Basic error checking in pipeName (-n -s) -> -s would be the pipeName
                if (nPos != -1) {
                    pipeName = args[nPos];
                    if (pipeName.Contains("-")) throw new Exception();
                } else if (!show_help) {
                    throw new Exception();
                }
            } catch {
                Console.WriteLine("Invalid commandline arguments");
                Console.WriteLine("Try `DMXServer -h' for more information.");
                return;
            }
            if (show_help) {
                ShowHelp();
                return;
            }
            
            if (verbose) Console.WriteLine("Name: {0}, V: {1}, S: {2}, H: {3}", pipeName, verbose, stayUp, show_help);

            //DMXServer
            if (verbose) Console.WriteLine("[DEBUG] - DMXServer started");
            OpenDMX.start(); //Starting the OpenDMX interface
            if (verbose) Console.WriteLine("[DEBUG] - DMXLink opened");
            do {
                //Define namedPipe
                using (NamedPipeClientStream pipeClient =
                new NamedPipeClientStream(".", pipeName, PipeDirection.In)) {
                        // Connect to the pipe or wait until the pipe is available.
                        if (verbose) Console.Write("[DEBUG] - Waiting for connection to DMXClient...");
                        pipeClient.Connect();

                        if (verbose) Console.WriteLine("Connected");
                        //if (verbose) Console.WriteLine("{0} DMXClients connected at the moment", pipeClient.NumberOfServerInstances); TODO: Work in progress
                        using (StreamReader sr = new StreamReader(pipeClient)) {
                            //variables used for command handling
                            string temp;
                            string[] dmxCommand;
                            List<int> dmxChannelL = new List<int>();
                            List<byte> dmxValueL = new List<byte>();
                            int channel;
                            byte value;

                            //Command handling
                            while ((temp = sr.ReadLine()) != null) {
                                //Check if message is a DMXCommand
                                if (temp.StartsWith("DMX")) {
                                    try {
                                        dmxCommand = temp.Split();
                                        //Console.WriteLine("DMX Command length: {0}, Invalid-Length: {1}", dmxCommand.Length, (dmxCommand.Length%2 == 0));

                                        //DMX Command basic error checking
                                        if ((dmxCommand.Length%2) == 0) {
                                            if (verbose) Console.WriteLine("[ERROR] - Modulo exception");
                                            throw new Exception();
                                        }
                                        
                                        //Extract Channel-Value pairs from DMX command, check them and add them to a list
                                        for (int i = 1; i < ((dmxCommand.Length-1)/2)+1; i++) {
                                            channel = Int16.Parse(dmxCommand[i*2-1]); //Channel
                                            value =  byte.Parse(dmxCommand[i*2]); //Value

                                            //Check if channel is blocked
                                            if (blockedChannels.Contains(val1)) {
                                                if (verbose) Console.WriteLine("[DEBUG] - Channel {0} is blocked atm", channel);
                                                continue;
                                            }

                                            //Console.WriteLine("{0} -> Val1: {1}, Val2: {2}", i, val1, val2);
                                            if (channel < 0 || channel > 513 || value < 0 || value > 255) {
                                                throw new Exception();
                                            }
                                            dmxChannelL.Add(channel);
                                            dmxValueL.Add(value);
                                        }

                                        //Itterate over list and set the channels->values
                                        //We do this in a seperate loop to ensure that the whole DMXCommand was valid (1 fails ->all fail)
                                        for (int i = 0; i < dmxChannelL.Count; i++) {
                                            if (verbose) Console.WriteLine("[DEBUG] - Setting: {0} -> {1}", dmxChannelL[i], dmxValueL[i]);
                                            OpenDMX.setDmxValue(dmxChannelL[i], dmxValueL[i]);
                                        }
                                    } catch {
                                        if (verbose) Console.WriteLine("[ERROR] - Malformed DMXCommand: {0}", temp);
                                    }

                                    dmxVal1.Clear();
                                    dmxVal2.Clear(); 
                                } else {
                                    if (verbose) Console.WriteLine("[ERROR] - Command not supported: {0}", temp);
                                }
                            }
                        }
                    }
                    if (verbose) Console.WriteLine("[DEBUG] - Connection to DMXClient lost...");
                } while (stayUp);
            if (verbose) Console.WriteLine("[DEBUG] - Closing...");
            Thread.Sleep(500); //Sleep so that the last received command can still be executed from the worker thread
            OpenDMX.stop();
            Environment.Exit(Environment.ExitCode);
        }

        static void ShowHelp() {
            Console.WriteLine("Usage: DMXServer [OPTIONS]");
            Console.WriteLine("DMXServer is the part of PODU that interfaces with the OPEN DMX USB.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h     Show this screen.");
            Console.WriteLine("  -n     Name of named pipe (must be same as used by DMXClient).");
            Console.WriteLine("  -v     Show verbose output.");
            Console.WriteLine("  -s     Wait for another connection after a DMXClient disconnects.");
        }
    }


    //The Class that interfaces with the OPEN DMX USB taken from enttecs website: https://www.enttec.com.au/product/lighting-communication-protocols/open-dmx-usb/
    //All new methods are commented
    public class OpenDMX {
        public static byte[] buffer = new byte[513];
        public static uint handle;
        public static bool done = false;
        public static int bytesWritten = 0;
        public static FT_STATUS status;

        public const byte BITS_8 = 8;
        public const byte STOP_BITS_2 = 2;
        public const byte PARITY_NONE = 0;
        public const UInt16 FLOW_NONE = 0;
        public const byte PURGE_RX = 1;
        public const byte PURGE_TX = 2;
  

        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_Open(UInt32 uiPort, ref uint ftHandle);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_Close(uint ftHandle);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_Read(uint ftHandle, IntPtr lpBuffer, UInt32 dwBytesToRead, ref UInt32 lpdwBytesReturned);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_Write(uint ftHandle, IntPtr lpBuffer, UInt32 dwBytesToRead, ref UInt32 lpdwBytesWritten);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_SetDataCharacteristics(uint ftHandle, byte uWordLength, byte uStopBits, byte uParity);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_SetFlowControl(uint ftHandle, char usFlowControl, byte uXon, byte uXoff);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_GetModemStatus(uint ftHandle, ref UInt32 lpdwModemStatus);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_Purge(uint ftHandle, UInt32 dwMask);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_ClrRts(uint ftHandle);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_SetBreakOn(uint ftHandle);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_SetBreakOff(uint ftHandle);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_GetStatus(uint ftHandle, ref UInt32 lpdwAmountInRxQueue, ref UInt32 lpdwAmountInTxQueue, ref UInt32 lpdwEventStatus);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_ResetDevice(uint ftHandle);
        [DllImport("FTD2XX.dll")]
        public static extern FT_STATUS FT_SetDivisor(uint ftHandle, char usDivisor);


        public static void start()
        {
            handle = 0;
            status = FT_Open(0, ref handle);
            Thread thread = new Thread(new ThreadStart(writeData));
            //thread.IsBackground = true;  
            thread.Start();
            setDmxValue(0, 0);  //Set DMX Start Code
        }

        public static void stop()
        {
            done = true;
            FT_Close(handle);
        }

        public static void setDmxValue(int channel, byte value)
        {
            buffer[channel] = value;
        }

        public static void writeData()
        {
            while (!done)
            {
                initOpenDMX();
                FT_SetBreakOn(handle);
                FT_SetBreakOff(handle);
                bytesWritten = write(handle, buffer, buffer.Length);
                Thread.Sleep(100);
            }

        }

        public static int write(uint handle, byte[] data, int length){
            IntPtr ptr = Marshal.AllocHGlobal((int)length);
            Marshal.Copy(data, 0, ptr, (int)length);
            uint bytesWritten = 0;
            status = FT_Write(handle, ptr, (uint)length, ref bytesWritten);
            return (int)bytesWritten;
        }

        public static void initOpenDMX()
        {
            status = FT_ResetDevice(handle);
            status = FT_SetDivisor(handle, (char)12);  // set baud rate
            status = FT_SetDataCharacteristics(handle, BITS_8, STOP_BITS_2, PARITY_NONE);
            status = FT_SetFlowControl(handle, (char)FLOW_NONE, 0, 0);
            status = FT_ClrRts(handle);
            status = FT_Purge(handle, PURGE_TX);
            status = FT_Purge(handle, PURGE_RX);
        }

    }


    /// <summary>
    /// Enumaration containing the varios return status for the DLL functions.
    /// </summary>
    public enum FT_STATUS
    {
        FT_OK = 0,
        FT_INVALID_HANDLE,
        FT_DEVICE_NOT_FOUND,
        FT_DEVICE_NOT_OPENED,
        FT_IO_ERROR,
        FT_INSUFFICIENT_RESOURCES,
        FT_INVALID_PARAMETER,
        FT_INVALID_BAUD_RATE,
        FT_DEVICE_NOT_OPENED_FOR_ERASE,
        FT_DEVICE_NOT_OPENED_FOR_WRITE,
        FT_FAILED_TO_WRITE_DEVICE,
        FT_EEPROM_READ_FAILED,
        FT_EEPROM_WRITE_FAILED,
        FT_EEPROM_ERASE_FAILED,
        FT_EEPROM_NOT_PRESENT,
        FT_EEPROM_NOT_PROGRAMMED,
        FT_INVALID_ARGS,
        FT_OTHER_ERROR
    };

}