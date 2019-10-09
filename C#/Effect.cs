using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Linq;


//Dont forget DMX.tickSpeed!!!

namespace Effects {

    public class MainClass {

        public static List<string> activeEffects = new List<string>();
        public static void Main() {
            
            DMX.start();
            // Console.Write("Waiting for it...");
            // Thread.Sleep(5000);
            // Console.WriteLine("Let`s Go!");

            bool verbose = true;
            string input;
            string inp;

            List<int> dmxVal1 = new List<int>(); //time
            List<int> dmxVal2 = new List<int>(); //channel
            List<byte> dmxVal3 = new List<byte>(); //value

            string effectName;

            int val1;
            int val2;
            byte val3;

            //EFFECT 1500 18 12 -> in 1500ms channel 18 to value 12
// EFFECT T 12500 5 100 10000 6 255
            // Dictionary<string, string> openWith = new Dictionary<string, string>();
            // DMX.start();
            while (true) {
                Console.Write("Befehl: ");
                inp = Console.ReadLine();
                if (inp.StartsWith("EFFECT ")) {
                    input = inp.Substring(7); //Cut out the 'EFFECT '
                    try {
                        List<string> effectCommand = input.Split().ToList();
                        effectName = effectCommand.First();
                        effectCommand.RemoveAt(0);

                        if ((effectCommand.Count%3) != 0 || activeEffects.Contains(effectName)) {
                            Console.WriteLine("Modulo or effectName exception");
                            throw new Exception();
                        }

                        activeEffects.Add(effectName);
                        // EFFECT a 12500 5 100 20000 2 10 1000 8 255 500 1 128
                        for (int i = 0; i < (effectCommand.Count/3); i++) {
                            //Console.WriteLine("Val1: {0}, Val2: {1}, Val3: {2}", effectCommand[3*i], effectCommand[3*i+1], effectCommand[3*i+2]);
                            val1 = Int16.Parse(effectCommand[3*i]);
                            val2 =  Int16.Parse(effectCommand[3*i+1]);
                            val3 =  byte.Parse(effectCommand[3*i+2]);
                            //Console.WriteLine("{0} -> Val2: {1}, Val3: {2}, ", val1, val2, val3);
                            if ( val1 < 100 || val1 % 100 != 0 || val2 < 0 || val2 > 513 || val3 < 0 || val3 > 255) {
                                Console.WriteLine("IF exception");
                                throw new Exception();
                            }
                            dmxVal1.Add(val1);
                            dmxVal2.Add(val2);
                            dmxVal3.Add(val3);



                        }
                        
                        EffectAddClass worker = new EffectAddClass(effectName, dmxVal1, dmxVal2, dmxVal3);
                        worker.addEffect();
                        
                        
                        Thread.Sleep(200000);

                        // var watch = System.Diagnostics.Stopwatch.StartNew();
                        // DMX.effectsQueue();
                        // watch.Stop();
                        // Console.WriteLine("Elappsed: {0}", watch.ElapsedMilliseconds);

                        // DMX.effectsQueue();
                        // Console.WriteLine("--------------------");
                        // DMX.effectsQueue();
                        // Console.WriteLine("--------------------");


                        //System.IO.File.WriteAllText("output.txt", DMX.buffer);

                    } catch (Exception e) {
                        if (verbose) Console.WriteLine("[ERROR] - Malformed DMXCommand: {0}; Error: {1}", inp, e);
                    }

                    




                } else {
                    Console.WriteLine("Ungültiger Befehl");
                }

            }



            //Thread.Sleep(500); //Sleep so that the last received command can still be executed from the worker thread
            //Environment.Exit(Environment.ExitCode);
        }
    }


    public class EffectAddClass {


        // ausgang = [10, 0]

        // time = [15000, 20000]
        // channel = [17, 18]
        // value = [128, 255]

        // # dX = neuerWert - ausgangsWert

        // eA = []

        // for i in range(len(time)):
        //     t = time[i]
        //     c = channel[i]
        //     v = value[i]
        //     a = ausgang[i] #Should use channel

        //     diffPL = (v-a)/(t/100) #Difference per loop

        //     s = []
        //     for j in range(int(t/100)):
        //         s.append(c)
        //         s.append(round(diffPL*j)+a)
        //     eA.append(s)

        private string name;
        private List<int> time;
        private List<int> channel;
        private List<byte> value;

        public EffectAddClass(string name, List<int> time, List<int> channel, List<byte> value) {
            this.name = name;
            this.time = time;
            this.channel = channel;
            this.value = value;
        }
        public void addEffect() {
            List<object> effectList = new List<object>();
            int t;
            int c;
            int a;
            int v;
            double diffPL;
            int tD; //tickspeed divisor
            for (int i = 0; i < time.Count; i++) {
                a = DMX.buffer[i];
                v = value[i];
                if (v == a) continue;
                t = time[i];
                c = channel[i];
                tD = t/DMX.tickSpeed;
                diffPL = (double)(value[i]-a)/(double)tD; //We need the cast to double so that we dont get an int as a result
                //Console.WriteLine("diffPL: {0}, tD: {1}, value: {2}, a: {3}, = {4}", diffPL, tD, value[i], a, (double)(value[i]-a)/(double)tD);

                List<object> s = new List<object>();

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
        public static Dictionary<string, List<object>> effectsDict = new Dictionary<string, List<object>>();
        static int channel;
        static byte value;

        public static List<string> effectsToRemove = new List<string>();

        public static int tickSpeed = 100;

        public static void effectsQueue() {
            bool delFlag;
            foreach(KeyValuePair<string, List<object>> entry in effectsDict)
            {
                delFlag = true;

                //EFFECT D 12500 5 100 15000 6 128

                foreach (List<object> i in entry.Value) {
                    if (i.Count > 0) {
                        delFlag = false;
                        channel = Convert.ToInt32(i.First());
                        i.RemoveAt(0);
                        value = Convert.ToByte(i.First());
                        i.RemoveAt(0);
                        //set channel and value

                        //Console.WriteLine("Channel: {0}, Value: {1}", channel, value);
                        //setDmx(channel, value);

                    }
                }

                if (delFlag) effectsToRemove.Add(entry.Key);
                // do something with entry.Value or entry.Key
            }

            foreach(string i in effectsToRemove) {
                effectsDict.Remove(i);
                MainClass.activeEffects.Remove(i);
            }
        }


        public static void start() {
            Thread thread = new Thread(new ThreadStart(write));
            thread.Start();
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