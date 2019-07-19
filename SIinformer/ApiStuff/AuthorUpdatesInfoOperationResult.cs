using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIinformer.ApiStuff
{
    public class AuthorUpdatesInfoOperationResult : IRemoteOperationResult<byte[]>
    {
        /// <summary>
        /// упакованный бинарной сериализацией json-массив книг типа TransportBookInfo
        /// </summary>
        public byte[] Result { get; set; }
        /// <summary>
        /// сообщение об ошибке
        /// </summary>
        public string Error { get; set; }
        /// <summary>
        /// время последней проверки автора кем-то из информаторов
        /// </summary>
        public long CheckDate { get; set; }
    }
}
