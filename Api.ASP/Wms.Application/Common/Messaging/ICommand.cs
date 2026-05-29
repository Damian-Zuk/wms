namespace Wms.Application.Common.Messaging;

public interface ICommand : ICommandBase;

public interface ICommand<TResponse> : ICommandBase;

public interface ICommandBase;
