// ReSharper disable once EmptyNamespace

//namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql;

// This file is optional for demo.
// If you want to implement or override your own custom uow, just define a uow implement
// IPlatformEfCoreUnitOfWork or PlatformEfCoreUnitOfWork
//internal class TextSnippetEfCoreUnitOfWork : PlatformEfCoreUnitOfWork<TextSnippetDbContext>
//{
//    public TextSnippetEfCoreUnitOfWork(TextSnippetDbContext dbContext) : base(dbContext)
//    {
//    }

//    public new event EventHandler OnCompleted;
//    public new event EventHandler<PlatformUnitOfWorkFailedArgs> OnFailed;

//    public override async Task CompleteAsync(CancellationToken cancellationToken = default)
//    {
//        return base.CompleteAsync(cancellationToken);
//    }
//}


