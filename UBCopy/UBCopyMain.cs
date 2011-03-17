﻿//
// UBCopyMain.cs
//
// Authors:
//  Wesley D. Brown <wes@planetarydb.com>
//
// Copyright (C) 2010 SQLServerIO (http://www.SQLServerIO.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

//TODO: add in directory copy
//TODO: add in retry/copy restart maybe utilize a WAL type file structure? Would require resetting the length?
//TODO: Yet more error checking!
//TODO: Command line compattible with robocopy?
//TODO: Command line compattible with xcopy?
//TODO: Command line compattible with copy?

using System;
using System.Diagnostics;
using System.IO;
using NDesk.Options;
using log4net;

namespace UBCopy
{
    class UBCopyMain
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UBCopyMain));
        private static readonly bool IsDebugEnabled = Log.IsDebugEnabled;

        //hold command line options
        private static string _sourcefile;
        private static string _destinationfile;
        private static bool _overwritedestination = true;
        //we set an inital buffer size to be on the safe side.
        private static int _buffersize = 16;
        private static bool _checksumfiles;
        private static bool _reportprogres;
        private static bool _movefile;

        private static int Main(string[] args)
        {
            if (IsDebugEnabled)
            {
                Log.DebugFormat("ArchiveTable started at {0}", DateTime.Now);
            }

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Console.WriteLine("UBCopy " + version);
            if (IsDebugEnabled)
            {
                Log.Debug("Version " + version);
            }
            var parseerr = ParseCommandLine(args);
            if (parseerr == 1)
            {
                return 0;
            }
            try
            {
                if (IsDebugEnabled)
                {
                    Log.Debug("Environment.UserInteractive: " + Environment.UserInteractive);
                }
                //if you are running without an interactive command shell then we disable the fancy reporting feature
                if (Environment.UserInteractive == false)
                {
                    _reportprogres = false;
                }

                var f = new FileInfo(_sourcefile);
                long fileSize = f.Length;

                var sw = new Stopwatch();
                sw.Start();
                try
                {

                AsyncUnbuffCopy.AsyncCopyFileUnbuffered(_sourcefile, _destinationfile, _overwritedestination,
                                                            _movefile,
                                                            _checksumfiles, _buffersize, _reportprogres);
                }
                catch (Exception ex)
                {
                    if (IsDebugEnabled)
                    {
                        Log.Error(ex);
                    }
                    throw;
                }

                sw.Stop();
                if (IsDebugEnabled)
                {
                    Log.Debug("ElapsedMilliseconds: " + sw.ElapsedMilliseconds);
                    Log.Debug("ElapsedSeconds     : " + (fileSize/(float) sw.ElapsedMilliseconds/1000.00));
                    Log.DebugFormat("File Size MB     : {0}", Math.Round(fileSize / 1024.00 / 1024.00, 2));
                    Log.DebugFormat("Elapsed Seconds  : {0}", sw.ElapsedMilliseconds / 1000.00);
                    Log.DebugFormat("Megabytes/sec    : {0}", Math.Round(fileSize / (float)sw.ElapsedMilliseconds / 1000.00, 2));
                    Log.Debug("Done.");

                }
                Console.WriteLine("File Size MB     : {0}",Math.Round(fileSize/1024.00/1024.00,2));
                Console.WriteLine("Elapsed Seconds  : {0}", sw.ElapsedMilliseconds / 1000.00);
                Console.WriteLine("Megabytes/sec    : {0}", Math.Round(fileSize / (float)sw.ElapsedMilliseconds / 1000.00, 2));


                Console.WriteLine("Done.");

#if DEBUG
                {
                    Console.ReadKey();
                }
#endif
                return 1;

            }
            catch (Exception e)
            {
                Log.Fatal("File Copy Aborted!");
                Log.Fatal(e);

                Console.WriteLine("Error: File copy aborted");
                Console.WriteLine(e.Message);
                return 0;
            }

        }

        static public int ParseCommandLine(string[] args)
        {
            bool showHelp = false;

            var p = new OptionSet
                        {
                          { "s:|sourcefile:", "The file you wish to copy",
                          v => _sourcefile = v },

                          { "d:|destinationfile:", "The target file you wish to write",
                          v => _destinationfile = v},

                          { "o:|overwritedestination:", "True if you want to overwrite the destination file if it exists",
                          (bool v) => _overwritedestination = v},

                          { "m:|movefile:", "True if you want to copy the file to new location and delete from the old location",
                          (bool v) => _movefile = v},

                          { "c:|checksum:", "True if you want use MD5 hash to verify the destination file is the same as the source file",
                          (bool v) => _checksumfiles = v},

                          { "b:|buffersize:", "size in Megabytes, maximum of 32",
                          (int v) => _buffersize = v},

                          { "r:|reportprogress:", "True give a visual indicator of the copy progress",
                          (bool v) => _reportprogres = v},

                          { "?|h|help",  "show this message and exit", 
                          v => showHelp = v != null },
                        };

            try
            {
                p.Parse(args);
            }

            catch (OptionException e)
            {
                Console.Write("UBCopy Error: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `UBCopy --help' for more information.");
                return 1;
            }

            if (args.Length == 0)
            {
                ShowHelp("Error: please specify some commands....", p);
                return 1;
            }

            if (_sourcefile == null || _destinationfile == null && !showHelp)
            {
                ShowHelp("Error: You must specify a file to copy (-s) and a file to copy to (-d).", p);
                return 1;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return 1;
            }
            return 0;
        }

        static void ShowHelp(string message, OptionSet p)
        {
            Console.WriteLine(message);
            Console.WriteLine("Usage: UBCopy [OPTIONS]");
            Console.WriteLine("copy files using unbuffered IO and asyncronus buffers");
            Console.WriteLine();
            Console.WriteLine("Options: ");
            p.WriteOptionDescriptions(Console.Out);
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: UBCopy [OPTIONS]");
            Console.WriteLine("copy files using unbuffered IO and asyncronus buffers");
            Console.WriteLine();
            Console.WriteLine("Options: ");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
