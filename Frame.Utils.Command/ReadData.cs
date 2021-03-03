using System.Data;

namespace Frame.Utils.Command
{
    public delegate void ReadData(IDataReader reader);

    public delegate void ReadData<in T>(IDataReader reader, T t);
}