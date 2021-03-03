using System;
using System.Collections;
using System.Data;

namespace Frame.Utils.Command
{
    public partial class CommandExtend
    {
        /// <summary>
        ///     将数据查询映射到对象列表
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paras"></param>
        /// <param name="result"></param>
        /// <param name="memberType"></param>
        /// <param name="includes"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        private static void ReadAll(this Command command, object paras, IList result, Type memberType,
            string[] includes = null,
            IDbTransaction transaction = null)
        {
            var dictionary = ToDictionary(command, paras);
            ReadAll(command, result, memberType, dictionary, includes, transaction);
        }

        /// <summary>
        ///     执行
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paras"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static bool Exec(this Command command, object paras, IDbTransaction transaction = null)
        {
            var dictionary = ToDictionary(command, paras);
            return Exec(command, dictionary, transaction);
        }
    }
}