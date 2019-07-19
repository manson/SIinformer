using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIinformer.ApiStuff
{
    // любая операция с удаленного сервера должна иметь параметр о внутренней ошибке
    interface IRemoteOperationResult<T>
    {
        T Result { get; set; }
        string Error { get; set; }
    }
}
