using System;
using System.Diagnostics;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;

namespace MarNEO
{
    [ExternalTool("MarNeo")]
    public class MarNeo : ToolFormBase, IExternalToolForm
    {
        public ApiContainer? _maybeAPIContainer { get; set; }
        private ApiContainer APIs => _maybeAPIContainer!;

        protected override string WindowTitleStatic => "MarNeo";

        private Random rng;
        private DateTime lastActionTime;

        public override void Restart()
        {
            lastActionTime = DateTime.Now;
            rng = new Random();
            Hide();
        }

        protected override void UpdateAfter()
        {
            var gameInfo = APIs.GameInfo.GetGameInfo();
            if (gameInfo.System != "NULL" && !APIs.EmuClient.IsPaused())
            {
                if ((DateTime.Now - lastActionTime).TotalMilliseconds >= 30)
                {
                    int minAddr = 0x1;
                    int maxAddr = 0xC0;
                    int addrMod = rng.Next(minAddr, maxAddr + 1);
                    uint nextVal = (APIs.Memory.ReadByte(addrMod) + 1) % 256;
                    APIs.Memory.WriteByte(addrMod, nextVal);
                    lastActionTime = DateTime.Now;
                }
            }
        }
    }
}
