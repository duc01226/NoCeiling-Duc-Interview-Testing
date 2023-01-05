using System.Diagnostics.CodeAnalysis;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.Exceptions.Extensions;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Common.Timing;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.EntityDtos;
using PlatformExampleApp.TextSnippet.Application.Infrastructures;
using PlatformExampleApp.TextSnippet.Domain.Entities;
using PlatformExampleApp.TextSnippet.Domain.Repositories;

// ReSharper disable ConvertToLocalFunction

namespace PlatformExampleApp.TextSnippet.Application.UseCaseCommands;

public class SaveSnippetTextCommand : PlatformCqrsCommand<SaveSnippetTextCommandResult>
{
    public TextSnippetEntityDto Data { get; set; }

    public List<TextSnippetEntityDto> DemoWorkWithListOfValidations { get; set; } = new();

    public PlatformCqrsEntityEventCrudAction StatusToDemoWhenValueCases { get; set; }

    public override PlatformValidationResult<IPlatformCqrsRequest> Validate()
    {
        return this
            .Validate(p => Data != null, "Data must be not null.")
            .And(p => Data.MapToEntity().Validate())
            .And(p => p.JustDemoUsingValidateNot())
            .Of<IPlatformCqrsRequest>();
    }

    public PlatformValidationResult<IPlatformCqrsRequest> JustDemoUsingValidateNot()
    {
        return this
            .ValidateNot(p => Data == null, "Data must be not null.")
            .AndNot(p => Data.MapToEntity().Validate().IsValid == false, Data.MapToEntity().Validate().Errors.FirstOrDefault())
            .Of<IPlatformCqrsRequest>();
    }
}

public class SaveSnippetTextCommandResult : PlatformCqrsCommandResult
{
    public TextSnippetEntityDto SavedData { get; set; }
}

public class SaveSnippetTextCommandHandler : PlatformCqrsCommandApplicationHandler<SaveSnippetTextCommand, SaveSnippetTextCommandResult>
{
    private readonly ILogger<SaveSnippetTextCommandHandler> logger;

    private readonly ITextSnippetRootRepository<MultiDbDemoEntity> multiDbDemoEntityRepository;

    // This only for demo define and use infrastructure services
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly ISendMailService sendMailService;
    private readonly ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository;

    public SaveSnippetTextCommandHandler(
        IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        ITextSnippetRootRepository<TextSnippetEntity> textSnippetEntityRepository,
        ITextSnippetRootRepository<MultiDbDemoEntity> multiDbDemoEntityRepository,
        ISendMailService sendMailService,
        ILogger<SaveSnippetTextCommandHandler> logger) : base(userContext, unitOfWorkManager, cqrs)
    {
        this.textSnippetEntityRepository = textSnippetEntityRepository;
        this.multiDbDemoEntityRepository = multiDbDemoEntityRepository;
        this.sendMailService = sendMailService;
        this.logger = logger;

        this.sendMailService.SendEmail("demo@email.com", "demo header", "demo content");
    }

    [SuppressMessage("Style", "IDE0039:Use local function", Justification = "<Pending>")]
    protected override async Task<SaveSnippetTextCommandResult> HandleAsync(
        SaveSnippetTextCommand request,
        CancellationToken cancellationToken)
    {
        // THIS IS NOT RELATED to SaveSnippetText logic. This is just for demo multi db features in one application works
        await UpsertFirstExistedMultiDbDemoEntity(cancellationToken);

        // THIS IS NOT RELATED to SaveSnippetText logic. Demo using WhenCases to more declarative
        // prevent if/else if code smell
        // References: https://levelup.gitconnected.com/treat-if-else-as-a-code-smell-until-proven-otherwise-3bd2c4c577bf#:~:text=The%20If%2DElse%20statement%20is,and%20the%20need%20for%20refactoring.
        var demoWhenCasesMappedRequest = await request
            .When(_ => request.Data == null, _ => request.With(p => p.Data = new TextSnippetEntityDto()))
            .When(
                _ => request.DemoWorkWithListOfValidations?.Any() != true,
                _ => request.With(p => p.DemoWorkWithListOfValidations = Util.ListBuilder.New(new TextSnippetEntityDto())))
            .WhenIs<PlatformCqrsCommand>(then: _ => request) // Do nothing, just demo WhenIs for check value type is something
            .ExecuteAsync();
        var demoWhenValueCasesMappedString = request.StatusToDemoWhenValueCases
            .WhenValue(PlatformCqrsEntityEventCrudAction.Created, _ => "Created")
            .WhenValue(PlatformCqrsEntityEventCrudAction.Updated, _ => "Updated")
            .When(status => status == PlatformCqrsEntityEventCrudAction.Deleted, _ => "Deleted")
            .Execute(); // .When(status => status == XXX) <=> .WhenValue(XXX)

        // THIS IS NOT RELATED to SaveSnippetText logic. Demo some other common USE FULL EXTENSIONS
        var demoGetItemsByExecuteAsyncOnEachOtherItems = await Util.ListBuilder.New("UserId1", "UserId2")
            .SelectAsync(
                userId => new
                {
                    Id = userId,
                    Name = $"User {userId}"
                }.AsTask()); // return [{Id:"UserId1",Name:"User UserId1"},{Id:"UserId2",Name:"User UserId2"}]
        var demoGetItemsByExecuteAsyncOnEachOtherItemsWithItemIndex = await Util.ListBuilder.New("UserId1", "UserId2")
            .SelectAsync(
                (userId, itemIndex) => new
                {
                    Id = userId,
                    Name = $"User Index{itemIndex} {userId}"
                }.AsTask()); // return [{Id:"UserId1",Name:"User Index0 UserId1"},{Id:"UserId2",Name:"User Index1 UserId2"}]
        await Util.ListBuilder.New("UserId1", "UserId2")
            .ForEachAsync(
                (userId, itemIndex) => Task.Run(() => logger.LogInformation($"{userId} {itemIndex}"), cancellationToken)); // Demo ForEach call async function

        // Check that an obj could be a object as other type. Like xxx as TXX in c#. Return null if it could not be parsed.
        // This case return null so that EnsureFound will throw not found
        // var demoFluentAs = request.As<TextSnippetEntity>().EnsureFound();
        var demoFluentAsync = request.AsTask(); // equal to Task.FromResult(request)
        // This case return the command because it is IPlatformCqrsCommand. Also demo fluent async task
        // Do not need to use await keyword in parenthesis (await request.AsTask()).As<IPlatformCqrsCommand>().EnsureFound();
        //var demoFluentAsync1 = await request.AsTask()
        //    .Then(request => request.As<IPlatformCqrsCommand>())
        //    .Then(request => request.As<PlatformCqrsCommand<SaveSnippetTextCommandResult>>().AsTask()) // Then work like Promise.Then, support both sync and async func
        //    .EnsureFound();
        var demoSingleValueToListOrArray = request.AsInArray().Concat(request.AsInList()); // return [request].Concat(List<>{request}) => [request, request]

        // THIS IS NOT RELATED to SaveSnippetText logic. Test support suppress uow works
        using (var uow = UnitOfWorkManager.Begin())
        {
            await textSnippetEntityRepository.UpdateAsync(
                await textSnippetEntityRepository.FirstOrDefaultAsync(cancellationToken: cancellationToken),
                cancellationToken: cancellationToken);
            await uow.CompleteAsync(cancellationToken);
        }

        // STEP 1: Build saving entity data from request. Throw not found if update (when id is not null)
        var toSaveEntity = request.Data.Id.HasValue
            ? await textSnippetEntityRepository.GetByIdAsync(request.Data.Id.Value, cancellationToken)
                .EnsureFound($"Has not found text snippet for id {request.Data.Id}")
                .Then(existingEntity => request.Data.UpdateToEntity(existingEntity))
            : request.Data.MapToEntity();

        // STEP 2: Do validation and ensure that all logic is valid
        var validToSaveEntity = toSaveEntity
            .With(toSaveEntity => toSaveEntity.SnippetText = toSaveEntity.SnippetText) //Demo Update Data By With Support Chaining
            .ValidateSavePermission(userId: CurrentUser.UserId<Guid?>()) // Demo Permission Logic
            .And(entity => toSaveEntity.ValidateSomeSpecificIsXxxLogic()) // Demo domain business logic
            .And(entity => ValidateSomeThisCommandApplicationLogic(entity)) // Demo application business logic
            .EnsureValid(); // Throw PermissionException, or DomainException, or ApplicationException on invalid for each stage

        // ADDITIONAL DEMO STEP 2 - Bonus Demo Alternative Validation directly on object
        // (Not recommended because of easily violate SingleResponsibility or duplicate code)
        var validToSaveEntity1 = toSaveEntity
            .Validate(
                must: toSaveEntity =>
                    toSaveEntity.CreatedByUserId == null || CurrentUser.UserId<Guid?>() == null || toSaveEntity.CreatedByUserId == CurrentUser.UserId<Guid?>(),
                "User must be the creator to update text snippet entity")
            .WithPermissionException() // Equivalent to ValidateSavePermission
            .And(
                must: toSaveEntity => true,
                "Some example domain logic violated message.")
            .WithDomainValidationException() // Equivalent to ValidateSomeSpecificDomainLogic
            .And(must: p => true, "Example Rule 1 violated error message")
            .And(must: p => true, "Example Rule 2 violated error message")
            .WithApplicationException() // Equivalent to ValidateSomeThisCommandApplicationLogic
            .EnsureValid();

        // ADDITIONAL DEMO STEP 2 - Demo Validate Combine for a list of items.
        // I is equivalent to request.DemoCombineValidationsOfListItems.ForEach(item => if(item... not satisfy a condition1) throw Exception(error1) FAIL FAST);
        request.DemoWorkWithListOfValidations.Select(p => p.Validate()).Combine().EnsureValid(); // I

        // II is equivalent to
        // var listErrors;
        // request.DemoCombineValidationsOfListItems.ForEach(item => {
        //   if(item... not satisfy a condition1) listErrors.Add(error1); // COLLECT ERRORS
        //   if (item...not satisfy a condition2) listErrors.Add(error2);
        // });
        // if(listErrors.Any()) throw Exception(listErrors);
        request.DemoWorkWithListOfValidations.Select(p => p.Validate()).Aggregate().EnsureValid(); // II

        // ADDITIONAL DEMO STEP 2 - DEMO Reuse logic and expression
        var hasSavePermissionSnippetTextEntities = await textSnippetEntityRepository.GetAllAsync(
            predicate: TextSnippetEntity.SavePermissionValidator(CurrentUser.UserId<Guid?>()).ValidExpr,
            cancellationToken);
        var isXxxTextSnippetEntityIds = await textSnippetEntityRepository.GetAllAsync(
            queryBuilder: query => query.Where(TextSnippetEntity.SomeSpecificIsXxxLogicValidator().ValidExpr).Select(p => p.Id),
            cancellationToken);

        // ADDITIONAL DEMO STEP 2 - DEMO Example to use validation result as a boolean to change program business flow
        if (ValidateSomeThisCommandApplicationLogicToChangeFlow(validToSaveEntity) || validToSaveEntity.ValidateSomeSpecificIsXxxLogic())
        {
            // Do Some business if ValidateSomeThisCommandLogicToChangeFlow
            // OR savingData.ValidateSomeSpecificDomainLogic
            // RETURN Valid validation result
        }

        // STEP 3: Saving data in to repository
        var savedData = await textSnippetEntityRepository.CreateOrUpdateAsync(
            validToSaveEntity,
            cancellationToken: cancellationToken);

        // STEP 4: Build and return result
        return new SaveSnippetTextCommandResult
        {
            SavedData = new TextSnippetEntityDto(savedData)
        };
    }

    private async Task UpsertFirstExistedMultiDbDemoEntity(CancellationToken cancellationToken)
    {
        var firstExistedMultiDbEntity =
            await multiDbDemoEntityRepository.FirstOrDefaultAsync(cancellationToken: cancellationToken) ??
            new MultiDbDemoEntity
            {
                Id = Guid.NewGuid(),
                Name = "First Multi Db Demo Entity"
            };

        firstExistedMultiDbEntity.Name = $"First Multi Db Demo Entity Upserted on {Clock.Now.ToShortDateString()}";

        await multiDbDemoEntityRepository.CreateOrUpdateAsync(
            firstExistedMultiDbEntity,
            cancellationToken: cancellationToken);
    }

    private PlatformValidationResult<TextSnippetEntity> ValidateSomeThisCommandApplicationLogic(TextSnippetEntity entityToSave)
    {
        return entityToSave
            .Validate(must: p => true, "Example Rule 1 violated error message")
            .And(must: p => true, "Example Rule 2 violated error message")
            .WithApplicationException();
    }

    private PlatformValidationResult<TextSnippetEntity> ValidateSomeThisCommandApplicationLogicToChangeFlow(TextSnippetEntity entityToSave)
    {
        return entityToSave
            .Validate(must: p => true, "Example Rule 1 violated error message")
            .And(must: p => true, "Example Rule 2 violated error message")
            .WithApplicationException();
    }
}
