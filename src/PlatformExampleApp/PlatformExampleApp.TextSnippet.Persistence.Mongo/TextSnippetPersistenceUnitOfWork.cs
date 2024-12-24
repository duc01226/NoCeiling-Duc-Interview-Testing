// ReSharper disable once EmptyNamespace

// namespace PlatformExampleApp.TextSnippet.Persistence.Mongo;

// If you want to implement or override your own custom uow, just define a uow implement
// IPlatformMongoDbUnitOfWork or PlatformMongoDbUnitOfWork
//internal class TextSnippetPersistenceUnitOfWork : PlatformMongoDbUnitOfWork<TextSnippetDbContext>
//{
//    public TextSnippetPersistenceUnitOfWork(TextSnippetDbContext dbContext) : base(dbContext)
//    {
//    }

//    public new event EventHandler OnCompleted;
//    public new event EventHandler<PlatformUnitOfWorkFailedArgs> OnFailed;

//    public override Task CompleteAsync(CancellationToken cancellationToken = default)
//    {
//        return base.CompleteAsync(cancellationToken);
//    }
//}


