// ReSharper disable once EmptyNamespace

namespace PlatformExampleApp.TextSnippet.Persistence;


// This file is optional for demo.
// If you want to implement or override your own custom uow, just define a uow implement
// IPlatformEfCoreUnitOfWork or PlatformEfCoreUnitOfWork
//internal class TextSnippetEfCoreUnitOfWork : PlatformEfCoreUnitOfWork<TextSnippetDbContext>
//{
//    public TextSnippetEfCoreUnitOfWork(TextSnippetDbContext dbContext) : base(dbContext)
//    {
//    }

//    public new event EventHandler OnCompleted;
//    public new event EventHandler<UnitOfWorkFailedArgs> OnFailed;

//    public override async Task CompleteAsync(CancellationToken cancellationToken = default)
//    {
//        if (Completed)
//            throw new Exception("This unit of work is completed");

//        try
//        {
//            await DbContext.SaveChangesAsync(cancellationToken);

//            // Some custom code for example after save changes log something

//            Completed = true;
//            OnCompleted?.Invoke(this, EventArgs.Empty);
//        }
//        catch (Exception e)
//        {
//            OnFailed?.Invoke(this, new UnitOfWorkFailedArgs(e));
//            throw;
//        }
//    }
//}
