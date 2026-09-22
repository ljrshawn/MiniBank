using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using MiniBank.Api.Infrastructure.Http;

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
            .WithSummary("Get a customer by ID, including their accounts.")
            .ProducesProblems(ApiProblems.CustomerNotFound);

        group
            .MapPost("/", CreateCustomerAsync)
            .WithName("CreateCustomer")
            .WithSummary("Create a customer.")
            .ProducesValidationProblem()
            .ProducesProblems(ApiProblems.DuplicateEmail);

        group
            .MapPut("/{id:guid}", UpdateCustomerAsync)
            .WithName("UpdateCustomer")
            .WithSummary("Replace a customer's profile and status.")
            .ProducesValidationProblem()
            .ProducesProblems(ApiProblems.CustomerNotFound, ApiProblems.DuplicateEmail);

        group
            .MapDelete("/{id:guid}", DeleteCustomerAsync)
            .WithName("DeleteCustomer")
            .WithSummary("Delete a customer who has no accounts.")
            .ProducesProblems(ApiProblems.CustomerNotFound, ApiProblems.CustomerHasAccounts);

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
        return customer.ToHttpResult(ApiProblems.CustomerNotFound, value => TypedResults.Ok(value));
    }

    private static async Task<
        Results<CreatedAtRoute<CustomerResponse>, ValidationProblem, ProblemHttpResult>
    > CreateCustomerAsync(
        CreateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.CreateCustomerAsync(request, cancellationToken);
        return result.ToHttpResult(customer =>
            TypedResults.CreatedAtRoute(customer, GetCustomerRouteName, new { id = customer.Id })
        );
    }

    private static async Task<Results<Ok<CustomerResponse>, ValidationProblem, ProblemHttpResult>> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken
    )
    {
        var result = await service.UpdateCustomerAsync(id, request, cancellationToken);
        return result.ToHttpResult(customer => TypedResults.Ok(customer));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCustomerAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken
    ) => (await service.DeleteCustomerAsync(id, cancellationToken)).ToHttpResult();
}
