using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Web;
using System.Windows.Forms;
using System.Xml.Linq;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BizHawk.Emulation.Common;
using BizHawk.Common.NumberExtensions;
using System.Drawing;

namespace MarNEO
{
    [ExternalTool("MarNeo")]
    public class MarNeo : ToolFormBase, IExternalToolForm
    {
        public const int ACTION_INTERVAL = 10; // frames

        public ApiContainer? _maybeAPIContainer { get; set; }
        private ApiContainer APIs => _maybeAPIContainer!;

        protected override string WindowTitleStatic => "MarNeo";

        private int initFrameCounter;
        private int initStateIndex;
        private bool initStateReached;

        private bool initialized;
        private int frameCounter;

        private int listenPort;
        private Socket listenerSocket;
        private Socket clientSocket;
        private string envId;
        private bool isTraining;

        private byte[] msgLenBuf;
        byte[] msgBuf;

        Button[] buttons = new Button[0x100];

        public MarNeo()
        {
            initFrameCounter = 0;
            initStateIndex = 0;
            initStateReached = false;
            initialized = false;
            frameCounter = ACTION_INTERVAL;
            msgLenBuf = new byte[4];
            msgBuf = new byte[1024];

            InitForm();
        }

        private void InitForm()
        {
            Size = new Size(1060, 1100);

            for (int i = 0; i < buttons.Length; i++)
            {
                int buttonSize = 64;

                float y = i / 16;
                buttons[i] = new Button();
                buttons[i].Size = new Size(buttonSize, buttonSize);
                buttons[i].Location = new Point((i % 16) * buttonSize, Math.Truncate(y).RoundToInt() * buttonSize);
                buttons[i].BackColor = System.Drawing.Color.White;
                buttons[i].Text = "0";
                buttons[i].Enabled = true;

                Controls.Add(buttons[i]);
            }
        }

        public override void Restart()
        {
        }

        private IList<(string, int)> GetInitInputSequence()
        {
            var gameInfo = APIs.GameInfo.GetGameInfo()!;
            switch (gameInfo.Hash)
            {
                case "33D23C2F2CFA4C9EFEC87F7BC1321CE3CE6C89BD": // Super Mario Bros       
                    return new List<(string, int)>
                    {
                        ("P1 Start", 100)
                    };
                case "4671517D72D09799403F6C672CD2B395933E926E": // Legend of Zelda
                    return new List<(string, int)>
                    {
                        ("P1 Start", 100),
                        ("P1 Start", 100),
                        ("P1 A", 200),
                        ("P1 Start", 100),
                        ("P1 Select", 100),
                        ("P1 Select", 100),
                        ("P1 Select", 100),
                        ("P1 Start", 100),
                        ("P1 Start", 100)
                    };
                case "EF76CEBDDC57B7C96CFC95B55DCC712FD5934B2C": // Pac-Man
                    return new List<(string, int)>
                    {
                        ("P1 Start", 500)
                    };
                case "3026D28B63D94C921FE58364F8B0659D10B5A0AC": // Tetris
                    return new List<(string, int)>
                    {
                        ("P1 Start", 300),
                        ("P1 Start", 100),
                        ("P1 Start", 100),
                        ("P1 Start", 100)
                    };
                default:
                    return null;
            }
        }

        private string MakeScreenshot()
        {
            var screenshot = ((MainForm)MainForm).MakeScreenshotImage();
            string path = envId + ".png";
            screenshot.ToSysdrawingBitmap().Save(path);
            return Path.GetFullPath(path);
        }

        protected override void UpdateAfter()
        {
            var config = ((MainForm)MainForm).Config;
            if (config != null)
            {
                config.AutosaveSaveRAM = false;
                config.BackupSaveram = false;
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

            if (!initStateReached)
            {
                var seq = GetInitInputSequence();
                if (seq == null)
                {
                    initStateReached = true;
                } else
                {
                    if (initFrameCounter < seq[initStateIndex].Item2)
                    {
                        ++initFrameCounter;
                        return;
                    } else
                    {
                        var pad = APIs.Joypad.Get();
                        Dictionary<string, bool> newPad = new Dictionary<string, bool>();
                        foreach (var key in pad.Keys)
                        {
                            newPad[key] = false;
                        }
                        newPad[seq[initStateIndex].Item1] = true;
                        APIs.Joypad.Set(newPad);
                        initFrameCounter = 0;
                        ++initStateIndex;
                        if (initStateIndex >= seq.Count)
                        {
                            initStateReached = true;
                        } else
                        {
                            return;
                        }
                    }
                }
            }

            --frameCounter;

            bool firstTime = false;
            if (!initialized)
            {
                envId = Environment.GetEnvironmentVariable("MARNEO_ID");
                string envAddr = Environment.GetEnvironmentVariable("MARNEO_ADDR");
                string envPort = Environment.GetEnvironmentVariable("MARNEO_PORT");
                string envTraining = Environment.GetEnvironmentVariable("MARNEO_IS_TRAINING");
                if (envId == null || envAddr == null || envPort == null || envTraining == null)
                {
                    throw new Exception("missing required env vars");
                }
                if (!int.TryParse(envPort, out listenPort))
                {
                    throw new Exception("Invalid MARNEO_PORT");
                }

                isTraining = bool.Parse(envTraining);

                if (isTraining)
                {
                    APIs.EmuClient.SpeedMode(1000); // speed up emulation when training
                } else
                {
                    APIs.EmuClient.SpeedMode(100);
                }

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
            if (firstTime || frameCounter == 0)
            {
                if (!firstTime)
                { 
                    // evaluate the last action performed
                    float reward;
                    bool done;
                    EvaluateAction(out reward, out done);

                    string screenshotPath = MakeScreenshot();

                    if (done)
                    {
                        SendMessage(new
                        {
                            reward = reward,
                            screenshotPath = screenshotPath,
                            done = true
                        });
                    } else
                    {
                        obs = CollectObservations();
                        SendMessage(new
                        {
                            observation = obs,
                            reward = reward,
                            screenshotPath = screenshotPath,
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

            UpdateVisual(actionId);
        }

        private void UpdateVisual(int actionId)
        {
            buttons[actionId - 1].BackColor = System.Drawing.Color.Black;

            int buttonvalue = Int32.Parse(buttons[actionId - 1].Text) + 1;
            buttons[actionId - 1].Text = buttonvalue.ToString();

            for (int i = 0; i < buttons.Length; i++)
            {
                int aValue = Int32.Parse(buttons[i].Text) * 5;

                if (aValue > 255)
                    aValue = 255;

                buttons[i].BackColor = System.Drawing.Color.FromArgb(aValue, 0, 0, 0);
            }

            buttons[actionId - 1].BackColor = System.Drawing.Color.Red;
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
