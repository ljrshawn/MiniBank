namespace MiniBank.Api.Infrastructure.Http;

// Keep public error messages and HTTP status codes in one place.
internal static class ApiProblems
{
    internal const string ValidationTitle = "One or more validation errors occurred.";

    internal static readonly ApiProblem CustomerNotFound = new(
        StatusCodes.Status404NotFound,
        "Customer not found",
        "No customer exists with the supplied ID."
    );

    internal static readonly ApiProblem DuplicateEmail = new(
        StatusCodes.Status409Conflict,
        "Email already registered",
        "A customer with the same email already exists."
    );

    internal static readonly ApiProblem CustomerHasAccounts = new(
        StatusCodes.Status409Conflict,
        "Customer has accounts",
        "A customer with accounts cannot be deleted. Update their status instead."
    );

    internal static readonly ApiProblem AccountNumberUnavailable = new(
        StatusCodes.Status503ServiceUnavailable,
        "Account number unavailable",
        "A unique account number could not be allocated. Please try again."
    );

    internal static readonly ApiProblem InvalidTransaction = new(
        StatusCodes.Status400BadRequest,
        "Invalid transaction"
    );

    internal static readonly ApiProblem WithdrawalInsufficientFunds = new(
        StatusCodes.Status400BadRequest,
        "Insufficient funds",
        "The account balance is too low for this withdrawal."
    );

    internal static readonly ApiProblem DepositBalanceLimitExceeded = new(
        StatusCodes.Status400BadRequest,
        "Balance limit exceeded",
        "The deposit would exceed the maximum supported account balance."
    );

    internal static readonly ApiProblem TransactionConflict = new(
        StatusCodes.Status409Conflict,
        "Account changed",
        "The account changed while processing this transaction. Please try again."
    );

    internal static readonly ApiProblem InvalidTransfer = new(
        StatusCodes.Status400BadRequest,
        "Invalid transfer"
    );

    internal static readonly ApiProblem TransferInsufficientFunds = new(
        StatusCodes.Status400BadRequest,
        "Insufficient funds",
        "The source account balance is too low for this transfer."
    );

    internal static readonly ApiProblem TransferBalanceLimitExceeded = new(
        StatusCodes.Status400BadRequest,
        "Balance limit exceeded",
        "The transfer would exceed the maximum supported destination account balance."
    );

    internal static readonly ApiProblem TransferConflict = new(
        StatusCodes.Status409Conflict,
        "Account changed",
        "An account changed while processing this transfer. Please try again."
    );

    internal static readonly ApiProblem InternalServerError = new(
        StatusCodes.Status500InternalServerError,
        "An error occurred while processing your request."
    );

    internal static ApiProblem AccountNotFound(Guid? id = null) =>
        new(
            StatusCodes.Status404NotFound,
            "Account not found",
            id.HasValue ? $"No account found with ID {id}." : "No account exists with the supplied ID."
        );

    internal static ApiProblem TransferNotFound(Guid? id = null) =>
        new(
            StatusCodes.Status404NotFound,
            "Transfer not found",
            id.HasValue ? $"No transfer found with ID {id}." : "No transfer exists with the supplied ID."
        );

    // Framework binding/routing errors keep their original status and standard title.
    internal static ApiProblem ForStatusCode(int statusCode) =>
        statusCode == StatusCodes.Status500InternalServerError
            ? InternalServerError
            : new ApiProblem(statusCode);
}
