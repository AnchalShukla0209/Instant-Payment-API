using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Interfaces
{
    public interface IFileHandler
    {
        public bool DeleteFile(int clientId, string completepath);
    }
}
