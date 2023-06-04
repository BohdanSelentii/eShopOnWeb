using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly ServerlessFunctionsSettings _functionsSettings;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IOptions<ServerlessFunctionsSettings> functionsSettingsOptions,
        ServiceBusClient serviceBusClient)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _functionsSettings = functionsSettingsOptions.Value;
        _serviceBusClient = serviceBusClient;
    }

    public async Task<Order> CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        Guard.Against.Null(basket, nameof(basket));
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);

        return order;
    }

    public async Task SendOrderRequestAsync(Order order)
    {
        var orderJson = new
        {
            order.BuyerId,
            order.OrderDate,
            order.ShipToAddress,
            order.OrderItems,
            Total = order.Total(),
        }.ToJson();

        using var jsonContent = new StringContent(orderJson);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-functions-key", _functionsSettings.DeliveryOrderProcessorKey);

        var response = await client.PostAsync(_functionsSettings.DeliveryOrderProcessorUrl, jsonContent);

        response.EnsureSuccessStatusCode();
    }

    public async Task SendDeliveryOrderAsync(Order order)
    {
        const string queueName = "order-requests";
        await using var sender = _serviceBusClient.CreateSender(queueName);

        var orderJson = new
        {
            order.ShipToAddress,
            order.OrderItems,
            FinalPrice = order.Total()
        }.ToJson();

        var message = new ServiceBusMessage(orderJson);
        await sender.SendMessageAsync(message);
    }
}
