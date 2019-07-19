using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIinformer.ApiStuff
{
    public class BooleanOperationResult : IRemoteOperationResult<bool>
    {
        public bool Result { get; set; }
        public string Error { get; set; }
    }
}
