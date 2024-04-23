// ReSharper disable once EmptyNamespace

#pragma warning disable S3261 // Namespaces should not be empty

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;
#pragma warning restore S3261 // Namespaces should not be empty

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
//        return base.CompleteAsync(cancellationToken);
//    }
//}
