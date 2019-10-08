using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Effects {

    public class MainClass {
        public static void Main() {
            DMX.start();

            bool verbose = true;
            string input;

            List<int> dmxVal1 = new List<int>(); //time
            List<int> dmxVal2 = new List<int>(); //channel
            List<byte> dmxVal3 = new List<byte>(); //value

            string effectName;

            int val1;
            int val2;
            byte val3;

            //EFFECT 1500 18 12 -> in 1500ms channel 18 to value 12

            // Dictionary<string, string> openWith = new Dictionary<string, string>();

            while (true) {
                Console.Write("Befehl: ");
                input = Console.ReadLine();
                if (input.StartsWith("EFFECT ")) {
                    input = input.Substring(7); //Cut out the 'EFFECT '
                    try {
                        List<string> effectCommand = input.Split().ToList();
                        effectName = effectCommand.First();
                        effectCommand.RemoveAt(0);
                        
                        if ((effectCommand.Count%3) != 0) {
                            Console.WriteLine("Modulo exception");
                            throw new Exception();
                        }


                        for (int i = 0; i < (effectCommand.Count/3); i++) {
                            val1 = Int16.Parse(effectCommand[i]);
                            val2 =  Int16.Parse(effectCommand[i+1]);
                            val3 =  byte.Parse(effectCommand[i+2]);
                            Console.WriteLine("{0} -> Val2: {1}, Val3: {2}, ", val1, val2, val3);
                            if ( val1 < 100 || val1 % 100 != 0 || val2 < 0 || val2 > 513 || val3 < 0 || val3 > 255) {
                                Console.WriteLine("IF exception");
                                throw new Exception();
                            }
                            dmxVal1.Add(val1);
                            dmxVal2.Add(val2);
                            dmxVal3.Add(val3);
                        }
                        for (int i = 0; i < dmxVal1.Count; i++) {
                            //Console.WriteLine("Setting: {0} -> {1}", dmxVal1[i], dmxVal2[i]);
                            //OpenDMX.setDmxValue(dmxVal1[i], dmxVal2[i]);
                            Console.WriteLine("Name: " + effectName + " Time: " + dmxVal1[i] + " Channel: " + dmxVal2[i] + " Value: " + dmxVal3[i]);
                        }
                    } catch {
                        if (verbose) Console.WriteLine("[ERROR] - Malformed DMXCommand: {0}", input);
                    }

                    




                } else {
                    Console.WriteLine("Ungültiger Befehl");
                }

            }



            //Thread.Sleep(500); //Sleep so that the last received command can still be executed from the worker thread
            //Environment.Exit(Environment.ExitCode);
        }
    }


    public class DMX {

        public static void start() {
            Thread thread = new Thread(new ThreadStart(write));
            thread.Start();
        }

        public static void write() {
            while (true) {
                
                Thread.Sleep(100);
            }
        }
    }



}