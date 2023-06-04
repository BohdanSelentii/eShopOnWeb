using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(int basketId, Address shippingAddress);

    Task SendOrderRequestAsync(Order order);

    Task SendDeliveryOrderAsync(Order order);
}
