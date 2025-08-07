using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.Application.Services
{
    public class FileHandler : IFileHandler
    {
        private readonly string _webRootPath;
        public FileHandler(string webRootPath)
        {
            _webRootPath = webRootPath;
        }

        public bool DeleteFile(int clientId, string completepath)
        {
            string fullPath = Path.Combine(_webRootPath, completepath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }
            return false;
        }
    }
}
