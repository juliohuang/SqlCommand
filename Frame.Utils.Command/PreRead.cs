using System.Data;

namespace Frame.Utils.Command
{
    public delegate T PreRead<out T>(DataTable reader);
}