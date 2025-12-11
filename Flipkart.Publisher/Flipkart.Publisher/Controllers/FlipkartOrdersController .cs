using Microsoft.AspNetCore.Mvc;
namespace Flipkart.Publisher.Controllers
{
   

    [ApiController]
    [Route("api/[controller]")]
    public class FlipkartOrdersController : ControllerBase
    {
        private readonly IFlipkartOrderMessagePublisher _publisher;

        public FlipkartOrdersController(IFlipkartOrderMessagePublisher publisher)
        {
            _publisher = publisher;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            // Here, in a real Flipkart system, you’d verify user auth, stock, etc.

            var message = new OrderCreatedMessage(
                OrderId: Guid.NewGuid().ToString(),
                Amount: request.Amount,
                CreatedAt: DateTime.UtcNow);

            await _publisher.PublishOrderAsync(message);

            return Accepted(new
            {
                message.OrderId,
                Status = "OrderQueued"
            });
        }
    }

}
