using MiniBank.Api.DTOs.Customers;
using MiniBank.Api.Services;

namespace MiniBank.Api.Endpoints;

public static class CustomersEndpoints
{
    public static void MapCustomersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/customers").WithTags("Customers");

        group
            .MapGet(
                "/",
                async (CustomerService customerService) =>
                    await customerService.GetAllCustomersAsync()
            )
            .Produces<List<CustomerResponse>>(StatusCodes.Status200OK)
            .WithName("GetAllCustomers");

        group
            .MapPost(
                "/",
                async (CreateCustomerRequest request, CustomerService customerService) =>
                {
                    if (
                        (await customerService.GetAllCustomersAsync()).Any(c =>
                            c.Email == request.Email
                        )
                    )
                    {
                        return Results.Conflict(
                            new { Message = "A customer with the same email already exists." }
                        );
                    }

                    var newCustomer = await customerService.CreateCustomerAsync(request);

                    return Results.CreatedAtRoute(
                        "GetAllCustomers",
                        new { id = newCustomer.Id },
                        newCustomer
                    );
                }
            )
            .Accepts<CreateCustomerRequest>("application/json")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateCustomer");
    }
}
