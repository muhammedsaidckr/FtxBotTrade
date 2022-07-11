// See https://aka.ms/new-console-template for more information
using CryptoExchange.Net.Authentication;
using FTX.Net.Clients;
using FTX.Net.Objects;
using FTX.Net.Objects.Models;
using FtxBotTrade;
using LiteDB;
using Microsoft.Extensions.Logging;

var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
var ftxClient = new FTXClient(new FTXClientOptions()
{
    ApiCredentials = new ApiCredentials(Settings.baseApi, Settings.baseSecret),
    LogLevel = LogLevel.Trace,
    RequestTimeout = TimeSpan.FromSeconds(60)
});



var ftxTrader = new FTXClient(new FTXClientOptions()
{
    ApiCredentials = new ApiCredentials(Settings.mirrorApi, Settings.mirrorSecret),
    LogLevel = LogLevel.Trace,
    RequestTimeout = TimeSpan.FromSeconds(60)
});

static FTXTrader StoreOrder(FTXOrder data, FTXOrder order)
{
    var ftxTraderOrder = new FTXTrader();
    ftxTraderOrder.Id = data.Id;
    ftxTraderOrder.BaseId = order.Id;
    ftxTraderOrder.ClientOrderId = order.ClientOrderId;
    ftxTraderOrder.Price = data.Price;
    ftxTraderOrder.Quantity = data.Quantity;
    ftxTraderOrder.Status = data.Status;

    return ftxTraderOrder;
}
static FTXTraderTriggerOrder StoreTriggerOrder(FTXTriggerOrder data, FTXTriggerOrder order)
{
    var ftxTraderOrder = new FTXTraderTriggerOrder();
    ftxTraderOrder.Id = data.Id;
    ftxTraderOrder.BaseId = order.Id;
    ftxTraderOrder.Price = data.Price;
    ftxTraderOrder.Quantity = data.Quantity;
    ftxTraderOrder.Status = data.Status;

    return ftxTraderOrder;
}



while (await timer.WaitForNextTickAsync())
{
    using (var db = new LiteDatabase(@"C:\Temp\FtxLive.db"))
    {
        var col = db.GetCollection<FTXOrder>("orders");
        var traderCol = db.GetCollection<FTXTrader>("traderOrders");


        var orders = await ftxClient.TradeApi.Trading.GetOrdersAsync(subaccountName: "FTXtest");


        if (orders.Success)
        {
            foreach (var order in orders.Data.OrderBy(o => o.CreateTime).ToList())
            {
                Console.WriteLine($"Order Info: {order.Status} <-> {order.CreateTime} <-> {order.Future} <-> {order.Quantity} <-> {order.QuantityFilled}");
                var orderRecord = col.Query().Where(o => o.Id == order.Id).FirstOrDefault();


                if (orderRecord != null)
                {
                    if (order.QuantityFilled == 0 && order.Status == FTX.Net.Enums.OrderStatus.Closed)
                    {
                        var rec = traderCol.Query().Where(t => t.BaseId == order.Id).FirstOrDefault();
                        if (rec != null)
                        {
                            await ftxTrader.TradeApi.Trading.CancelOrderAsync(rec.Id);
                            orderRecord!.QuantityFilled = order.QuantityFilled;
                            rec.Status = order.Status;
                            rec.QuantityFilled = rec.QuantityFilled;
                            col.Update(orderRecord);
                            traderCol.Update(rec);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{DateTime.UtcNow} <-> {order.CreateTime}");
                    if (order.Status != FTX.Net.Enums.OrderStatus.Closed)
                    {
                        Console.WriteLine($"Islem: {order.Symbol} - {order.Side} - {order.Type} - {Math.Round(order.Quantity * 0.4M)}");
                        col.Insert(order);
                        if (order.Type == FTX.Net.Enums.OrderType.Market)
                        {
                            var newOrder = await ftxTrader.TradeApi.Trading.PlaceOrderAsync(
                                    order.Symbol,
                                    order.Side,
                                    order.Type,
                                    order.Quantity * 0.4M
                                );
                            if (newOrder.Success)
                            {
                                var traderRecord = StoreOrder(newOrder.Data, order);
                                traderCol.Insert(traderRecord);
                            }

                        }
                        else if (order.Type == FTX.Net.Enums.OrderType.Limit)
                        {
                            var newOrder = await ftxTrader.TradeApi.Trading.PlaceOrderAsync(
                                    order.Symbol,
                                    order.Side,
                                    order.Type,
                                    order.Quantity * 0.4M,
                                    order.Price
                                );
                            if (newOrder.Success)
                            {
                                var traderRecord = StoreOrder(newOrder.Data, order);
                                traderCol.Insert(traderRecord);
                            }
                        }
                    }

                }
            }
        }

        var colTrigger = db.GetCollection<FTXTriggerOrder>("triggerOrders");
        var traderColTrigger = db.GetCollection<FTXTraderTriggerOrder>("traderTriggerOrders");

        var triggerOrders = await ftxClient.TradeApi.Trading.GetTriggerOrdersAsync(subaccountName: "FTXtest");
        
        if (triggerOrders.Success)
        {
            foreach (var triggerOrder in triggerOrders.Data.OrderBy(o => o.CreateTime).ToList())
            {
                Console.WriteLine($"Order Info: {triggerOrder.Status} <-> {triggerOrder.CreateTime} <-> {triggerOrder.Future} <-> {triggerOrder.Quantity} <-> {triggerOrder.QuantityFilled}");
                var orderRecord = colTrigger.Query().Where(o => o.Id == triggerOrder.Id).FirstOrDefault();



                if (orderRecord != null)
                {
                    if (triggerOrder.Status == FTX.Net.Enums.TriggerOrderStatus.Canceled && orderRecord.Status != FTX.Net.Enums.TriggerOrderStatus.Canceled)
                    {
                        var rec = traderColTrigger.Query().Where(t => t.BaseId == triggerOrder.Id).FirstOrDefault();
                        if (rec != null)
                        {
                            await ftxTrader.TradeApi.Trading.CancelTriggerOrderAsync(rec.Id);
                            orderRecord.Status = triggerOrder.Status;
                            rec.Status = triggerOrder.Status;
                            rec.QuantityFilled = triggerOrder.QuantityFilled;
                            colTrigger.Update(orderRecord);
                            traderColTrigger.Update(rec);
                        }
                    }
                }
                else {
                    if (triggerOrder!.Status != FTX.Net.Enums.TriggerOrderStatus.Canceled)
                    {
                        Console.WriteLine($"Trigger Islem: {triggerOrder.Symbol} - {triggerOrder.Side} - {triggerOrder.Type} - {Math.Round(triggerOrder.Quantity * 0.4M)}");
                        colTrigger.Insert(triggerOrder);
                        if (triggerOrder.Type == FTX.Net.Enums.OrderType.Market)
                        {
                            var newOrder = await ftxTrader.TradeApi.Trading.PlaceTriggerOrderAsync(
                                    triggerOrder.Symbol,
                                    triggerOrder.Side,
                                    triggerOrder.TriggerType,
                                    triggerOrder.Quantity * 0.4M
                                );
                            if (newOrder.Success)
                            {
                                var traderRecord = StoreTriggerOrder(newOrder.Data, triggerOrder);
                                traderColTrigger.Insert(traderRecord);
                            }
                        }
                        else if (triggerOrder.Type == FTX.Net.Enums.OrderType.Limit)
                        {
                            if (triggerOrder.Price != null)
                            {
                                var newOrder = await ftxTrader.TradeApi.Trading.PlaceTriggerOrderAsync(
                                        triggerOrder.Symbol,
                                        triggerOrder.Side,
                                        triggerOrder.TriggerType,
                                        triggerOrder.Quantity * 0.4M,
                                        triggerPrice: triggerOrder.TriggerPrice,
                                        orderPrice: triggerOrder.Price
                                    );
                                if (newOrder.Success)
                                {
                                    var traderRecord = StoreTriggerOrder(newOrder.Data, triggerOrder);
                                    traderColTrigger.Insert(traderRecord);
                                }
                            }
                            else
                            {
                                var newOrder = await ftxTrader.TradeApi.Trading.PlaceTriggerOrderAsync(
                                        triggerOrder.Symbol,
                                        triggerOrder.Side,
                                        triggerOrder.TriggerType,
                                        triggerOrder.Quantity * 0.4M,
                                        triggerPrice: triggerOrder.TriggerPrice
                                    );
                                if (newOrder.Success)
                                {
                                    var traderRecord = StoreTriggerOrder(newOrder.Data, triggerOrder);
                                    traderColTrigger.Insert(traderRecord);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}