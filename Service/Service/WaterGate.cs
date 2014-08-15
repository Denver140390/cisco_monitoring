﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using SnmpSharpNet;
using System.Threading;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO.Compression;

namespace Service
{
 
    public partial class WaterGate : ServiceBase
    {
        private readonly Thread workerThread;
        private readonly Thread TCPlistenerthread;
        
        public WaterGate()
        {
            InitializeComponent();



            workerThread = new Thread(DoWork);
            workerThread.SetApartmentState(ApartmentState.STA);


            TCPlistenerthread = new Thread(TCPlistener);
            TCPlistenerthread.SetApartmentState(ApartmentState.STA);

        }

      
        protected override void OnStart(string[] args)
        {
            #if DEBUG
                if(!System.Diagnostics.Debugger.IsAttached)
                   System.Diagnostics.Debugger.Launch();
            #endif


            Functions.SerializeConfig(Functions.pathConfig);

                AddLog("WaterGate Service started");
                string fname = @"C:\temp\temp.txt";
                using (StreamWriter stream = new StreamWriter(fname, true))
                {
                    stream.WriteLine("Служба запущена!");
                    stream.WriteLine(DateTime.Now);
                }
            
                workerThread.Start();
                TCPlistenerthread.Start();

        }

        protected override void OnStop()
        {
            AddLog("Service is stopped");
            string fname = @"C:\temp\temp.txt";
            using (StreamWriter stream = new StreamWriter(fname, true))
            {
                stream.WriteLine("Служба остановлена!");
                stream.WriteLine(DateTime.Now);
            }
            workerThread.Abort();
            TCPlistenerthread.Abort();

        }

        private static void TCPlistener()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 32157);
            EndPoint ep = (EndPoint)ipep;
            socket.Bind(ep);
            // Disable timeout processing. Just block until packet is received 
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 0);

            while (true)
            {
                byte[] indata = new byte[16 * 1024];
                // 16KB receive buffer 
                int inlen = 0;
                IPEndPoint peer = new IPEndPoint(IPAddress.Any, 0);
                EndPoint inep = (EndPoint)peer;
                try
                {
                    inlen = socket.ReceiveFrom(indata, ref inep);
                }
                catch (Exception ex)
                {
                    Functions.AddTempLog(new List<string> { ex.Message, ex.ToString() });
                   
                    inlen = -1;
                }
                if (inlen > 0)
                {
                    try
                    {
                        StaticValuesDll.JDSUCiscoClass ser = new StaticValuesDll.JDSUCiscoClass();
                        ser = null;
                      
                        var formatter = new BinaryFormatter();
                        using (var ms = new MemoryStream(indata))
                        {
                            using (var ds = new DeflateStream(ms, CompressionMode.Decompress, true))
                            {
                                ser = (StaticValuesDll.JDSUCiscoClass)formatter.Deserialize(ds);
                            }
                        }

                        Functions.AddTempLog(new List<string> { ser.JDSUPort });
                    }
                    catch (Exception ex)
                    {
                        Functions.AddTempLog(new List<string> { ex.Message, ex.ToString() });
                    }


                   Functions.AddTempLog(new List <string>  {Encoding.ASCII.GetString(indata)});
                    
                
                }
            }
        
        }

       
        private static void DoWork()
        {
           

            // Construct a socket and bind it to the trap manager port 162 

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 162);
            EndPoint ep = (EndPoint)ipep;
            socket.Bind(ep);
            // Disable timeout processing. Just block until packet is received 
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 0);

            while (true)
            {
                byte[] indata = new byte[16 * 1024];
                // 16KB receive buffer 
                int inlen = 0;
                IPEndPoint peer = new IPEndPoint(IPAddress.Any, 0);
                EndPoint inep = (EndPoint)peer;
                try
                {
                    inlen = socket.ReceiveFrom(indata, ref inep);
                }
                catch (Exception ex)
                {
                  Functions.AddTempLog(new List <string>  {ex.Message, ex.ToString()});
                    inlen = -1;
                }
                if (inlen > 0)
                {
                    // Check protocol version 
                    int ver = SnmpPacket.GetProtocolVersion(indata, inlen);
                    if (ver == (int)SnmpVersion.Ver2)
                    {

                        // Parse SNMP Version 2 TRAP packet 
                        SnmpV2Packet pkt = new SnmpV2Packet();
                        pkt.decode(indata, inlen);
                        List<string> trap = new List<string> { "*** SNMP Version 2 TRAP received from ", inep.ToString(), "*** Community", pkt.Community.ToString(), "*** VarBind content:" };
                        foreach (Vb v in pkt.Pdu.VbList)
                        {
                            trap.Add(v.Oid.ToString());
                            trap.Add(SnmpConstants.GetTypeName(v.Value.Type));
                            trap.Add(v.Value.ToString());
                        }
                        trap.Add("*** End of SNMP Version 2 TRAP data.");
                        Functions.AddTempLog(trap);

                        StaticValuesDll.AlarmClass alarm = new StaticValuesDll.AlarmClass();
                        alarm.ClearStatus = 0;
                        alarm.DateTime = DateTime.Now;
                        alarm.inep = new StaticValuesDll.IPCom(inep.ToString(), pkt.Community.ToString());
                        alarm.link = "not set";
                        //Functions.SendAlarm(alarm);
                        
                    }
                    
                }
                else
                {
                    if (inlen == 0)
                    { 
                        Functions.AddTempLog(new List <string> {"Zero length packet received."});
                    }
                    
                    
                }
          
            }
        }

        public void AddLog(string log)
        {
            try
            {
                if (!EventLog.SourceExists("WaterGateService"))
                {
                    EventLog.CreateEventSource("WaterGateService", "WaterGateService");
                }

                eventLog1.Source = "WaterGateservice";
                eventLog1.WriteEntry(log);
            }
            catch { }
        }

       
    }
}
