using NArchitecture.Core.Mediator;
using Okul.Modules.Parents.Domain;

namespace Okul.Modules.Parents.Application;

public sealed record VerifyParentSmsCommand(Guid TenantId, string PhoneNumber, string SmsCode)
    : IRequest<VerifyParentSmsResult>;

public sealed record VerifyParentSmsResult(bool Success, string? ErrorMessage = null);

public sealed class ParentLoginServices
{
    private readonly IParentAccountStore _accountStore;
    private readonly ISmsCodeStore _smsCodeStore;
    private readonly TimeProvider _timeProvider;
    private const int MaxSmsAttempts = 3;
    private const int LockoutDurationMinutes = 15;

    public ParentLoginServices(
        IParentAccountStore accountStore,
        ISmsCodeStore smsCodeStore,
        TimeProvider timeProvider)
    {
        _accountStore = accountStore;
        _smsCodeStore = smsCodeStore;
        _timeProvider = timeProvider;
    }

    public async Task<VerifyParentSmsResult> VerifyCodeAsync(
        Guid tenantId, string phoneNumber, string smsCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(smsCode);

        var now = _timeProvider.GetUtcNow();

        // Find or create parent account
        var parent = await _accountStore.FindByPhoneAsync(tenantId, phoneNumber, cancellationToken);
        if (parent is null)
        {
            parent = Parent.Create(tenantId, phoneNumber);
            await _accountStore.AddAsync(parent, cancellationToken);
            parent = await _accountStore.FindByPhoneAsync(tenantId, phoneNumber, cancellationToken)
                ?? throw new InvalidOperationException("Parent not found after creation");
        }

        // Check lockout
        if (parent.IsLockedOut(now, TimeSpan.FromMinutes(LockoutDurationMinutes)))
        {
            return new VerifyParentSmsResult(Success: false, ErrorMessage: "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.");
        }

        // Get latest SMS code
        var latestCode = await _smsCodeStore.FindLatestByParentAsync(parent.Id, cancellationToken);
        if (latestCode is null || latestCode.IsExpired(now))
        {
            parent.RecordFailure(now);
            await _accountStore.SaveChangesAsync(cancellationToken);
            return new VerifyParentSmsResult(Success: false, ErrorMessage: "SMS kodunuz süresi geçmiş. Yeni kod gönderin.");
        }

        // Verify code
        if (!latestCode.IsValid(smsCode, now))
        {
            parent.RecordFailure(now);
            await _accountStore.SaveChangesAsync(cancellationToken);

            if (parent.IsLockedOut(now, TimeSpan.FromMinutes(LockoutDurationMinutes)))
            {
                return new VerifyParentSmsResult(Success: false, ErrorMessage: "Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.");
            }

            return new VerifyParentSmsResult(Success: false, ErrorMessage: "SMS kodu hatalı veya süresi geçmiş.");
        }

        // Success: reset failures
        parent.ResetFailures();
        await _accountStore.SaveChangesAsync(cancellationToken);

        return new VerifyParentSmsResult(Success: true);
    }
}

public sealed class VerifyParentSmsCommandHandler : IRequestHandler<VerifyParentSmsCommand, VerifyParentSmsResult>
{
    private readonly ParentLoginServices _services;

    public VerifyParentSmsCommandHandler(ParentLoginServices services) => _services = services;

    public Task<VerifyParentSmsResult> Handle(VerifyParentSmsCommand request, CancellationToken cancellationToken) =>
        _services.VerifyCodeAsync(request.TenantId, request.PhoneNumber, request.SmsCode, cancellationToken);
}
