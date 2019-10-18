using System;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Linq;




namespace DMXServer
{

    public class MainClass {
        //Settings
        static bool verbose = false; //If the Server should output verbose messages
        static bool stayUp = false; //If the Server should quit if all Clients disconnect
        static bool show_help = false; //Helper flag for showing the help text (-h)
        static string pipeName = ""; //The name of the namedPipe to listen for

        //Variables
        public static List<int> blockedChannels = new List<int>(); //Contains all channels that are blocked for any reason (Effects, etc...)
        public static Dictionary<string, List<int>> activeEffects = new Dictionary<string, List<int>>(); //Dict of all active effects and the channels they block

        //Main method the programm launches into
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
                            string input; //Holds the string recieved over the named pipe

                            //Command handling
                            while ((input = sr.ReadLine()) != null) {
                                try { //Errors indicate malformed command
                                    if (input.StartsWith("DMX ")) { // 'DMX ...'
                                        command_DMX(input);
                                    } else if (input.StartsWith("EFFECT ")) { // 'EFFECT ...'
                                        command_EFFECT(input);
                                    } else {
                                        if (verbose) Console.WriteLine("[ERROR] - Command not supported: {0}", input);
                                    }
                                } catch {
                                    if (verbose) Console.WriteLine("[ERROR] - Malformed DMXCommand: {0}", input);
                                }
                            }
                        }
                    if (verbose) Console.WriteLine("[DEBUG] - Connection to DMXClient lost...");
                }
            } while (stayUp);
            if (verbose) Console.WriteLine("[DEBUG] - Closing...");
            Thread.Sleep(500); //Sleep so that the last received command can still be executed from the worker thread
            OpenDMX.stop();
            Environment.Exit(Environment.ExitCode);
        }

        //Shows the help text (-h)
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

        //Basic DMX command TODO: Implement Substring to cut out the 'DMX ' for easyer usage
        static void command_DMX(string input) {
            string[] dmxCommand; //Stores the elements of the command
            List<int> dmxChannelL = new List<int>(); //List of the DMX channels
            List<byte> dmxValueL = new List<byte>(); //List of the DMX values
            int channel; //The channelof a subcommand
            byte value; //The value of a subcommand

            dmxCommand = input.Split(); //Split the command into its elements
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
                if (blockedChannels.Contains(channel)) {
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
        }
        
        
        //Effects for DMX
        static void command_EFFECT(string input) {
            List<int> dmxTimeL = new List<int>(); //time
            List<int> dmxChannelL = new List<int>(); //channel
            List<byte> dmxValueL = new List<byte>(); //value

            string effectName; //name of the effect

            int time; //Time of a subeffect
            int channel; //Channel of a subeffect
            byte value; //Value of a subeffect


            List<string> effectCommand = input.Substring(7).Split().ToList(); //Cut out the 'EFFECT ', split times, channels and values into list
            effectName = effectCommand.First(); //first element in the list is the name
            effectCommand.RemoveAt(0); //get rid of the name in the list

            if ((effectCommand.Count%3) != 0 || activeEffects.ContainsKey(effectName)) { //Check if every value has channel and time and if effect name already taken
                Console.WriteLine("Modulo or effectName exception");
                throw new Exception();
            }

            activeEffects.Add(effectName, new List<int>()); //Add the effect name to active effects to block another effect with the same name
            for (int i = 0; i < (effectCommand.Count/3); i++) { //Loop over time, channel, value list and split them into their own lists
                //Console.WriteLine("time: {0}, channel: {1}, value: {2}", effectCommand[3*i], effectCommand[3*i+1], effectCommand[3*i+2]);

                //Convert elements to their data type
                time = Int16.Parse(effectCommand[3*i]); 
                channel =  Int16.Parse(effectCommand[3*i+1]);
                value =  byte.Parse(effectCommand[3*i+2]);

                //Check if channel is blocked
                if (blockedChannels.Contains(channel)) {
                    if (verbose) Console.WriteLine("Channel {0} is blocked atm", channel);
                    activeEffects.Remove(effectName);
                    throw new Exception();
                }

                //Console.WriteLine("{0} -> channel: {1}, value: {2}, ", time, channel, value);

                //Check if values in allowed range
                if ( time < 0 || time % 100 != 0 || channel < 0 || channel > 513 || value < 0 || value > 255) {
                    Console.WriteLine("IF exception");
                    activeEffects.Remove(effectName);
                    throw new Exception();
                }
                if (time == 0) time = OpenDMX.tickSpeed; //time=0 will always be fastest

                //Block channel for duration of effect
                blockedChannels.Add(channel);

                //Add time, channel and value to their own list
                dmxTimeL.Add(time);
                dmxChannelL.Add(channel);
                dmxValueL.Add(value);
            }
            //Insert the channel list into the active effects list to unblock channels after the effect ends
            activeEffects[effectName] = dmxChannelL;

            //TODO: See if this is efficient -> maybe move to normal method
            //Spawn a new thread that precomputes the dmx values and makes them ready for execution
            Effect_AddClass worker = new Effect_AddClass(effectName, dmxTimeL, dmxChannelL, dmxValueL);
            Thread EAthread = new Thread(new ThreadStart(worker.addEffect));
            EAthread.Start();
            //Console.WriteLine("EFFECT ADDED");
        }


    }

    //Class that precomutes dmx values for effects
    public class Effect_AddClass {

        private string name;
        private List<int> time;
        private List<int> channel;
        private List<byte> value;


        //Constructor that gets the variables for computing the effect
        public Effect_AddClass(string name, List<int> time, List<int> channel, List<byte> value) {
            this.name = name;
            this.time = time;
            this.channel = channel;
            this.value = value;
        }

        //Method that computes the dmx values based on their timing, current value and the value they should reach
        public void addEffect() {
            List<object> effectList = new List<object>();
            int t; //Time
            int c; //Channel
            int a; //Current value of channel in OpenDMX.buffer
            int v; //Value to reach after effect end
            int tD; //tickspeed divisor
            double diffPL; //Difference of valueToReach and currentValue devided by the tickspeed divisor
            for (int i = 0; i < time.Count; i++) {
                c = channel[i];
                a = OpenDMX.buffer[c];
                v = value[i];
                if (v == a) continue; //Skip if current value is the same as the value we are supposed to approach
                t = time[i];
                tD = t/OpenDMX.tickSpeed;
                diffPL = (double)(value[i]-a)/(double)tD; //We need the cast to double so that we dont get an int as a result
                //Console.WriteLine("diffPL: {0}, tD: {1}, value: {2}, a: {3}, = {4}", diffPL, tD, value[i], a, (double)(value[i]-a)/(double)tD);

                List<object> s = new List<object>(); //List to save the steps in

                //
                for (int j = 1; j < tD+1; j++) {
                    s.Add(c);
                    s.Add(Math.Round(diffPL*j, MidpointRounding.AwayFromZero)+a); // Math.Round(value, MidpointRounding.AwayFromZero))
                    //Console.WriteLine("Channel: {0}, Value: {1}", c, Math.Round(diffPL*j, MidpointRounding.AwayFromZero)+a);
                }
                effectList.Add(s);
                //Console.WriteLine("Last: {0}", s[tD*2-1]);
            }
            //Console.WriteLine("------  GO  ------");
            OpenDMX.effectsDict.Add(name, effectList);
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


        //PODU Variables
        public static Dictionary<string, List<object>> effectsDict = new Dictionary<string, List<object>>(); //Dict that stores all current effects
        public static int tickSpeed = 100; //The tick speed for setting new DMX Values


        //PODU methods
        //Called every OpenDMX tick to apply currently running effects
        public static void effectsQueue() {
            bool delFlag; //Indicate if the effect is over
            int channel; //The channel of an effect
            byte value; //The Value of an effect
            List<string> effectsToRemove = new List<string>(); //Stores all effects that are over and should be deleted
            foreach(KeyValuePair<string, List<object>> entry in effectsDict) //Itterate over all effects
            {
                delFlag = true; //for checking if the effect has no lists that contain new values

                foreach (List<object> i in entry.Value) { //Itterate over all subeffects
                    if (i.Count > 0) { //Check that the list is not empty
                        delFlag = false; //Indicate that there are still subeffects running
                        //Channel
                        channel = Convert.ToInt32(i.First());
                        i.RemoveAt(0);
                        //Value
                        value = Convert.ToByte(i.First());
                        i.RemoveAt(0);

                        //Push value into DMXBuffer at channel
                        //Console.WriteLine("Channel: {0}, Value: {1}", channel, value);
                        setDmxValue(channel, value);

                    }
                }

                if (delFlag) effectsToRemove.Add(entry.Key); //If no subeffects are running anymore add the effect to the remove list
            }


            //Remove all over effects
            foreach(string i in effectsToRemove) {
                //Remove from OpenDMX effectDict
                effectsDict.Remove(i);
                //Remove from MainClass activeEffects and unblock channels
                foreach (int j in MainClass.activeEffects[i]) {
                    MainClass.blockedChannels.Remove(j);
                }
                MainClass.activeEffects.Remove(i);
            }
            effectsToRemove.Clear();
            //Console.WriteLine("Lenght: {0}", effectsDict.Count);
        }

        public static void start()
        {
            handle = 0;
            status = FT_Open(0, ref handle);
            Thread thread = new Thread(new ThreadStart(writeData)); 
            thread.Start();
            setDmxValue(0, 0);  //Set DMX Start Code
        }


        //Normal methods (with changes)
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
                effectsQueue(); //Handles the currently active effects

                initOpenDMX();
                FT_SetBreakOn(handle);
                FT_SetBreakOff(handle);
                bytesWritten = write(handle, buffer, buffer.Length);
                Thread.Sleep(tickSpeed);
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