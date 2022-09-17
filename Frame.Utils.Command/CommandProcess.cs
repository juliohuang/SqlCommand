using System.Data;

namespace Frame.Utils.Command
{
    public delegate void CommandProcess(IDbCommand command);

    public delegate T CommandProcess<T>(IDbCommand command, T result);
}