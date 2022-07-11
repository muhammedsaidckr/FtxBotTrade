using FTX.Net.Enums;
using FTX.Net.Objects.Models;
using Newtonsoft.Json;
using FTX.Net.Converters;

namespace FtxBotTrade
{
    /// <summary>
    /// Order info
    /// </summary>
    internal class FTXTrader : FTXOrder
    {
        public long BaseId { get; set; }
    }

    internal class FTXTraderTriggerOrder : FTXTriggerOrder
    {
        public long BaseId { get; set; }    
    }
}
