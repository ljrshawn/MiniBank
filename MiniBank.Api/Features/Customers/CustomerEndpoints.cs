using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MiniBank.Api.Features.Customers;

public static class CustomerEndpoints
{
    private const string GetCustomerRouteName = "GetCustomerById";

    public static RouteGroupBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/customers").WithTags("Customers");

        group
            .MapGet("/", GetCustomersAsync)
            .WithName("GetCustomers")
            .WithSummary("List customers in a stable order, with pagination.")
            .ProducesValidationProblem();

        group
            .MapGet("/{id:guid}", GetCustomerByIdAsync)
            .WithName(GetCustomerRouteName)
            .WithSummary("Get a customer by ID.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group
            .MapPost("/", CreateCustomerAsync)
            .WithName("CreateCustomer")
            .WithSummary("Create a customer.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group
            .MapPut("/{id:guid}", UpdateCustomerAsync)
            .WithName("UpdateCustomer")
            .WithSummary("Replace a customer's profile and status.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group
            .MapDelete("/{id:guid}", DeleteCustomerAsync)
            .WithName("DeleteCustomer")
            .WithSummary("Delete a customer who has no accounts.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Ok<List<CustomerResponse>>> GetCustomersAsync(
        CustomerService service,
        CancellationToken cancellationToken,
        [Range(1, 1_000_000)] int page = 1,
        [Range(1, 100)] int pageSize = 50
    ) => TypedResults.Ok(await service.GetCustomersAsync(page, pageSize, cancellationToken));

    private static async Task<
        Results<Ok<CustomerResponse>, ProblemHttpResult>
    > GetCustomerByIdAsync(Guid id, CustomerService service, CancellationToken cancellationToken)
    {
        var customer = await service.GetCustomerByIdAsync(id, cancellationToken);
        return customer is null ? CustomerNotFound() : TypedResults.Ok(customer);
    }

    private static async Task<
        Results<CreatedAtRoute<CustomerResponse>, ProblemHttpResult>
    > CreateCustomerAsync(
        CreateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateCustomerAsync(request, cancellationToken);
        if (result.Status == CustomerWriteStatus.DuplicateEmail)
        {
            return DuplicateEmail();
        }

        var customer = result.Customer!;
        return TypedResults.CreatedAtRoute(
            customer,
            GetCustomerRouteName,
            new { id = customer.Id }
        );
    }

    private static async Task<Results<Ok<CustomerResponse>, ProblemHttpResult>> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.UpdateCustomerAsync(id, request, cancellationToken);
        return result.Status switch
        {
            CustomerWriteStatus.NotFound => CustomerNotFound(),
            CustomerWriteStatus.DuplicateEmail => DuplicateEmail(),
            _ => TypedResults.Ok(result.Customer!),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCustomerAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken
    ) =>
        await service.DeleteCustomerAsync(id, cancellationToken) switch
        {
            CustomerDeleteResult.NotFound => CustomerNotFound(),
            CustomerDeleteResult.HasAccounts => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Customer has accounts",
                detail: "A customer with accounts cannot be deleted. Update their status instead."
            ),
            _ => TypedResults.NoContent(),
        };

    private static ProblemHttpResult CustomerNotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Customer not found",
            detail: "No customer exists with the supplied ID."
        );

    private static ProblemHttpResult DuplicateEmail() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Email already registered",
            detail: "A customer with the same email already exists."
        );
}
