// See https://aka.ms/new-console-template for more information
using CryptoExchange.Net.Authentication;
using FTX.Net.Clients;
using FTX.Net.Objects;
using FTX.Net.Objects.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

while (await timer.WaitForNextTickAsync())
{
    using (var db = new LiteDatabase(@"C:\Temp\FtxLive.db"))
    {
        var col = db.GetCollection<FTXOrder>("orders");

        var ftxClient = new FTXClient(new FTXClientOptions()
        {
            ApiCredentials = new ApiCredentials("baseApi", "baseSecret"),
            LogLevel = LogLevel.Trace,
            RequestTimeout = TimeSpan.FromSeconds(60)
        });



        var ftxTrader = new FTXClient(new FTXClientOptions()
        {
            ApiCredentials = new ApiCredentials("mirrorApi", "mirrorSecret"),
            LogLevel = LogLevel.Trace,
            RequestTimeout = TimeSpan.FromSeconds(60)
        });



        var orders = await ftxClient.TradeApi.Trading.GetOrdersAsync(subaccountName: "FTXtest");


        if (orders.Success)
        {
            foreach (var order in orders.Data.OrderBy(o => o.CreateTime).ToList())
            {
                var orderRecord = col.Query().Where(o => o.Id == order.Id).FirstOrDefault();

                if (order.ImmediateOrCancel && !orderRecord.ImmediateOrCancel)
                {
                    await ftxTrader.TradeApi.Trading.CancelOrderAsync(order.Id);
                    orderRecord.ImmediateOrCancel = order.ImmediateOrCancel;
                    col.Update(orderRecord);
                }

                if (!(orderRecord != null))
                {
                    if (!order.ImmediateOrCancel)
                    {
                        Console.WriteLine($"Islem: {order.Symbol} - {order.Side} - {order.Type} - {Math.Round(order.Quantity * 0.4M)}");
                        col.Insert(order);
                        if (order.Type == FTX.Net.Enums.OrderType.Market)
                        {
                            await ftxTrader.TradeApi.Trading.PlaceOrderAsync(
                                    order.Symbol,
                                    order.Side,
                                    order.Type,
                                    order.Quantity * 0.4M
                                );
                        }
                        else if (order.Type == FTX.Net.Enums.OrderType.Limit)
                        {
                            await ftxTrader.TradeApi.Trading.PlaceOrderAsync(
                                    order.Symbol,
                                    order.Side,
                                    order.Type,
                                    order.Quantity * 0.4M,
                                    order.Price
                                );
                        }
                    }
                }
            }
        }

        var colTrigger = db.GetCollection<FTXTriggerOrder>("triggerOrders");
        var triggerOrders = await ftxClient.TradeApi.Trading.GetTriggerOrdersAsync(subaccountName: "FTXtest");

        if (triggerOrders.Success)
        {
            foreach (var triggerOrder in triggerOrders.Data.OrderBy(o => o.CreateTime).ToList())
            {
                var orderRecord = colTrigger.Query().Where(o => o.Id == triggerOrder.Id).FirstOrDefault();

                if (triggerOrder.Status == FTX.Net.Enums.TriggerOrderStatus.Canceled && orderRecord.Status != FTX.Net.Enums.TriggerOrderStatus.Canceled)
                {
                    await ftxTrader.TradeApi.Trading.CancelTriggerOrderAsync(orderRecord.Id);
                    orderRecord.Status = triggerOrder.Status;
                    colTrigger.Update(orderRecord);
                }

                if (!(orderRecord != null))
                {
                    if (triggerOrder!.Status != FTX.Net.Enums.TriggerOrderStatus.Canceled)
                    {
                        Console.WriteLine($"Trigger Islem: {triggerOrder.Symbol} - {triggerOrder.Side} - {triggerOrder.Type} - {Math.Round(triggerOrder.Quantity * 0.4M)}");
                        colTrigger.Insert(triggerOrder);
                        if (triggerOrder.Type == FTX.Net.Enums.OrderType.Market)
                        {
                            await ftxTrader.TradeApi.Trading.PlaceTriggerOrderAsync(
                                    triggerOrder.Symbol,
                                    triggerOrder.Side,
                                    triggerOrder.TriggerType,
                                    triggerOrder.Quantity * 0.4M
                                );
                        }
                        else if (triggerOrder.Type == FTX.Net.Enums.OrderType.Limit)
                        {
                            await ftxTrader.TradeApi.Trading.PlaceTriggerOrderAsync(
                                    triggerOrder.Symbol,
                                    triggerOrder.Side,
                                    triggerOrder.TriggerType,
                                    triggerOrder.Quantity * 0.4M,
                                    triggerPrice: triggerOrder.TriggerPrice
                                );
                        }
                    }
                }
            }
        }
    }
}