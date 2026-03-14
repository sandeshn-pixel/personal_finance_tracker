using FluentValidation;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Categories.DTOs;
using FinanceTracker.Application.Categories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(
    ICategoryService categoryService,
    ICurrentUserService currentUserService,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        var items = await categoryService.ListAsync(GetUserId(), includeArchived, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var item = await categoryService.CreateAsync(GetUserId(), request, cancellationToken);
        return Ok(item);
    }

    [HttpPut("{categoryId:guid}")]
    public async Task<IActionResult> Update(Guid categoryId, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var item = await categoryService.UpdateAsync(GetUserId(), categoryId, request, cancellationToken);
        return Ok(item);
    }

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Archive(Guid categoryId, CancellationToken cancellationToken)
    {
        await categoryService.ArchiveAsync(GetUserId(), categoryId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

    private IActionResult BuildValidationProblem(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
