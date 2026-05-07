using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wms.Application.Abstractions.Messaging;
using Wms.Application.Common.Interfaces;
using Wms.Shared.Common;

namespace Wms.Application.Features.Products.Commands;

public sealed record UpdateProductCommand(Guid Id, string Name, string Description) : ICommand;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Product ID is required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
    }
}

public sealed class UpdateProductCommandHandler(IAppDbContext context)
    : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (product is null)
            return Error.NotFound;
        
        product.Name = request.Name;
        product.Description = request.Description;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
