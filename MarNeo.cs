using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MarNEO
{
    [ExternalTool("MarNeo")]
    public class MarNeo : ToolFormBase, IExternalToolForm
    {
        public const int ACTION_INTERVAL = 3; // frames

        public ApiContainer? _maybeAPIContainer { get; set; }
        private ApiContainer APIs => _maybeAPIContainer!;

        protected override string WindowTitleStatic => "MarNeo";

        private bool initialized;
        private int frameCounter;

        private int listenPort;
        private Socket listenerSocket;
        private Socket clientSocket;
        private string envId;

        private byte[] msgLenBuf;
        byte[] msgBuf;

        public MarNeo()
        {
            initialized = false;
            msgLenBuf = new byte[4];
            msgBuf = new byte[1024];
        }

        public override void Restart()
        {
        }

        protected override void UpdateAfter()
        {
            if (Visible)
            {
                Hide(); // dialog not used
            }

            var gameInfo = APIs.GameInfo.GetGameInfo();
            if (gameInfo.System == "NULL" || APIs.EmuClient.IsPaused())
            {
                if (initialized)
                {
                    throw new Exception("unexpected state (game should not be paused while connected)");
                }
                return; // not ready yet
            }

            --frameCounter;

            bool firstTime = false;
            if (!initialized)
            {
                // this can be used to control the speed of the emulation
                APIs.EmuClient.SpeedMode(100);

                envId = Environment.GetEnvironmentVariable("MARNEO_ID");
                string envAddr = Environment.GetEnvironmentVariable("MARNEO_ADDR");
                string envPort = Environment.GetEnvironmentVariable("MARNEO_PORT");
                if (envId == null || envAddr == null || envPort == null)
                {
                    throw new Exception("missing required env vars");
                }
                if (!int.TryParse(envPort, out listenPort))
                {
                    throw new Exception("Invalid MARNEO_PORT");
                }
                Console.WriteLine("hosting " + envId + " on " + envAddr + ":" + envPort);
                IPAddress ipAddress = IPAddress.Parse(envAddr);
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, listenPort);
                listenerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                listenerSocket.Bind(localEndPoint);
                listenerSocket.Listen(10);
                listenerSocket.ReceiveTimeout = 120000;
                clientSocket = listenerSocket.Accept();
                firstTime = true;
                initialized = true;

                IList<float> initObs = CollectObservations();
                SendMessage(new
                {
                    ready = true,
                    observation = initObs
                });

                frameCounter = ACTION_INTERVAL;
            }

            IList<float> obs;
            DateTime now = DateTime.Now;
            if (firstTime || frameCounter == 0)
            {
                if (!firstTime)
                { 
                    // evaluate the last action performed
                    float reward;
                    bool done;
                    EvaluateAction(out reward, out done);

                    if (done)
                    {
                        SendMessage(new
                        {
                            reward = reward,
                            done = true
                        });
                    } else
                    {
                        obs = CollectObservations();
                        SendMessage(new
                        {
                            observation = obs,
                            reward = reward,
                            done = false
                        });
                    }
                }
                for (; ;)
                {
                    int nRec = clientSocket.Receive(msgLenBuf, 4, SocketFlags.None);
                    if (nRec != 4)
                    {
                        throw new Exception("unexpected # bytes received");
                    }

                    int msgLen = BitConverter.ToInt32(msgLenBuf, 0);
                    if (msgLen > msgBuf.Length)
                    {
                        msgBuf = new byte[msgLen];
                    }

                    nRec = clientSocket.Receive(msgBuf, msgLen, SocketFlags.None);
                    if (nRec != msgLen)
                    {
                        throw new Exception("unexpected # bytes received");
                    }

                    string strMsg = Encoding.UTF8.GetString(msgBuf, 0, msgLen);
                    JObject msg = JObject.Parse(strMsg);
                    if (msg.ContainsKey("wait"))
                    {
                        continue;
                    }
                    int actionId = msg["action"].ToObject<int>();
                    PerformAction(actionId);
                    frameCounter = ACTION_INTERVAL;
                    break;
                }
            }
        }

        private void PerformAction(int actionId)
        {
            if (actionId == 0)
            {
                // do nothing
                return;
            }

            // if you change the number of actions you must update action_space in the python script

            const int MIN_ACTION_ADDR = 0x1;
            const int MAX_ACTION_ADDR = 0x100;

            int targetAddr = MIN_ACTION_ADDR + (actionId - 1);
            if (targetAddr < MIN_ACTION_ADDR || targetAddr > MAX_ACTION_ADDR)
            {
                throw new Exception("invalid action " + actionId + " (expected range 1-" + 
                    (MAX_ACTION_ADDR-MIN_ACTION_ADDR+1) + ")");
            }
            uint nextVal = (APIs.Memory.ReadByte(targetAddr) + 1u) % 256u;
            APIs.Memory.WriteByte(targetAddr, nextVal);
        }

        private IList<float> CollectObservations()
        {
            // if you change this you need to update the python script's observation_space too
            List<byte> raw = APIs.Memory.ReadByteRange(0x0, 0x100); 
            return raw.Select(b => b / 255.0f).ToList();
        }

        private void EvaluateAction(out float reward, out bool done)
        {
            // TODO
            reward = 0.0f;
            done = false;
        }

        private void SendMessage(object msg)
        {
            string s = JsonConvert.SerializeObject(msg);
            byte[] b = Encoding.UTF8.GetBytes(s);
            byte[] l = BitConverter.GetBytes(b.Length);
            int count = clientSocket.Send(l);
            if (count != l.Length)
            {
                throw new Exception("failed to send all of message length");
            }
            count = clientSocket.Send(b);
            if (count != b.Length)
            {
                throw new Exception("failed to send all of message");
            }
        }
    }
}
