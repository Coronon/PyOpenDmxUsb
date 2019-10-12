using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Effects {

    public class MainClass {

        public static Dictionary<string, List<int>> activeEffects = new Dictionary<string, List<int>>(); //Dict of all active effects and the channels they block
        public static List<int> blockedChannels = new List<int>(); //List of all blocked channels that cannot the controlled manually

        public static bool verbose = true;
        public static void Main() {
            DMX.start();
            string inp;

            while (true) {
                Console.Write("Befehl: ");
                inp = Console.ReadLine();
                try {
                    if (inp.StartsWith("EFFECT ")) {
                        command_EFFECT(inp);
                    } else {
                            Console.WriteLine("Ungültiger Befehl");
                        }

                } catch (Exception e) {
                    if (verbose) Console.WriteLine("[ERROR] - Malformed DMXCommand: {0}; Error: {1}", inp, e);
                }


            }
        }
    
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
                //Block channel for duration of effect
                blockedChannels.Add(channel);

                //Console.WriteLine("{0} -> channel: {1}, value: {2}, ", time, channel, value);

                //Check if values in allowed range
                if ( time < 100 || time % 100 != 0 || channel < 0 || channel > 513 || value < 0 || value > 255) {
                    Console.WriteLine("IF exception");
                    throw new Exception();
                }

                //Add time, channel and value to their own list
                dmxTimeL.Add(time);
                dmxChannelL.Add(channel);
                dmxValueL.Add(value);
            }
            //Insert the channel list into the active effects list to unblock channels after the effect ends
            activeEffects[effectName] = dmxChannelL;

            //TODO: See if this is efficient -> maybe move to normal method
            //Spawn a new thread that precomputes the dmx values and makes them ready for execution
            EffectAddClass worker = new EffectAddClass(effectName, dmxTimeL, dmxChannelL, dmxValueL);
            Thread EAthread = new Thread(new ThreadStart(worker.addEffect));
            EAthread.Start();
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
                a = DMX.buffer[i];
                v = value[i];
                if (v == a) continue; //Skip if current value is the same as the value we are supposed to approach
                t = time[i];
                c = channel[i];
                tD = t/DMX.tickSpeed;
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
            DMX.effectsDict.Add(name, effectList);
        }
    }


    public class DMX {
        public static byte[] buffer = new byte[10];
        public static Dictionary<string, List<object>> effectsDict = new Dictionary<string, List<object>>(); //Dict that stores all current effects

        public static int tickSpeed = 100; //The tick speed for setting new DMX Values

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
        }


        public static void start() {
            Thread thread = new Thread(new ThreadStart(write));
            thread.Start();
        }

        public static void setDmxValue(int channel, byte value)
        {
            buffer[channel] = value;
        }

        public static void write() {
            while (true) {
                // var watch = System.Diagnostics.Stopwatch.StartNew();
                effectsQueue();
                // watch.Stop();
                // Console.WriteLine("Elappsed: {0}", watch.ElapsedMilliseconds);
                Thread.Sleep(tickSpeed);
            }
        }
    }



}