using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace InstantPay.SharedKernel.RandomNumberGenerator
{
    public static class ReferenceGenerator
    {
        public static string GenerateCustomerRefNo(int length = 12)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(
                System.Security.Cryptography.RandomNumberGenerator
                    .GetBytes(length)
                    .Select(b => chars[b % chars.Length])
                    .ToArray()
            );
        }
    }
}
