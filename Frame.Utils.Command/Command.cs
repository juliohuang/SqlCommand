using System.Data;

namespace Frame.Utils.Command
{
    /// <summary>
    ///     Sql 命令
    /// </summary>
  
    public class Command
    {
        public Command()
        {
            CommandType = CommandType.Text;
        }

        /// <summary>
        ///     预编译
        /// </summary>
        public bool Precompiled { get; set; }

        /// <summary>
        ///     ID
        /// </summary>
       
        public string Id { get; set; }

        /// <summary>
        ///     DataBase Name
        /// </summary>
       
        public string DbName { get; set; }

        /// <summary>
        ///     Command Text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        ///     Command Type
        /// </summary>
     
        public CommandType CommandType { get; set; }
    }
}