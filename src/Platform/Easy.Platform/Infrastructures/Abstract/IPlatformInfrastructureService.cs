namespace Easy.Platform.Infrastructures.Abstract;

/// <summary>
/// This interface is used for conventional register class mapping via PlatformApplicationModule.InternalRegister
/// <br/>
/// InfrastructureService is used to serve some infrastructure/third-party services, not related to domain, likes: EmailService, FileStorageService, etc...
/// <br/>
/// InfrastructureService is used by Queries/Commands.
/// </summary>
public interface IPlatformInfrastructureService
{
}
