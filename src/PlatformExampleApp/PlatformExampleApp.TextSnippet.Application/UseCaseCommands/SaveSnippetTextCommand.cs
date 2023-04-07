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
            .And(p => Data.MapToEntity().Validate().Of(p))
            .ThenValidate(p => p.JustDemoUsingValidateNot())
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
    [SuppressMessage("Minor Code Smell", "S1450:Private fields only used as local variables in methods should become local variables", Justification = "<Pending>")]
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

    /*
     * OTHER CODE FROM OTHER PROJECT DEMO CHAINING WAY TO SHOW THAT CLEAN FLUENT PROGRAMMING CODE COULD DO CHAINING.
     * ALSO DEMO CONVENIENT MAPPING METHOD FOR Task<PlatformValidation<T>> and Task<PlatformValidation<ValueTuple<T1,T2,T3>>>
     *
     * var validationOfResult = await ValidateRequestAsync(request, cancellationToken)
            .WaitValidThenWithAllAsync(
                request => employeeRepository.FirstAsync(
                    predicate: Employee.UniqueExpr(CurrentUser.ProductScope(), CurrentUser.CurrentCompanyId(), CurrentUser.UserId()),
                    cancellationToken,
                    loadRelatedEntities: p => p.User),
                request => employeeRepository.FirstAsync(
                    predicate: Employee.UniqueExpr(CurrentUser.ProductScope(), CurrentUser.CurrentCompanyId(), CurrentUser.UserId()),
                    cancellationToken,
                    loadRelatedEntities: p => p.User))
            .WaitValidThenGetAllAsync(
                (request, currentUserEmployee1, currentUserEmployee2) => currentUserEmployee1.ToTask(),
                (request, currentUserEmployee1, currentUserEmployee2) => currentUserEmployee2.ToTask())
            .WaitValidThenGetAll(
                (currentUserEmployee1, currentUserEmployee2) => currentUserEmployee1,
                (currentUserEmployee1, currentUserEmployee2) => currentUserEmployee2)
            .WaitValidThen(
                (currentUserEmployee1, currentUserEmployee2) =>
                    request.Data.IsSubmitToCreate()
                        ? request.Data.MapToNewEntity()
                            .With(_ => _.EmployeeId = currentUserEmployee1.Id)
                            .With(_ => _.UserId = currentUserEmployee1.UserId)
                            .With(_ => _.TimeZone = request.TimeZone)
                            .ToTask()
                        : leaveRequestRepository
                            .GetByIdAsync(request.Data.Id, cancellationToken)
                            .Then(existingEntity => request.Data.UpdateToEntity(existingEntity)))
            .WaitValidThen(
                toSaveLeaveRequest => toSaveLeaveRequest.ValidateCanBeSavedAsync(
                    isUpdate: request.Data.IsSubmitToUpdate(),
                    leaveRequestRepository,
                    leaveTypeRepository,
                    employeeRemainingLeaveRepository,
                    employeeRepository,
                    CurrentUser.ProductScope(),
                    cancellationToken))
            .WaitValidThen(
                validToSaveLeaveRequest => leaveRequestRepository.CreateOrUpdateAsync(validToSaveLeaveRequest, cancellationToken: cancellationToken)
                    .ThenSideEffectActionAsync(
                        async p =>
                        {
                            // Side effect before return result here using ThenSideEffectActionAsync
                            if (request.IsSendingNotificationEmail && request.Data.IsSubmitToCreate())
                                await leaveRequestEmailHelper.SendEmailApproverAsync(validToSaveLeaveRequest);
                        }))
            .WaitValidThen(
                savedLeaveRequest => new SaveLeaveRequestCommandResult
                {
                    SaveLeaveRequest = new LeaveRequestEntityDto(savedLeaveRequest)
                });

        var ensuredValidResult = await ValidateRequestAsync(request, cancellationToken)
            .EnsureValidAsync()
            .ThenWithAllAsync(
                request => employeeRepository.FirstAsync(
                    predicate: Employee.UniqueExpr(CurrentUser.ProductScope(), CurrentUser.CurrentCompanyId(), CurrentUser.UserId()),
                    cancellationToken,
                    loadRelatedEntities: p => p.User),
                request => employeeRepository.FirstAsync(
                    predicate: Employee.UniqueExpr(CurrentUser.ProductScope(), CurrentUser.CurrentCompanyId(), CurrentUser.UserId()),
                    cancellationToken,
                    loadRelatedEntities: p => p.User))
            .ThenGetAllAsync(
                (request, currentUserEmployee1, currentUserEmployee2) => currentUserEmployee1.ToTask(),
                (request, currentUserEmployee1, currentUserEmployee2) => currentUserEmployee2.ToTask())
            .ThenGetAll(
                (currentUserEmployee1, currentUserEmployee2) => currentUserEmployee1,
                (currentUserEmployee1, currentUserEmployee2) => currentUserEmployee2)
            .Then(
                (currentUserEmployee1, currentUserEmployee2) => request.Data.IsSubmitToCreate()
                    ? request.Data.MapToNewEntity()
                        .With(_ => _.EmployeeId = currentUserEmployee1.Id)
                        .With(_ => _.UserId = currentUserEmployee1.UserId)
                        .With(_ => _.TimeZone = request.TimeZone)
                        .ToTask()
                    : leaveRequestRepository
                        .GetByIdAsync(request.Data.Id, cancellationToken)
                        .Then(existingEntity => request.Data.UpdateToEntity(existingEntity)))
            .Then(
                toSaveLeaveRequest => toSaveLeaveRequest
                    .ValidateCanBeSavedAsync(
                        isUpdate: request.Data.IsSubmitToUpdate(),
                        leaveRequestRepository,
                        leaveTypeRepository,
                        employeeRemainingLeaveRepository,
                        employeeRepository,
                        CurrentUser.ProductScope(),
                        cancellationToken)
                    .EnsureValidAsync())
            .Then(
                validToSaveLeaveRequest => leaveRequestRepository.CreateOrUpdateAsync(validToSaveLeaveRequest, cancellationToken: cancellationToken)
                    .ThenSideEffectActionAsync(
                        async p =>
                        {
                            // Side effect before return result here using ThenSideEffectActionAsync
                            if (request.IsSendingNotificationEmail && request.Data.IsSubmitToCreate())
                                await leaveRequestEmailHelper.SendEmailApproverAsync(validToSaveLeaveRequest);
                        }))
            .Then(
                savedLeaveRequest => new SaveLeaveRequestCommandResult
                {
                    SaveLeaveRequest = new LeaveRequestEntityDto(savedLeaveRequest)
                });
     */

    /// <summary>
    /// DEMO ADDITIONAL VALIDATE REQUEST ASYNC IF NEEDED, OVERRIDE THE FUNCTION ValidateRequestAsync
    /// </summary>
    protected override Task<PlatformValidationResult<SaveSnippetTextCommand>> ValidateRequestAsync(
        PlatformValidationResult<SaveSnippetTextCommand> requestSelfValidation,
        CancellationToken cancellationToken)
    {
        return requestSelfValidation.AndAsync(
            request =>
            {
                // Logic async validation here, example connect database to validate something
                return PlatformValidationResult.Valid(request).ToTask();
            });
    }

    [SuppressMessage("Style", "IDE0039:Use local function", Justification = "<Pending>")]
    [SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>")]
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
                }.ToTask()); // return [{Id:"UserId1",Name:"User UserId1"},{Id:"UserId2",Name:"User UserId2"}]
        var demoGetItemsByExecuteAsyncOnEachOtherItemsWithItemIndex = await Util.ListBuilder.New("UserId1", "UserId2")
            .SelectAsync(
                (userId, itemIndex) => new
                {
                    Id = userId,
                    Name = $"User Index{itemIndex} {userId}"
                }.ToTask()); // return [{Id:"UserId1",Name:"User Index0 UserId1"},{Id:"UserId2",Name:"User Index1 UserId2"}]
        await Util.ListBuilder.New("UserId1", "UserId2")
            .ForEachAsync(
                (userId, itemIndex) => Task.Run(() => logger.LogInformation($"{userId} {itemIndex}"), cancellationToken)); // Demo ForEach call async function

        // Check that an obj could be a object as other type. Like xxx as TXX in c#. Return null if it could not be parsed.
        // This case return null so that EnsureFound will throw not found
        // var demoFluentAs = request.As<TextSnippetEntity>().EnsureFound();
        var demoFluentAsync = request.ToTask(); // equal to Task.FromResult(request)
        // This case return the command because it is IPlatformCqrsCommand. Also demo fluent async task
        // Do not need to use await keyword in parenthesis (await request.AsTask()).As<IPlatformCqrsCommand>().EnsureFound();
        //var demoFluentAsync1 = await request.AsTask()
        //    .Then(request => request.As<IPlatformCqrsCommand>())
        //    .Then(request => request.As<PlatformCqrsCommand<SaveSnippetTextCommandResult>>().AsTask()) // Then work like Promise.Then, support both sync and async func
        //    .EnsureFound();
        var demoSingleValueToListOrArray = request.BoxedInArray().Concat(request.BoxedInList()); // return [request].Concat(List<>{request}) => [request, request]

        try
        {
            // THIS IS NOT RELATED to SaveSnippetText logic. Test support suppress uow works
            using (var uow = UnitOfWorkManager.Begin())
            {
                await textSnippetEntityRepository.UpdateAsync(
                    await textSnippetEntityRepository.FirstOrDefaultAsync(cancellationToken: cancellationToken),
                    cancellationToken: cancellationToken);
                await uow.CompleteAsync(cancellationToken);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "THIS IS NOT RELATED to SaveSnippetText logic. Test support suppress uow works failed.");
        }

        // STEP 1: Build saving entity data from request. Throw not found if update (when id is not null)
        var toSaveEntity = request.Data.IsSubmitToUpdate()
            ? await textSnippetEntityRepository.GetByIdAsync(request.Data.Id!.Value, cancellationToken)
                .EnsureFoundAsync($"Has not found text snippet for id {request.Data.Id}")
                .Then(existingEntity => request.Data.UpdateToEntity(existingEntity))
            : request.Data.MapToNewEntity();

        // STEP 2: Do validation and ensure that all logic is valid
        var validToSaveEntity = await toSaveEntity
            .With(
                toSaveEntity =>
                {
                    //toSaveEntity.SnippetText += " Update";  //Demo Update Data By With Support Chaining
                })
            .ValidateSavePermission(userId: CurrentUser.UserId<Guid?>()) // Demo Permission Logic
            .And(entity => toSaveEntity.ValidateSomeSpecificIsXxxLogic()) // Demo domain business logic
            .AndAsync(entity => toSaveEntity.ValidateSomeSpecificIsXxxLogicAsync(textSnippetEntityRepository, multiDbDemoEntityRepository)) // Demo domain business logic
            .AndAsync(entity => ValidateSomeThisCommandApplicationLogic(entity)) // Demo application business logic
            .EnsureValidAsync(); // Throw PermissionException, or DomainException, or ApplicationException on invalid for each stage

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
